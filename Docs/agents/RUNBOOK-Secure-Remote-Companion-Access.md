# Runbook · Secure remote access (House Victoria remote companion)

**Scope:** How to expose the PC-hosted **remote companion HTTP API** to a phone **without** treating “obscure port” as security. TLS and strong auth are expected at the tunnel or mesh edge; the app listens on **plain HTTP** on loopback or (optionally) LAN.

**Related:** `Docs/agents/GOALS-Remote-Companion-and-AI-Home.md` · API: `GET /api/remote/v1/health`, `POST /api/remote/v1/chat`, `POST /api/remote/v1/chat-audio`.

**Defaults (see `HouseVictoria.Core` / Settings):**

| Setting | Typical |
|--------|---------|
| Port | **17890** (`RemoteCompanionListenPort`) |
| Bind | **127.0.0.1** if `RemoteCompanionListenOnLan` is **false** (recommended with a tunnel) |
| Bind | **0.0.0.0** if `RemoteCompanionListenOnLan` is **true** (LAN subnet; tighten firewall) |
| API secret | `RemoteCompanionApiToken` — **≥ 16 characters**; required when remote companion is enabled |

**Security model (summary):**

- Prefer **loopback-only** HTTP + **Tailscale** or **Cloudflare Tunnel** to the PC. The tunnel provides encryption in transit; the app still enforces **Bearer** or **`X-Api-Key`** on chat/audio routes.
- **`/api/remote/v1/health` does not require a token** in current code — treat the tunnel hostname as trusted only if the tunnel ACL is tight; do not port-forward WAN → 17890 without a tunnel.

---

## Recommended option A — Tailscale (or equivalent mesh VPN)

**When to use:** You want phone ↔ PC connectivity with **minimal public DNS** exposure; both devices run Tailscale.

**Outline:**

1. Install Tailscale on the **Windows PC** and **phone**; sign in to the same tailnet (or use ACLs for device sharing).
2. Confirm the PC’s **Tailscale IP** (e.g. `100.x.y.z`) — from the Tailscale admin console or `tailscale status` on the PC.
3. Keep **House Victoria** bound to **loopback**: set `RemoteCompanionListenOnLan` = **false** so Kestrel listens on `127.0.0.1:17890` only.
4. From the **phone** (Wi-Fi or cellular, as long as Tailscale is on), open a browser or HTTP client and call:
   - `http://127.0.0.1:17890` is **not** used on the phone — use **`http://<tailscale-ip>:17890/api/remote/v1/health`** (or MagicDNS hostname if configured).
5. **Firewall on Windows:** With **loopback-only** bind, you **do not** need an inbound rule for port 17890 on the LAN interface; Tailscale reaches the stack per Tailscale’s own routing. If you previously added rules for 17890, remove or restrict them.

**Operational note:** Tailscale ACLs can further restrict which devices may hit which ports — use them for defense in depth.

---

## Recommended option B — Cloudflare Tunnel (or equivalent) to localhost

**When to use:** You want a **public HTTPS URL** (or Cloudflare Access–protected URL) terminating TLS at the edge, with origin = `127.0.0.1:<port>`.

**Outline (conceptual; exact flags depend on `cloudflared` version):**

1. Run **House Victoria** with **`RemoteCompanionListenOnLan` = false** so the service listens on **`http://127.0.0.1:17890`** only.
2. Install and authenticate **cloudflared** on the PC (Cloudflare Zero Trust dashboard: create a tunnel, get a token).
3. Configure a **public hostname** → **HTTP** origin `http://127.0.0.1:17890` (or use a **private hostname** + WARP / Access as your policy requires).
4. **Do not** enable **“public + no Access”** on the tunnel without considering that **`/health` is unauthenticated** — prefer **Cloudflare Access** or **token-protected hostname** so only your account can reach the origin.
5. Validate: `curl -sS "https://<your-hostname>/api/remote/v1/health"` should return JSON with `"ok":true`.

**Secrets:** Tunnel credentials and Cloudflare tokens belong in **cloudflared** config or a sealed store — **not** in chat or a public repo.

---

## Windows Firewall — only if LAN listen is enabled

If **`RemoteCompanionListenOnLan` = true**, Kestrel binds **`0.0.0.0:<port>`** and the port is reachable on **local network interfaces**. **Do not** expose this to the internet without a proper front door (tunnel + policy).

**Typical locked-down approach:**

1. **Scope the rule:** allow **TCP <port>** (default 17890) **only** from **private subnet(s)** you control (e.g. `192.168.1.0/24`), **not** `Any`.
2. **Prefer “allow from Private profile only”** if applicable to your LAN design.
3. Optional: restrict to specific **remote IP** (e.g. one test phone’s Wi-Fi IP) during bring-up only.
4. **Verify:** from another LAN device, `curl http://<pc-lan-ip>:17890/api/remote/v1/health` — expect `200` and JSON.

**Remove** wide-open rules after testing.

**PowerShell examples (adjust subnet and port):**

