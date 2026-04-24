# Android Remote Companion (MVP v2)

Minimal Android app for House Victoria remote companion:

- `GET /api/remote/v1/health`
- `POST /api/remote/v1/chat` (Bearer token)
- `POST /api/remote/v1/chat-audio` (`multipart/form-data`, field name: `audio`)

This MVP now supports both text and short microphone audio turns.

## 1) Prerequisites on PC

In House Victoria settings:

- `RemoteCompanionEnabled = true`
- `RemoteCompanionApiToken` set (16+ chars)
- Optional: keep `RemoteCompanionListenOnLan = false` and use Tailscale / Cloudflare Tunnel

Restart House Victoria after changing these settings.

## 2) Open in Android Studio

1. Open folder: `AndroidRemoteCompanion/`
2. Let Gradle sync.
3. Run on device/emulator (API 26+).

## 3) Configure in app

- **Base URL** examples:
  - Local testing (same network / adb reverse): `http://127.0.0.1:17890`
  - Tunnel URL: `https://your-subdomain.example`
- **API token**: same value as House Victoria setting.
- **Contact ID (optional)**: if provided, sent as `contactId` to both text and audio endpoints.
- Tap **Check Health**, then send text and/or audio chat.

## 4) Audio usage and permissions

- App permission required: `RECORD_AUDIO`.
- On first audio attempt, Android prompts for microphone access.
- Tap **Record & Send Audio** to start capture, then **Stop & Send Audio** to upload.
- Audio endpoint request:
  - URL: `/api/remote/v1/chat-audio`
  - Content-Type: `multipart/form-data`
  - File field: `audio`
  - Optional form field: `contactId`
- The app displays API responses including `conversationId`.

Error handling includes explicit messages for:

- `audio_field_required`
- `multipart_form_required`
- `unauthorized`

## 5) Reliability and UX

- In-session chronological conversation log (user + assistant + system/error events).
- Base URL / token / contact ID are persisted locally.
- URL/token validation before text or audio send.
- Loading states and retry button for transient network failures.

## 6) Next planned upgrades

- Optional certificate pinning for public tunnel host.
