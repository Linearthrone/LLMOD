"""
Minimal STT server using faster-whisper.
Exposes POST /transcribe (multipart "audio" or "file", audio/wav) -> {"text": "..."}
Compatible with House Victoria OllamaAIService.ProcessAudioWithWhisperAsync.
"""
from __future__ import annotations

import os
from io import BytesIO

from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse

# Lazy-load model on first request
_model = None
_MODEL_NAME = os.environ.get("WHISPER_MODEL", "base")  # base = fast; small/medium = better quality


def get_model():
    global _model
    if _model is None:
        from faster_whisper import WhisperModel
        device = os.environ.get("WHISPER_DEVICE", "auto")  # auto, cpu, cuda
        compute_type = os.environ.get("WHISPER_COMPUTE_TYPE", "default")  # default, float16, int8
        _model = WhisperModel(_MODEL_NAME, device=device, compute_type=compute_type or "default")
    return _model


app = FastAPI(title="STT Server (faster-whisper)", version="1.0")


@app.get("/health")
def health():
    return {"status": "ok", "model": _MODEL_NAME}


@app.post("/transcribe")
async def transcribe(audio: UploadFile = File(None), file: UploadFile = File(None)):
    """Accept multipart 'audio' or 'file' (WAV preferred). Returns {"text": "..."}."""
    upload = audio or file
    if not upload:
        raise HTTPException(400, "Missing multipart field 'audio' or 'file'")
    try:
        data = await upload.read()
    except Exception as e:
        raise HTTPException(400, f"Failed to read upload: {e}") from e
    if not data:
        return JSONResponse(content={"text": ""})
    try:
        model = get_model()
        buffer = BytesIO(data)
        segments, _ = model.transcribe(buffer)
        text = " ".join(s.text for s in segments).strip() if segments else ""
        return JSONResponse(content={"text": text})
    except Exception as e:
        raise HTTPException(500, f"Transcription failed: {e}") from e


if __name__ == "__main__":
    import uvicorn
    host = os.environ.get("STT_HOST", "127.0.0.1")
    port = int(os.environ.get("STT_PORT", "8000"))
    uvicorn.run(app, host=host, port=port)