```powershell
# Example: inbound TCP 17890 from home LAN only (run elevated)
New-NetFirewallRule -DisplayName "HV Remote Companion (LAN restricted)" `
  -Direction Inbound -Action Allow -Protocol TCP -LocalPort 17890 `
  -RemoteAddress 192.168.1.0/24 -Profile Private
```

```powershell
# Remove the rule when switching back to loopback + tunnel
Remove-NetFirewallRule -DisplayName "HV Remote Companion (LAN restricted)"
```

---

## Operational checklist

Use this after any change to tunnel, firewall, or House Victoria remote settings.

| Step | Action | Pass criterion |
|------|--------|----------------|
| 1 | PC: House Victoria running; remote companion **enabled**; token set (≥16 chars). | Startup log shows listener line; no “not started — token” message. |
| 2 | PC: `curl http://127.0.0.1:<port>/api/remote/v1/health` | HTTP 200; body includes `"ok":true`. |
| 3 | Phone on **cellular** (Wi-Fi off): same check **through tunnel/mesh URL** as used in production (not only LAN Wi-Fi). | Same JSON as step 2 (screenshot or redacted log line acceptable). |
| 4 | Chat route (optional regression): `POST /api/remote/v1/chat` with **`Authorization: Bearer <token>`** or **`X-Api-Key`**. | `401` without secret; `200`/valid payload with secret. |

**Token handling (operations):**

- **Generate:** password manager or `openssl rand -hex 32` (paste into Settings / sealed config — **never** commit).
- **Store:** OS credential locker, `.env` excluded from git, or encrypted config — **no plaintext** in ticket/chat.
- **Rotate:** generate new token → update House Victoria → update phone client / scripted callers → revoke old references.

---

## Android quickstart lane (real-device testing)

Use this lane for `AndroidRemoteCompanion/` and QA validation.

### 1) Set PC service state

1. In House Victoria Settings:
   - `RemoteCompanionEnabled = true`
   - `RemoteCompanionApiToken` set to 16+ chars
   - Preferred: `RemoteCompanionListenOnLan = false` (loopback + tunnel)
2. Restart House Victoria after changes.
3. Confirm local health:
   - `curl http://127.0.0.1:17890/api/remote/v1/health`

### 2) Choose Android base URL

- **Tailscale (mesh):** `http://<tailscale-ip>:17890`
- **Cloudflare Tunnel (recommended internet path):** `https://<your-hostname>`
- **LAN fallback (only with strict firewall + LAN bind):** `http://<pc-lan-ip>:17890`

In Android app settings, enter the base URL only (no trailing endpoint path). The client appends `/api/remote/v1/*`.

### 3) Token rotation steps (Android lane)

1. Generate a new token (password manager or secure generator).
2. Update House Victoria setting `RemoteCompanionApiToken`.
3. Restart House Victoria.
4. Update Android app token field.
5. Re-test:
   - `GET /api/remote/v1/health` (should be 200)
   - `POST /api/remote/v1/chat` with new token (should be 200)
   - old token should return 401 on chat

### 4) Troubleshooting quick table

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `401 unauthorized` on `/chat` | Wrong token, stale token after rotation, missing `Authorization: Bearer ...` | Re-enter token on Android, verify app restarted after token change, test with curl using same token |
| `Connection refused` | House Victoria not running, remote companion disabled, wrong port, listener bound to loopback while using LAN IP | Start app, set `RemoteCompanionEnabled=true`, verify `RemoteCompanionListenPort`, prefer tunnel URL when loopback-only |
| Health works locally but fails on phone network | Tunnel/mesh down, Access policy blocking, DNS/hostname mismatch | Verify tunnel status, test hostname from another network, check policy and route rules |
| Works on Wi-Fi but fails on cellular | Only LAN path configured, no tunnel/mesh path | Use Tailscale or Cloudflare Tunnel endpoint for off-LAN testing |

---

## Failure modes (user-visible expectations)

| Condition | What happens | User-visible expectation |
|-----------|----------------|---------------------------|
| PC **sleep** / **hibernate** | Listener and tunnel origin **stop** | Remote shows **companion unreachable** until PC wakes |
| House Victoria **not running** | Nothing on `127.0.0.1:<port>` | Same — **unreachable** |
| **UE** crash / hung | Avatar/world **stops**; remote **text/audio** may still work if app + LLM stack healthy | Spoken intent may **not** drive Unreal until UE restarts; document as **partial outage** |
| Remote companion **disabled** or **token &lt; 16 chars** | API **not started** (see startup log) | **Unreachable** until settings fixed and app restarted |
| Tunnel / Tailscale **down** | Phone cannot reach PC | **Unreachable** — fix mesh/tunnel first |

---

## References in repo

- API host: `HouseVictoria.App/RemoteCompanion/RemoteCompanionWebHost.cs`
- Config fields: `HouseVictoria.Core/Models/PersistenceModels.cs` (`RemoteCompanion*`)

*Document version: 1.0 · OPS maintenance: keep aligned with Settings names and default port.*
