const BRIDGE_URL = "http://127.0.0.1:17891";
const CAST_WS_URL = "ws://127.0.0.1:17891/ws/cast";
const POLL_MS = 400;
const STREAM_MIN_MS = 750;

let lastStreamPushMs = 0;
let cachedStreamEnabled = false;
let castWs = null;
let castWsConnecting = false;

function connectCastProducer() {
  if (castWs?.readyState === WebSocket.OPEN) return;
  if (castWsConnecting) return;
  castWsConnecting = true;

  try {
    castWs = new WebSocket(CAST_WS_URL);
    castWs.onopen = () => {
      castWsConnecting = false;
      castWs.send(JSON.stringify({ role: "producer" }));
    };
    castWs.onclose = () => {
      castWs = null;
      castWsConnecting = false;
    };
    castWs.onerror = () => {
      castWsConnecting = false;
    };
  } catch (_) {
    castWsConnecting = false;
  }
}

async function pollBridge() {
  try {
    const res = await fetch(`${BRIDGE_URL}/poll`, { cache: "no-store" });
    if (!res.ok) return;
    const job = await res.json();
    cachedStreamEnabled = !!job.stream_enabled;
    if (job.pending && job.job_id) {
      await runCaptureJob(job);
    } else if (cachedStreamEnabled) {
      connectCastProducer();
      await streamPushActiveTab();
    }
  } catch (_) {
    // Bridge not running — extension stays idle.
  }
}

async function isStreamEnabled() {
  try {
    const res = await fetch(`${BRIDGE_URL}/stream/status`, { cache: "no-store" });
    if (!res.ok) return false;
    const status = await res.json();
    cachedStreamEnabled = !!status.stream_enabled;
    return cachedStreamEnabled;
  } catch {
    return false;
  }
}

async function streamPushActiveTab() {
  const now = Date.now();
  if (now - lastStreamPushMs < STREAM_MIN_MS) return;

  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || tab.id == null || tab.windowId == null) return;
    if (!tab.url || tab.url.startsWith("chrome://") || tab.url.startsWith("edge://")) return;

    const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });
    const screenshotBase64 = dataUrl.replace(/^data:image\/png;base64,/, "");

    if (cachedStreamEnabled && castWs?.readyState === WebSocket.OPEN) {
      castWs.send(
        JSON.stringify({
          type: "frame",
          tab_id: tab.id,
          url: tab.url || "",
          title: tab.title || "",
          png: screenshotBase64,
        })
      );
      lastStreamPushMs = now;
      return;
    }

    const pushRes = await fetch(`${BRIDGE_URL}/stream`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        ok: true,
        tab_id: tab.id,
        url: tab.url || "",
        title: tab.title || "",
        screenshot_base64: screenshotBase64,
      }),
    });
    if (pushRes.ok) lastStreamPushMs = now;
  } catch (_) {
    // Capture denied or bridge offline — skip this tick.
  }
}

async function runCaptureJob(job) {
  const jobId = job.job_id;
  const includeScreenshot = job.include_screenshot !== false;
  const includePageMap = job.include_page_map !== false;

  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || tab.id == null) {
      await postResult(jobId, { ok: false, error: "no_active_tab" });
      return;
    }

    let screenshotBase64 = null;
    if (includeScreenshot) {
      const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });
      screenshotBase64 = dataUrl.replace(/^data:image\/png;base64,/, "");
    }

    let pageMap = null;
    if (includePageMap) {
      const [{ result }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: buildPageMapInPage,
      });
      pageMap = result;
    }

    await postResult(jobId, {
      ok: true,
      tab_id: tab.id,
      window_id: tab.windowId,
      url: tab.url || "",
      title: tab.title || "",
      screenshot_base64: screenshotBase64,
      page_map: pageMap,
    });
  } catch (err) {
    await postResult(jobId, { ok: false, error: String(err?.message || err) });
  }
}

async function postResult(jobId, payload) {
  await fetch(`${BRIDGE_URL}/result`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ job_id: jobId, ...payload }),
  });
}

async function checkBridgeHealth() {
  try {
    const res = await fetch(`${BRIDGE_URL}/health`, { cache: "no-store" });
    return res.ok;
  } catch {
    return false;
  }
}

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg?.type === "hv_health") {
    checkBridgeHealth().then((ok) => sendResponse({ bridge_ok: ok }));
    return true;
  }

  if (msg?.type === "hv_keepalive") {
    (async () => {
      if (!(await isStreamEnabled())) return;
      const tabId = sender.tab?.id;
      if (tabId == null) return;
      const [active] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!active || active.id !== tabId) return;
      connectCastProducer();
      await streamPushActiveTab();
    })();
    return false;
  }

  return false;
});

chrome.alarms.create("hv-poll", { periodInMinutes: 1 });
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === "hv-poll") pollBridge();
});

setInterval(pollBridge, POLL_MS);
pollBridge();

/** Runs inside the page context — must stay self-contained (no outer closures). */
function buildPageMapInPage() {
  const INTERACTIVE =
    "a[href],button,input,textarea,select,[role='button'],[role='link'],[role='textbox'],[contenteditable='true'],[onclick]";
  const viewport = {
    width: window.innerWidth,
    height: window.innerHeight,
    scrollX: window.scrollX,
    scrollY: window.scrollY,
  };
  const elements = [];
  const seen = new Set();

  function shortText(el) {
    const t = (el.innerText || el.value || el.getAttribute("aria-label") || el.title || "").trim();
    return t.length > 120 ? t.slice(0, 117) + "..." : t;
  }

  function cssPath(el) {
    if (el.id) return `#${CSS.escape(el.id)}`;
    const parts = [];
    let node = el;
    while (node && node.nodeType === 1 && parts.length < 6) {
      let part = node.tagName.toLowerCase();
      if (node.id) {
        part += `#${CSS.escape(node.id)}`;
        parts.unshift(part);
        break;
      }
      const parent = node.parentElement;
      if (parent) {
        const siblings = Array.from(parent.children).filter((c) => c.tagName === node.tagName);
        if (siblings.length > 1) {
          part += `:nth-of-type(${siblings.indexOf(node) + 1})`;
        }
      }
      parts.unshift(part);
      node = parent;
    }
    return parts.join(" > ");
  }

  document.querySelectorAll(INTERACTIVE).forEach((el, index) => {
    if (!(el instanceof HTMLElement)) return;
    const style = window.getComputedStyle(el);
    if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return;
    const rect = el.getBoundingClientRect();
    if (rect.width < 2 || rect.height < 2) return;
    if (rect.bottom < 0 || rect.right < 0 || rect.top > viewport.height || rect.left > viewport.width) return;
    const key = `${el.tagName}|${rect.x}|${rect.y}|${shortText(el)}`;
    if (seen.has(key)) return;
    seen.add(key);
    elements.push({
      index,
      tag: el.tagName.toLowerCase(),
      text: shortText(el),
      id: el.id || null,
      name: el.getAttribute("name"),
      type: el.getAttribute("type"),
      href: el.tagName === "A" ? el.getAttribute("href") : null,
      role: el.getAttribute("role"),
      selector: cssPath(el),
      bounds: {
        x: Math.round(rect.x),
        y: Math.round(rect.y),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
      },
      center: {
        x: Math.round(rect.x + rect.width / 2),
        y: Math.round(rect.y + rect.height / 2),
      },
    });
  });

  return {
    url: location.href,
    title: document.title,
    viewport,
    elementCount: elements.length,
    elements: elements.slice(0, 200),
  };
}
