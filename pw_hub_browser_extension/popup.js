const API_BASE = "http://127.0.0.1:43827/pwhub";

const statusElement = document.getElementById("status");
const pairingSection = document.getElementById("pairing");
const connectedSection = document.getElementById("connected");
const pairingCode = document.getElementById("pairingCode");
const connectButton = document.getElementById("connectButton");
const importButton = document.getElementById("importButton");
const exportButton = document.getElementById("exportButton");
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
        "X-PW-Hub-Extension": "pw-hub-session-import-v1",
        ...(token ? { "X-PW-Hub-Token": token } : {})
      },
      body: options.body ? JSON.stringify(options.body) : undefined,
      cache: "no-store",
      signal: controller.signal
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || `PW Hub вернул ошибку HTTP ${response.status}.`);
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
  setStatus(message || "Выберите аккаунт в PW Hub, затем нажмите «Подключить расширение браузера» в меню значка приложения.");
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
    accountName.textContent = status.accountName || "Аккаунт не выбран";
    selectedAccountId = status.accountId || "";
    importButton.disabled = !status.ready;
    exportButton.disabled = !status.ready;
    setStatus(status.message || "Подключено к PW Hub.", status.ready ? "success" : "");
  } catch (error) {
    if (String(error.message).includes("не подключено") || String(error.message).includes("not connected")) {
      await chrome.storage.local.remove("pwHubToken");
    }
    await showPairing(`Не удалось подключиться к PW Hub: ${error.message}`);
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

function setCookie(details) {
  return new Promise((resolve, reject) => {
    chrome.cookies.set(details, (cookie) => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
      } else if (!cookie) {
        reject(new Error("Браузер не подтвердил сохранение cookie."));
      } else {
        resolve(cookie);
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

function fromPwHubSameSite(value, isSecure) {
  if (value === 1) return "lax";
  if (value === 2) return "strict";
  return isSecure ? "no_restriction" : "unspecified";
}

function toBrowserCookie(cookie) {
  const sourceDomain = String(cookie.domain || "").trim();
  const host = sourceDomain.replace(/^\./, "").toLowerCase();
  if (!host) {
    throw new Error("PW Hub вернул cookie без домена.");
  }
  const path = String(cookie.path || "/").startsWith("/")
    ? String(cookie.path || "/")
    : `/${cookie.path}`;
  const details = {
    url: `https://${host}${path}`,
    name: String(cookie.name || ""),
    value: String(cookie.value ?? ""),
    path,
    secure: Boolean(cookie.isSecure),
    httpOnly: Boolean(cookie.isHttpOnly),
    sameSite: fromPwHubSameSite(cookie.sameSite, Boolean(cookie.isSecure))
  };
  if (sourceDomain.startsWith(".")) {
    details.domain = sourceDomain;
  }
  if (!cookie.isSession) {
    const expirationDate = Date.parse(cookie.expires) / 1000;
    if (Number.isFinite(expirationDate) && expirationDate > Date.now() / 1000) {
      details.expirationDate = expirationDate;
    }
  }
  return details;
}

async function installPwCookies(cookies) {
  let installed = 0;
  for (const cookie of cookies) {
    await setCookie(toBrowserCookie(cookie));
    installed += 1;
  }
  return installed;
}

connectButton.addEventListener("click", async () => {
  const code = pairingCode.value.trim();
  if (!/^\d{6}$/.test(code)) {
    setStatus("Введите шестизначный код подключения, показанный в PW Hub.", "error");
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
    setStatus(`Ошибка подключения: ${error.message}`, "error");
  } finally {
    connectButton.disabled = false;
  }
});

importButton.addEventListener("click", async () => {
  importButton.disabled = true;
  exportButton.disabled = true;
  setStatus("Чтение текущей сессии PW Online…");
  try {
    const cookies = await collectPwCookies();
    if (!cookies.length) {
      throw new Error("Cookies PW Online не найдены. Сначала откройте страницу аккаунта PW через Игровой центр.");
    }
    const result = await request("/import", {
      method: "POST",
      body: { accountId: selectedAccountId, cookies },
      timeout: 45000
    });
    accountName.textContent = result.accountName;
    setStatus(`Сессия импортирована в аккаунт ${result.accountName}.`, "success");
  } catch (error) {
    setStatus(`Ошибка импорта: ${error.message}`, "error");
  } finally {
    importButton.disabled = !selectedAccountId;
    exportButton.disabled = !selectedAccountId;
  }
});

exportButton.addEventListener("click", async () => {
  const confirmed = window.confirm(
    `Экспортировать сессию аккаунта «${accountName.textContent}» в этот профиль браузера? Совпадающие cookies PW Online будут заменены.`
  );
  if (!confirmed) return;
  exportButton.disabled = true;
  importButton.disabled = true;
  setStatus("Получение сессии из PW Hub…");
  try {
    const result = await request("/export", {
      method: "POST",
      body: { accountId: selectedAccountId },
      timeout: 45000
    });
    const cookies = Array.isArray(result.cookies) ? result.cookies : [];
    if (!cookies.length) {
      throw new Error("PW Hub не вернул cookies PW Online.");
    }
    const installed = await installPwCookies(cookies);
    accountName.textContent = result.accountName;
    setStatus(`Сессия аккаунта ${result.accountName} экспортирована (${installed} cookies).`, "success");
    await chrome.tabs.create({ url: "https://pwonline.ru/" });
  } catch (error) {
    setStatus(`Ошибка экспорта: ${error.message}`, "error");
  } finally {
    importButton.disabled = !selectedAccountId;
    exportButton.disabled = !selectedAccountId;
  }
});

refreshButton.addEventListener("click", refreshStatus);

disconnectButton.addEventListener("click", async () => {
  await chrome.storage.local.remove("pwHubToken");
  await showPairing("Расширение отключено от PW Hub.");
});

refreshStatus();
