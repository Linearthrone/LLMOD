# STT Server (faster-whisper)

Minimal speech-to-text server for House Victoria. Uses [faster-whisper](https://github.com/SYSTRAN/faster-whisper) for local transcription.

## Endpoints

- **POST /transcribe** — multipart form field `audio` or `file` (WAV/audio). Returns `{"text": "..."}`.
- **GET /health** — `{"status": "ok", "model": "base"}`.

## Setup

From the repo root (or with venv activated):

```bash
pip install -r STTServer/requirements.txt
```

Or run `install.bat`; it will install STT deps into the MCP venv.

## Run

- **Via start.bat** — STT server starts automatically on port 8000 if `STTServer\app.py` exists and a venv is available.
- **Manual:**

  ```bash
  cd /d path\to\LLMOD-max-master
  .venv\Scripts\python.exe -m uvicorn STTServer.app:app --host 127.0.0.1 --port 8000
  ```

## Config

- **App:** Set **STT Endpoint** in Settings to `http://localhost:8000/transcribe`, or leave default in `App.config`.
- **Env (optional):** `WHISPER_MODEL=base` (default), `small`, `medium`, `large-v3`; `WHISPER_DEVICE=auto|cpu|cuda`; `WHISPER_COMPUTE_TYPE=default|float16|int8`; `STT_PORT=8000`.

Model `base` is fastest; `small`/`medium` improve accuracy at higher CPU/GPU cost.
