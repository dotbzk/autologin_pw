const API_BASE = "http://127.0.0.1:43827/pwhub";

const statusElement = document.getElementById("status");
const pairingSection = document.getElementById("pairing");
const connectedSection = document.getElementById("connected");
const pairingCode = document.getElementById("pairingCode");
const connectButton = document.getElementById("connectButton");
const importButton = document.getElementById("importButton");
const refreshButton = document.getElementById("refreshButton");
const disconnectButton = document.getElementById("disconnectButton");
const accountName = document.getElementById("accountName");

let selectedAccountId = "";

function setStatus(message, kind = "") {
  statusElement.textContent = message;
  statusElement.className = `status ${kind}`.trim();
}

async function getStoredToken() {
  const values = await chrome.storage.local.get("pwHubToken");
  return values.pwHubToken || "";
}

async function request(path, options = {}) {
  const token = options.token ?? await getStoredToken();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), options.timeout ?? 5000);
  try {
    const response = await fetch(`${API_BASE}${path}`, {
      method: options.method || "GET",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { "X-PW-Hub-Token": token } : {})
      },
      body: options.body ? JSON.stringify(options.body) : undefined,
      cache: "no-store",
      signal: controller.signal
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || `PW Hub returned HTTP ${response.status}.`);
    }
    return payload;
  } finally {
    clearTimeout(timeout);
  }
}

async function showPairing(message) {
  selectedAccountId = "";
  pairingSection.hidden = false;
  connectedSection.hidden = true;
  setStatus(message || "Select an account in PW Hub, then use Connect browser extension from its tray menu.");
}

async function refreshStatus() {
  const token = await getStoredToken();
  if (!token) {
    await showPairing();
    return;
  }
  try {
    const status = await request("/status", { token });
    pairingSection.hidden = true;
    connectedSection.hidden = false;
    accountName.textContent = status.accountName || "No account selected";
    selectedAccountId = status.accountId || "";
    importButton.disabled = !status.ready;
    setStatus(status.message || "Connected to PW Hub.", status.ready ? "success" : "");
  } catch (error) {
    if (String(error.message).includes("not connected")) {
      await chrome.storage.local.remove("pwHubToken");
    }
    await showPairing(`Cannot connect to PW Hub: ${error.message}`);
  }
}

function getCookies(details) {
  return new Promise((resolve, reject) => {
    chrome.cookies.getAll(details, (cookies) => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
      } else {
        resolve(cookies);
      }
    });
  });
}

function mapSameSite(value) {
  if (value === "lax") return 1;
  if (value === "strict") return 2;
  return 0;
}

function toPwHubCookie(cookie) {
  return {
    name: cookie.name,
    value: cookie.value,
    domain: cookie.domain,
    path: cookie.path || "/",
    expires: cookie.expirationDate
      ? new Date(cookie.expirationDate * 1000).toISOString()
      : "0001-01-01T00:00:00Z",
    isSession: Boolean(cookie.session),
    isHttpOnly: Boolean(cookie.httpOnly),
    isSecure: Boolean(cookie.secure),
    sameSite: mapSameSite(cookie.sameSite)
  };
}

async function collectPwCookies() {
  const groups = await Promise.all([
    getCookies({ domain: "pwonline.ru" }),
    getCookies({ domain: "pw.mail.ru" })
  ]);
  const unique = new Map();
  for (const cookie of groups.flat()) {
    const normalizedDomain = cookie.domain.replace(/^\./, "").toLowerCase();
    const isAllowed = normalizedDomain === "pwonline.ru"
      || normalizedDomain.endsWith(".pwonline.ru")
      || normalizedDomain === "pw.mail.ru"
      || normalizedDomain.endsWith(".pw.mail.ru");
    if (!isAllowed) continue;
    unique.set(`${cookie.name}\n${cookie.domain}\n${cookie.path}`, toPwHubCookie(cookie));
  }
  return [...unique.values()];
}

connectButton.addEventListener("click", async () => {
  const code = pairingCode.value.trim();
  if (!/^\d{6}$/.test(code)) {
    setStatus("Enter the six-digit pairing code shown by PW Hub.", "error");
    return;
  }
  connectButton.disabled = true;
  try {
    const result = await request("/pair", {
      method: "POST",
      token: "",
      body: { code }
    });
    await chrome.storage.local.set({ pwHubToken: result.token });
    pairingCode.value = "";
    await refreshStatus();
  } catch (error) {
    setStatus(`Connection failed: ${error.message}`, "error");
  } finally {
    connectButton.disabled = false;
  }
});

importButton.addEventListener("click", async () => {
  importButton.disabled = true;
  setStatus("Reading the current PW Online session…");
  try {
    const cookies = await collectPwCookies();
    if (!cookies.length) {
      throw new Error("No PW Online cookies were found. Open the PW account page through Game Center first.");
    }
    const result = await request("/import", {
      method: "POST",
      body: { accountId: selectedAccountId, cookies },
      timeout: 45000
    });
    accountName.textContent = result.accountName;
    setStatus(`Session imported into ${result.accountName}.`, "success");
  } catch (error) {
    setStatus(`Import failed: ${error.message}`, "error");
  } finally {
    importButton.disabled = false;
  }
});

refreshButton.addEventListener("click", refreshStatus);

disconnectButton.addEventListener("click", async () => {
  await chrome.storage.local.remove("pwHubToken");
  await showPairing("The extension has been disconnected from PW Hub.");
});

refreshStatus();
