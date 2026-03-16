#!/usr/bin/env python3
"""
HTTP server wrapper for Piper TTS.
Exposes Piper TTS as an HTTP service on port 5000.
"""

import argparse
import io
import json
import logging
import sys
import wave
from pathlib import Path
from typing import Optional

try:
    from fastapi import FastAPI, HTTPException
    from fastapi.responses import Response
    from pydantic import BaseModel
    import uvicorn
except ImportError:
    print("ERROR: Required packages not installed. Install with:")
    print("  pip install fastapi uvicorn pydantic")
    sys.exit(1)

try:
    from piper import PiperVoice
except ImportError:
    print("ERROR: piper package not installed. Install with:")
    print("  pip install piper-tts")
    sys.exit(1)

# Configure logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

app = FastAPI(title="Piper TTS Server")

# Global voice instance
_voice: Optional[PiperVoice] = None
_voice_path: Optional[Path] = None


class TtsRequest(BaseModel):
    text: str
    voice: Optional[str] = None
    speed: Optional[float] = 1.0


def find_voice_model(voice_name: str, data_dirs: list[Path]) -> Optional[Path]:
    """Find voice model file in data directories."""
    # Try direct path first
    voice_path = Path(voice_name)
    if voice_path.exists():
        return voice_path
    
    # Try with .onnx extension
    if not voice_path.suffix:
        voice_path = voice_path.with_suffix('.onnx')
        if voice_path.exists():
            return voice_path
    
    # Search in data directories
    for data_dir in data_dirs:
        maybe_path = data_dir / f"{voice_name}.onnx"
        logger.debug(f"Checking '{maybe_path}'")
        if maybe_path.exists():
            return maybe_path
    
    return None


def load_voice(model_path: Path, use_cuda: bool = False) -> PiperVoice:
    """Load Piper voice model."""
    logger.info(f"Loading voice model: {model_path}")
    if not model_path.exists():
        raise FileNotFoundError(f"Voice model not found: {model_path}")
    
    voice = PiperVoice.load(model_path, use_cuda=use_cuda)
    logger.info(f"Voice model loaded successfully: {model_path}")
    return voice


@app.get("/")
async def root():
    """Health check endpoint."""
    return {"status": "ok", "service": "piper-tts"}


@app.get("/health")
async def health():
    """Health check endpoint."""
    return {"status": "ok", "voice_loaded": _voice is not None}


@app.get("/voices")
async def list_voices():
    """List available voices (returns current voice if loaded)."""
    if _voice_path:
        return {"voices": [_voice_path.stem]}
    return {"voices": []}


@app.post("/")
async def synthesize(request: TtsRequest):
    """Synthesize speech from text."""
    global _voice
    
    if not _voice:
        raise HTTPException(status_code=503, detail="Voice model not loaded")
    
    if not request.text or not request.text.strip():
        raise HTTPException(status_code=400, detail="Text is required")
    
    try:
        # Synthesize audio and collect chunks
        audio_chunks = []
        sample_rate = None
        sample_width = None
        sample_channels = None
        
        for audio_chunk in _voice.synthesize(request.text.strip()):
            audio_chunks.append(audio_chunk.audio_int16_bytes)
            # Get format info from first chunk
            if sample_rate is None:
                sample_rate = audio_chunk.sample_rate
                sample_width = audio_chunk.sample_width
                sample_channels = audio_chunk.sample_channels
        
        if not audio_chunks:
            raise HTTPException(status_code=500, detail="No audio generated")
        
        # Combine all chunks
        audio_data = b''.join(audio_chunks)
        
        # Create WAV file in memory
        wav_buffer = io.BytesIO()
        with wave.open(wav_buffer, 'wb') as wav_file:
            wav_file.setframerate(sample_rate)
            wav_file.setsampwidth(sample_width)
            wav_file.setnchannels(sample_channels)
            wav_file.writeframes(audio_data)
        
        wav_data = wav_buffer.getvalue()
        
        # Return WAV audio
        return Response(
            content=wav_data,
            media_type="audio/wav",
            headers={
                "Content-Length": str(len(wav_data)),
                "Content-Type": "audio/wav"
            }
        )
    except Exception as e:
        logger.error(f"Error synthesizing speech: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Synthesis failed: {str(e)}")


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Piper TTS HTTP Server")
    parser.add_argument(
        "-m", "--model",
        required=True,
        help="Voice model name or path (e.g., 'en_US-amy-medium')"
    )
    parser.add_argument(
        "--data-dir",
        "--data_dir",
        action="append",
        default=[],
        help="Data directory to search for voice models (can be specified multiple times)"
    )
    parser.add_argument(
        "--port",
        type=int,
        default=5000,
        help="HTTP server port (default: 5000)"
    )
    parser.add_argument(
        "--host",
        default="127.0.0.1",
        help="HTTP server host (default: 127.0.0.1)"
    )
    parser.add_argument(
        "--cuda",
        action="store_true",
        help="Use CUDA/GPU acceleration"
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug logging"
    )
    
    args = parser.parse_args()
    
    if args.debug:
        logging.getLogger().setLevel(logging.DEBUG)
    
    # Prepare data directories
    data_dirs = [Path(d).resolve() for d in args.data_dir]
    if not data_dirs:
        # Default to current directory
        data_dirs = [Path.cwd()]
    
    # Find and load voice model
    global _voice, _voice_path
    model_path = find_voice_model(args.model, data_dirs)
    
    if not model_path:
        logger.error(f"Voice model not found: {args.model}")
        logger.error(f"Searched in directories: {data_dirs}")
        sys.exit(1)
    
    _voice_path = model_path
    _voice = load_voice(model_path, use_cuda=args.cuda)
    
    # Start server
    logger.info(f"Starting Piper TTS HTTP server on {args.host}:{args.port}")
    logger.info(f"Voice model: {model_path}")
    
    uvicorn.run(
        app,
        host=args.host,
        port=args.port,
        log_level="debug" if args.debug else "info"
    )


if __name__ == "__main__":
    main()
