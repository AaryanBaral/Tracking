const AGENT_URL = "http://127.0.0.1:43121/events/web";
const TOKEN = "dev-token-123"; // match your appsettings.json
const MIN_INTERVAL_MS = 1500;
let lastSentAt = 0;
let lastSignature = "";
const BROWSER = detectBrowser();

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

async function sendWebEvent(tab) {
  if (!tab || !tab.url) return;
  if (tab.active === false) return;

  const domain = domainFromUrl(tab.url);
  if (!domain) return;

  const title = tab.title || null;
  const signature = `${domain}::${tab.url}::${title || ""}`;
  const now = Date.now();
  if (signature === lastSignature) return;
  if (now - lastSentAt < MIN_INTERVAL_MS) return;

  const payload = {
    eventId: crypto.randomUUID(),
    domain,
    title,
    url: tab.url,
    timestamp: new Date().toISOString(),
    browser: BROWSER
  };

  try {
    const res = await fetch(AGENT_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Agent-Token": TOKEN
      },
      body: JSON.stringify(payload)
    });
    if (!res.ok) {
       console.error("Agent send failed:", res.status, res.statusText);
       return;
    }
    lastSentAt = now;
    lastSignature = signature;
    console.log("Successfully sent web event", payload);
  } catch (e) {
    console.error("Agent fetch error:", e);
  }
}

// When active tab changes
chrome.tabs.onActivated.addListener(async ({ tabId }) => {
  const tab = await chrome.tabs.get(tabId);
  sendWebEvent(tab);
});

// When tab URL/title updates
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (!tab.active) return;
  if (changeInfo.status === "complete" || changeInfo.title || changeInfo.url) {
    sendWebEvent(tab);
  }
});

// On browser startup, capture current active tab
chrome.runtime.onStartup.addListener(async () => {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  sendWebEvent(tab);
});
