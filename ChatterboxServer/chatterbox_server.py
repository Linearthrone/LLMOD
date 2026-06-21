"""
Chatterbox Turbo TTS HTTP server for House Victoria.
Exposes POST / (JSON {text, voice?, speed?}) -> WAV bytes.
Reference voices: Media/ChatterboxVoices/{voice}.wav (5–10 s clips).
"""
from __future__ import annotations

import io
import logging
import os
import sys
import wave
from pathlib import Path
from typing import Optional

import numpy as np

try:
    from fastapi import FastAPI, HTTPException
    from fastapi.responses import Response
    from pydantic import BaseModel, Field
    import uvicorn
except ImportError:
    print("ERROR: pip install fastapi uvicorn pydantic", file=sys.stderr)
    raise

logging.basicConfig(
    level=logging.DEBUG if os.environ.get("CHATTERBOX_DEBUG") else logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger("chatterbox-server")

HOST = os.environ.get("CHATTERBOX_HOST", "127.0.0.1")
PORT = int(os.environ.get("CHATTERBOX_PORT", "8881"))
DEFAULT_VOICE = os.environ.get("CHATTERBOX_DEFAULT_VOICE", "default")
DEVICE = os.environ.get("CHATTERBOX_DEVICE", "cuda")

_repo_root = Path(__file__).resolve().parent.parent
_voices_dir = Path(
    os.environ.get("CHATTERBOX_VOICES_DIR", str(_repo_root / "Media" / "ChatterboxVoices"))
).resolve()

_model = None
_model_sr = 44100

app = FastAPI(title="Chatterbox Turbo TTS", version="1.0")


class TtsRequest(BaseModel):
    text: str
    voice: Optional[str] = None
    speed: Optional[float] = Field(default=1.0, ge=0.5, le=2.0)


def _resolve_repo_voices_dir() -> Path:
    if _voices_dir.is_dir():
        return _voices_dir
    _voices_dir.mkdir(parents=True, exist_ok=True)
    return _voices_dir


def _list_voice_stems() -> list[str]:
    voices_dir = _resolve_repo_voices_dir()
    stems = sorted(
        {
            p.stem
            for p in voices_dir.glob("*.wav")
            if p.is_file() and not p.name.startswith(".")
        },
        key=str.lower,
    )
    return stems or [DEFAULT_VOICE]


def _resolve_voice_path(voice: Optional[str]) -> Path:
    voices_dir = _resolve_repo_voices_dir()
    stem = (voice or DEFAULT_VOICE).strip()
    if not stem:
        stem = DEFAULT_VOICE

    direct = voices_dir / f"{stem}.wav"
    if direct.is_file():
        return direct

    available = _list_voice_stems()
    if available and available[0] != DEFAULT_VOICE:
        fallback = voices_dir / f"{available[0]}.wav"
        if fallback.is_file():
            logger.warning("Voice '%s' not found; using '%s'", stem, available[0])
            return fallback

    raise FileNotFoundError(
        f"No reference clip for voice '{stem}'. "
        f"Add Media/ChatterboxVoices/{stem}.wav (5–10 s WAV)."
    )


def _get_model():
    global _model, _model_sr
    if _model is not None:
        return _model

    import torch
    from chatterbox.tts_turbo import ChatterboxTurboTTS

    device = DEVICE
    if device == "cuda" and not torch.cuda.is_available():
        logger.warning("CUDA unavailable; falling back to CPU.")
        device = "cpu"

    logger.info("Loading Chatterbox Turbo on %s (first request may take a minute)...", device)
    _model = ChatterboxTurboTTS.from_pretrained(device=device)
    _model_sr = int(getattr(_model, "sr", 44100))
    logger.info("Chatterbox Turbo ready (sample rate %s Hz).", _model_sr)
    return _model


def _tensor_to_wav_bytes(wav_tensor) -> bytes:
    import torch

    if isinstance(wav_tensor, torch.Tensor):
        audio = wav_tensor.detach().cpu().float().numpy()
    else:
        audio = np.asarray(wav_tensor, dtype=np.float32)

    audio = np.squeeze(audio)
    if audio.ndim != 1:
        audio = audio.reshape(-1)

    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if peak > 1.0:
        audio = audio / peak

    pcm16 = np.clip(audio * 32767.0, -32768, 32767).astype(np.int16)

    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(_model_sr)
        wf.writeframes(pcm16.tobytes())
    return buf.getvalue()


@app.get("/")
async def root():
    return {
        "status": "ok",
        "service": "chatterbox-turbo",
        "voices_dir": str(_resolve_repo_voices_dir()),
        "sample_rate": _model_sr,
    }


@app.get("/health")
async def health():
    voices = _list_voice_stems()
    return {
        "status": "ok",
        "service": "chatterbox-turbo",
        "model_loaded": _model is not None,
        "voices": voices,
        "default_voice": DEFAULT_VOICE,
    }


@app.get("/v1/voices")
async def list_voices():
    return {"voices": _list_voice_stems()}


@app.post("/")
async def synthesize(request: TtsRequest):
    text = (request.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="Text is required")

    try:
        voice_path = _resolve_voice_path(request.voice)
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc

    try:
        model = _get_model()
        wav = model.generate(text, audio_prompt_path=str(voice_path))
        wav_data = _tensor_to_wav_bytes(wav)
    except Exception as exc:
        logger.error("Synthesis failed: %s", exc, exc_info=True)
        raise HTTPException(status_code=500, detail=f"Synthesis failed: {exc}") from exc

    return Response(
        content=wav_data,
        media_type="audio/wav",
        headers={"Content-Length": str(len(wav_data))},
    )


def main():
    voices_dir = _resolve_repo_voices_dir()
    logger.info("Chatterbox voices directory: %s", voices_dir)
    logger.info("Starting Chatterbox Turbo TTS on http://%s:%s", HOST, PORT)
    uvicorn.run(app, host=HOST, port=PORT, log_level="info")


if __name__ == "__main__":
    main()
