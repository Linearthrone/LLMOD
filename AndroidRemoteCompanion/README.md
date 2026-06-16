# Victoria Link (Android Remote Companion v0.3)

Messaging-style remote companion for House Victoria — inbox threads, persona directory, per-contact chat with avatars and accent themes.

## App flow

| Screen | Purpose |
|--------|---------|
| **Inbox** | Conversation threads for every AI contact on the PC |
| **Persona Directory** | Grid of all AI contacts — tap to open a thread |
| **Chat** | Persona header (avatar, name, status) + themed bubbles + voice |
| **Settings** | Tailscale URL + API token only (no chat config here) |

Toolbar icons: **Personas** (contact book) · **Settings** (connection)

## PC API (House Victoria remote companion)

Requires **House Victoria restart** after updating the PC app.

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `GET /api/remote/v1/health` | No | Liveness |
| `GET /api/remote/v1/contacts` | Bearer | AI contact book + last message preview |
| `GET /api/remote/v1/contacts/{id}/messages` | Bearer | Thread history |
| `GET /api/remote/v1/contacts/{id}/avatar` | Bearer | Contact portrait (local file on PC) |
| `POST /api/remote/v1/chat` | Bearer | Text chat (`contactId` in body) |
| `POST /api/remote/v1/chat-audio` | Bearer | Voice upload |

## Setup

1. **PC:** Enable remote companion in House Victoria Settings, set API token (16+ chars), restart app.
2. **Tailscale:** Run `scripts/Setup-TailscaleRemoteCompanion.ps1` on the PC.
3. **Phone:** Install Tailscale, open Victoria Link → **Settings** → paste HTTPS URL + token → **Test link** → **Save**.
4. Open **Personas** or tap a thread to chat with any AI contact.

## Build

```powershell
cd AndroidRemoteCompanion
.\gradlew.bat installDebug
```

## Design notes

- Each persona gets a unique accent from the House Victoria palette (bubbles, headers, rings).
- Avatars load from the PC when the persona has a portrait path set in AI Models.
- Screen transitions use slide/fade animations; inbox opens with a subtle scale-in.
