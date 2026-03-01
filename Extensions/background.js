const DEFAULT_AGENT_URL = "http://127.0.0.1:43121/events/web";
const DEFAULT_ALLOWED_DOMAINS = ["youtube.com", "vimeo.com", "teams.microsoft.com"];
const DEFAULT_MIN_INTERVAL_MS = 2500;
const DEFAULT_HEARTBEAT_MS = 15000;

let lastSentAt = 0;
let lastSignature = "";
const tabMediaState = new Map();
const BROWSER = detectBrowser();

chrome.runtime.onInstalled.addListener(async () => {
  const existing = await chrome.storage.local.get([
    "agentUrl",
    "agentToken",
    "trackingConsentGranted",
    "allowedVideoDomains",
    "minEventIntervalMs",
    "heartbeatMs"
  ]);

  const patch = {};
  if (!existing.agentUrl) patch.agentUrl = DEFAULT_AGENT_URL;
  if (!Array.isArray(existing.allowedVideoDomains)) patch.allowedVideoDomains = DEFAULT_ALLOWED_DOMAINS;
  if (typeof existing.trackingConsentGranted !== "boolean") patch.trackingConsentGranted = false;
  if (!existing.minEventIntervalMs) patch.minEventIntervalMs = DEFAULT_MIN_INTERVAL_MS;
  if (!existing.heartbeatMs) patch.heartbeatMs = DEFAULT_HEARTBEAT_MS;

  if (Object.keys(patch).length > 0) {
    await chrome.storage.local.set(patch);
  }
});

function detectBrowser() {
  const ua = navigator.userAgent;
  if (ua.includes("Edg/")) return "edge";
  if (ua.includes("OPR/")) return "opera";
  if (ua.includes("Brave/")) return "brave";
  if (ua.includes("Vivaldi/")) return "vivaldi";
  if (ua.includes("Chromium/")) return "chromium";
  if (ua.includes("Chrome/")) return "chrome";
  return "chromium";
}

function domainFromUrl(url) {
  try {
    return new URL(url).hostname;
  } catch {
    return null;
  }
}

function isDomainAllowed(domain, allowedDomains) {
  if (!domain || !Array.isArray(allowedDomains) || allowedDomains.length === 0) {
    return false;
  }

  const normalized = domain.toLowerCase();
  return allowedDomains.some((entry) => {
    const suffix = String(entry || "").toLowerCase().trim();
    if (!suffix) return false;
    return normalized === suffix || normalized.endsWith(`.${suffix}`);
  });
}

function shouldTrackTab(tab, mediaState) {
  if (!tab) return false;
  if (tab.active) return true;
  return mediaState?.pipActive === true;
}

async function loadRuntimeConfig() {
  const data = await chrome.storage.local.get([
    "agentUrl",
    "agentToken",
    "trackingConsentGranted",
    "allowedVideoDomains",
    "minEventIntervalMs"
  ]);

  return {
    agentUrl: data.agentUrl || DEFAULT_AGENT_URL,
    agentToken: data.agentToken || "",
    trackingConsentGranted: data.trackingConsentGranted === true,
    allowedVideoDomains: Array.isArray(data.allowedVideoDomains)
      ? data.allowedVideoDomains
      : DEFAULT_ALLOWED_DOMAINS,
    minEventIntervalMs: Number.isFinite(data.minEventIntervalMs)
      ? Math.max(1000, data.minEventIntervalMs)
      : DEFAULT_MIN_INTERVAL_MS
  };
}

async function sendWebEvent(tab, options = {}) {
  if (!tab || !tab.id) return;

  const cfg = await loadRuntimeConfig();
  if (!cfg.trackingConsentGranted || !cfg.agentToken) {
    return;
  }

  const mediaState = tabMediaState.get(tab.id);
  if (!shouldTrackTab(tab, mediaState)) return;

  const eventUrl = mediaState?.videoUrl || tab.url;
  if (!eventUrl) return;

  const domain = mediaState?.videoDomain || domainFromUrl(eventUrl);
  if (!isDomainAllowed(domain, cfg.allowedVideoDomains)) return;

  const title = mediaState?.title || tab.title || null;
  const pipActive = mediaState?.pipActive === true;
  const isVideoPlaying = mediaState?.isVideoPlaying === true;

  const signature = `${tab.id}::${domain}::${eventUrl}::${title || ""}::${pipActive}::${isVideoPlaying}`;
  const now = Date.now();
  if (!options.force && signature === lastSignature) return;
  if (!options.force && now - lastSentAt < cfg.minEventIntervalMs) return;

  const payload = {
    eventId: crypto.randomUUID(),
    domain,
    title,
    url: tab.url || null,
    timestamp: new Date().toISOString(),
    browser: BROWSER,
    pipActive,
    videoPlaying: isVideoPlaying,
    videoUrl: mediaState?.videoUrl || null,
    videoDomain: mediaState?.videoDomain || null,
    tabId: tab.id
  };

  try {
    const res = await fetch(cfg.agentUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Agent-Token": cfg.agentToken
      },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      console.error("Agent send failed:", res.status, res.statusText);
      return;
    }

    lastSentAt = now;
    lastSignature = signature;
  } catch (error) {
    console.error("Agent fetch error:", error);
  }
}

chrome.runtime.onMessage.addListener(async (message, sender) => {
  if (!message || message.type !== "pip_state") {
    return;
  }

  const tabId = sender?.tab?.id;
  if (!tabId) {
    return;
  }

  const cfg = await loadRuntimeConfig();
  if (!cfg.trackingConsentGranted) {
    return;
  }

  const sourceUrl = message.videoUrl || sender.tab?.url || "";
  const sourceDomain = message.videoDomain || domainFromUrl(sourceUrl);
  if (!isDomainAllowed(sourceDomain, cfg.allowedVideoDomains)) {
    return;
  }

  const state = {
    pipActive: message.pipActive === true,
    isVideoPlaying: message.isVideoPlaying === true,
    videoUrl: sourceUrl || null,
    videoDomain: sourceDomain || null,
    title: message.title || sender.tab?.title || null,
    timestamp: message.timestamp || new Date().toISOString()
  };

  tabMediaState.set(tabId, state);
  sendWebEvent(sender.tab, { force: true });
});

chrome.tabs.onRemoved.addListener((tabId) => {
  tabMediaState.delete(tabId);
});

chrome.tabs.onActivated.addListener(async ({ tabId }) => {
  const tab = await chrome.tabs.get(tabId);
  sendWebEvent(tab);
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (!tab) return;
  if (changeInfo.status === "complete" || changeInfo.title || changeInfo.url) {
    sendWebEvent(tab, { force: Boolean(changeInfo.url) });
  }
});

chrome.runtime.onStartup.addListener(async () => {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  sendWebEvent(tab, { force: true });
});

setInterval(async () => {
  const { heartbeatMs } = await chrome.storage.local.get(["heartbeatMs"]);
  const interval = Number.isFinite(heartbeatMs) ? Math.max(5000, heartbeatMs) : DEFAULT_HEARTBEAT_MS;
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  sendWebEvent(tab);
  if (interval !== DEFAULT_HEARTBEAT_MS) {
    // no-op, interval setting is picked up on next service worker restart
  }
}, DEFAULT_HEARTBEAT_MS);
