# Victoria Link (Android Remote Companion v0.4)

Messaging-style remote companion for House Victoria — inbox threads, persona directory, per-contact chat with avatars and accent themes, media generation, and gallery.

## What's new in v0.4

- **App themes** — Settings → Themes: House Victoria, Amethyst, Emerald, Sunset palettes with mesh backgrounds and accent wiring
- **System monitor** — Inbox card shows PC CPU/RAM/GPU via `GET /api/remote/v1/system/status`; auto-refreshes every ~12s while inbox is visible
- **MediaGen** — Generate images on the PC from the phone (`POST /api/remote/v1/media/generate`)
- **Gallery** — Browse generated media from the PC (`GET /api/remote/v1/media/{id}/file`)
- **Chat images** — Image bubbles in threads when personas reply with media (`hasMedia` + message media endpoint)
- **Bottom navigation** — Home / MediaGen / Gallery / Settings on Chat and Persona Directory screens
- **Persona image replies** — PC-side pipeline (TASK-002) generates and delivers images when personas promise or are asked for pictures

**Requires House Victoria desktop restart** after updating the PC app so Remote Companion v3 APIs and the image pipeline load.

## App flow

| Screen | Purpose |
|--------|---------|
| **Inbox** | Conversation threads + live system monitor card |
| **Persona Directory** | Grid of all AI contacts — tap to open a thread |
| **Chat** | Persona header (avatar, name, status) + themed bubbles, voice, and image messages |
| **MediaGen** | Prompt-based image generation against PC ComfyUI/models |
| **Gallery** | Grid of generated media from the PC |
| **Settings** | Connection (Tailscale URL + API token) and app theme picker |

Toolbar icons: **Personas** (contact book) · **Settings** (connection)  
Bottom nav (Chat + Personas): **Home** · **MediaGen** · **Gallery** · **Settings**

## PC API (House Victoria remote companion)

Requires **House Victoria restart** after updating the PC app.

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `GET /api/remote/v1/health` | No | Liveness |
| `GET /api/remote/v1/system/status` | Bearer | CPU/RAM/GPU snapshot |
| `GET /api/remote/v1/contacts` | Bearer | AI contact book + last message preview |
| `GET /api/remote/v1/contacts/{id}/messages` | Bearer | Thread history |
| `GET /api/remote/v1/contacts/{id}/avatar` | Bearer | Contact portrait (local file on PC) |
| `GET /api/remote/v1/messages/{id}/media` | Bearer | Image/media attachment for a message |
| `POST /api/remote/v1/chat` | Bearer | Text chat (`contactId` in body) |
| `POST /api/remote/v1/chat-audio` | Bearer | Voice upload |
| `POST /api/remote/v1/chat-image` | Bearer | User-sent image to persona |
| `GET /api/remote/v1/media/models` | Bearer | Available image models |
| `POST /api/remote/v1/media/generate` | Bearer | Generate image from prompt |
| `GET /api/remote/v1/media/{id}/file` | Bearer | Download generated media file |

## Setup

1. **PC:** Enable remote companion in House Victoria Settings, set API token (16+ chars), **restart app**.
2. **Tailscale:** Run `scripts/Setup-TailscaleRemoteCompanion.ps1` on the PC.
3. **Phone:** Install Tailscale, open Victoria Link → **Settings** → paste HTTPS URL + token → **Test link** → **Save**.
4. Open **Personas** or tap a thread to chat with any AI contact.

## Build

```powershell
cd AndroidRemoteCompanion
.\gradlew.bat assembleDebug
# or install to device:
.\gradlew.bat installDebug
```

**Version:** `versionCode` 3 · `versionName` 0.4.0

## Design notes

- Each persona gets a unique accent from the House Victoria palette (bubbles, headers, rings).
- Avatars load from the PC when the persona has a portrait path set in AI Models.
- App-wide theme selection persists and applies mesh backgrounds + accent colors across screens.
- Screen transitions use slide/fade animations; inbox opens with a subtle scale-in.
