# COVAS: Next + House Victoria (Elite Dangerous Ship Computer)

Use House Victoria’s AI as your ship computer and second-in-command in **Elite Dangerous** via **COVAS: Next**.

## What you need

- **House Victoria** (this app) with Ollama and at least one AI contact configured.
- **COVAS: Next** (Elite Dangerous AI Integration) installed.  
  [Download](https://github.com/RatherRude/Elite-Dangerous-AI-Integration/releases) | [Docs](https://ratherrude.github.io/Elite-Dangerous-AI-Integration/)

House Victoria exposes an **OpenAI-compatible API** that COVAS: Next can call. Your chosen AI contact (persona) in House Victoria becomes the voice and brain of the ship computer.

---

## 1. Create your ship computer AI in House Victoria

1. Open **House Victoria** → **AI Models & Personas** (or SMS/MMS and use an AI contact).
2. Create or pick an **AI contact** to act as your ship computer / second-in-command.
3. Set **Model** (e.g. an Ollama model like `llama3.2` or `mistral`).
4. Set **System prompt** so the AI behaves as your ship computer (e.g. role, tone, callsign, how to address you).
5. Optionally set **Primary AI** so this contact is used when no specific COVAS contact is set.
6. Note the contact’s **ID** if you want to pin COVAS to this contact (see step 3).

---

## 2. Enable the COVAS bridge in House Victoria

Edit the config file that the app actually uses. That is either:
- **In your project:** `HouseVictoria.App\App.config` (then rebuild so the change is copied to the output), or  
- **Next to the running app:** the `.config` file beside `HouseVictoria.App.exe` in the build output folder (e.g. `bin\Debug\net8.0-windows\HouseVictoria.App.dll.config`).

Set:

```xml
<add key="CovasBridgeEnabled" value="true"/>
<add key="CovasBridgeEndpoint" value="http://localhost:11435"/>
<add key="CovasContactId" value=""/>
```

- **CovasBridgeEnabled** – `true` = turn on the bridge (required for COVAS).
- **CovasBridgeEndpoint** – URL the bridge listens on. Default `http://localhost:11435` is fine unless something else uses that port.
- **CovasContactId** – Leave empty to use your **Primary AI** (or first contact). To always use a specific ship-computer contact, set it to that contact’s ID (from step 1).

Restart House Victoria so the bridge starts. You can check it’s running by opening in a browser:

- `http://localhost:11435/health`  
  You should see something like: `{"status":"ok","service":"covas-bridge"}`

---

## 3. Point COVAS: Next at House Victoria

1. Start **COVAS: Next** and open its configuration.
2. **API / LLM** settings:
   - Set the **API base URL** to your bridge URL **without** `/v1` at the end, e.g.  
     `http://localhost:11435`  
     (COVAS will call `http://localhost:11435/v1/chat/completions` and `/v1/models`.)
   - **API key**: COVAS often requires an API key. House Victoria’s bridge does not validate the key; you can use any value, e.g. `house-victoria` or `ollama`.
3. If COVAS has a **model** field, use the same model name as in your House Victoria contact (e.g. `llama3.2`). The bridge reports your contact’s model to COVAS.
4. Save and start the AI assistant in COVAS.

After that, COVAS will send your voice and game context to House Victoria’s bridge, and your ship computer AI will reply using the contact you configured.

---

## 4. Optional: use Ollama directly (no House Victoria persona)

If you only want a **local model** without House Victoria’s personas or prompts:

- In COVAS, set base URL to **Ollama’s OpenAI-compatible API**:  
  `http://localhost:11434/v1`  
  and API key: `ollama` (or any).  
  No need to enable the House Victoria COVAS bridge.

To use **House Victoria’s ship computer persona** (system prompt, chosen model, memory, etc.), use the bridge as above.

---

## Troubleshooting

### “LLM Error: Connection error” (COVAS can’t reach the LLM)

This means COVAS could not open a connection to the API (House Victoria’s bridge or Ollama). Check in order:

1. **House Victoria is running**  
   Start House Victoria and leave it open while you use COVAS.

2. **Bridge is enabled and listening**  
   - In `HouseVictoria.App\App.config` set `CovasBridgeEnabled` to `true`.  
   - Restart House Victoria after changing config.  
   - In a browser open `http://localhost:11435/health`. You should see `{"status":"ok","service":"covas-bridge"}`. If the page doesn’t load, the bridge isn’t running (check config and restart).

3. **COVAS API base URL is correct**  
   - When using the House Victoria bridge, use **exactly**: `http://localhost:11435`  
   - Use `http` (not `https`).  
   - No trailing slash.  
   - Port must be **11435** (bridge), not 11434 (Ollama), unless you’re pointing COVAS directly at Ollama (see “Optional: use Ollama directly” above).

4. **Nothing else is using port 11435**  
   If another app uses that port, change it in App.config: set `CovasBridgeEndpoint` to e.g. `http://localhost:11436`, then in COVAS use that same URL.

5. **Firewall**  
   Allow House Victoria or local port 11435 so COVAS (and the browser) can connect to localhost.

---

- **Bridge not responding**  
  - Ensure `CovasBridgeEnabled` is `true` and you restarted House Victoria.  
  - Check `http://localhost:11435/health` in a browser.

- **“No AI contact configured”**  
  - Create at least one AI contact in House Victoria.  
  - If you set `CovasContactId`, make sure it matches the contact’s ID exactly (or leave it empty to use Primary/first contact).

- **COVAS can’t connect**  
  - Confirm COVAS base URL is `http://localhost:11435` (no trailing slash).  
  - Firewall: allow the House Victoria app or port 11435 if needed.

- **Slow or no replies**  
  - Ollama must be running and the model loaded.  
  - In House Victoria, check that the chosen AI contact’s Ollama endpoint and model are correct and reachable.

---

## Summary

| Step | Action |
|------|--------|
| 1 | Create/choose an AI contact in House Victoria as your ship computer. |
| 2 | Set `CovasBridgeEnabled` to `true` in App.config and restart House Victoria. |
| 3 | In COVAS: Next, set API base URL to `http://localhost:11435` and any API key. |
| 4 | Start the AI assistant in COVAS and talk to your ship. |

Your House Victoria AI contact is now your Elite Dangerous ship computer and second-in-command via COVAS: Next.
