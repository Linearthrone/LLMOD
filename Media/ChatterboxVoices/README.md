# Chatterbox Turbo reference voices

Place one **5–10 second WAV clip** per voice here. The file name (without `.wav`) is the voice id used in persona settings and voice calls.

Example:

- `default.wav` — fallback voice when a persona has no voice set
- `victoria.wav` — custom cloned voice for a persona

Requirements:

- Mono or stereo WAV, any sample rate (the server resamples internally)
- Clear speech, minimal background noise
- At least ~5 seconds of continuous speech

After adding clips, restart the Chatterbox TTS server (`start.bat` or `.ps1 scripts/start-chatterbox.ps1`).
