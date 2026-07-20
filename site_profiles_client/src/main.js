const { app, BrowserView, BrowserWindow, dialog, ipcMain, session, shell } = require("electron");
const crypto = require("crypto");
const fs = require("fs/promises");
const path = require("path");

const DEFAULT_STATE = {
  settings: {
    defaultUrl: "https://example.com",
    launchMode: "profiles"
  },
  lastProfileId: null,
  profiles: []
};

let mainWindow = null;
let browserView = null;
let activeProfileId = null;
let activeBounds = null;
let statePath = null;

function nowIso() {
  return new Date().toISOString();
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function normalizeUrl(value, fallback = DEFAULT_STATE.settings.defaultUrl) {
  const raw = String(value || "").trim() || fallback;
  if (!raw) {
    return DEFAULT_STATE.settings.defaultUrl;
  }
  if (/^[a-zA-Z][a-zA-Z\d+\-.]*:\/\//.test(raw)) {
    return raw;
  }
  return `https://${raw}`;
}

function makeProfileId() {
  if (typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return crypto.randomBytes(16).toString("hex");
}

function partitionName(profileId) {
  return `persist:site-profile-${profileId}`;
}

function partitionStorageDir(profileId) {
  return path.join(app.getPath("userData"), "Partitions", `site-profile-${profileId}`);
}

async function ensureStatePath() {
  const dataDir = app.getPath("userData");
  await fs.mkdir(dataDir, { recursive: true });
  statePath = path.join(dataDir, "profiles.json");
}

function normalizeState(rawState) {
  const next = {
    settings: {
      ...DEFAULT_STATE.settings,
      ...(rawState && rawState.settings ? rawState.settings : {})
    },
    lastProfileId: rawState && rawState.lastProfileId ? rawState.lastProfileId : null,
    profiles: Array.isArray(rawState && rawState.profiles) ? rawState.profiles : []
  };

  next.settings.defaultUrl = normalizeUrl(next.settings.defaultUrl);
  next.settings.launchMode = next.settings.launchMode === "last" ? "last" : "profiles";
  next.profiles = next.profiles.map((profile) => ({
    id: String(profile.id || makeProfileId()),
    name: String(profile.name || "Untitled profile"),
    url: normalizeUrl(profile.url, next.settings.defaultUrl),
    note: String(profile.note || ""),
    color: String(profile.color || "#3b82f6"),
    createdAt: profile.createdAt || nowIso(),
    updatedAt: profile.updatedAt || nowIso(),
    lastOpenedAt: profile.lastOpenedAt || null
  }));

  if (!next.profiles.some((profile) => profile.id === next.lastProfileId)) {
    next.lastProfileId = null;
  }

  return next;
}

async function readState() {
  await ensureStatePath();
  try {
    const text = await fs.readFile(statePath, "utf8");
    return normalizeState(JSON.parse(text));
  } catch (error) {
    if (error.code === "ENOENT") {
      const initial = normalizeState(DEFAULT_STATE);
      await writeState(initial);
      return initial;
    }
    throw error;
  }
}

async function writeState(nextState) {
  await ensureStatePath();
  const normalized = normalizeState(nextState);
  await fs.writeFile(statePath, `${JSON.stringify(normalized, null, 2)}\n`, "utf8");
  return normalized;
}

async function updateState(mutator) {
  const current = await readState();
  const result = await mutator(current);
  return writeState(result || current);
}

function publicState(state) {
  return clone(state);
}

function findProfile(state, id) {
  return state.profiles.find((profile) => profile.id === id);
}

function sendBrowserState(extra = {}) {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return;
  }

  const webContents = browserView ? browserView.webContents : null;
  mainWindow.webContents.send("browser:state", {
    activeProfileId,
    url: webContents ? webContents.getURL() : "",
    title: webContents ? webContents.getTitle() : "",
    canGoBack: webContents ? webContents.canGoBack() : false,
    canGoForward: webContents ? webContents.canGoForward() : false,
    isLoading: webContents ? webContents.isLoading() : false,
    ...extra
  });
}

function applyBrowserBounds(bounds) {
  if (!browserView || !bounds) {
    return;
  }

  const safeBounds = {
    x: Math.max(0, Math.round(Number(bounds.x) || 0)),
    y: Math.max(0, Math.round(Number(bounds.y) || 0)),
    width: Math.max(100, Math.round(Number(bounds.width) || 100)),
    height: Math.max(100, Math.round(Number(bounds.height) || 100))
  };
  activeBounds = safeBounds;
  browserView.setBounds(safeBounds);
}

function destroyBrowserView() {
  if (!browserView) {
    activeProfileId = null;
    sendBrowserState();
    return;
  }

  const view = browserView;
  browserView = null;
  activeProfileId = null;
  activeBounds = null;

  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.removeBrowserView(view);
  }
  if (!view.webContents.isDestroyed()) {
    view.webContents.destroy();
  }
  sendBrowserState();
}

async function openProfile(profileId, bounds) {
  const state = await readState();
  const profile = findProfile(state, profileId);
  if (!profile) {
    throw new Error("Profile not found");
  }

  destroyBrowserView();

  activeProfileId = profile.id;
  browserView = new BrowserView({
    webPreferences: {
      partition: partitionName(profile.id),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });

  mainWindow.setBrowserView(browserView);
  applyBrowserBounds(bounds || activeBounds || { x: 0, y: 88, width: 1200, height: 700 });

  const webContents = browserView.webContents;
  webContents.setWindowOpenHandler(({ url }) => {
    webContents.loadURL(url);
    return { action: "deny" };
  });
  webContents.on("did-start-loading", () => sendBrowserState({ isLoading: true }));
  webContents.on("did-stop-loading", () => sendBrowserState({ isLoading: false }));
  webContents.on("did-navigate", () => sendBrowserState());
  webContents.on("did-navigate-in-page", () => sendBrowserState());
  webContents.on("page-title-updated", () => sendBrowserState());
  webContents.on("did-fail-load", (_event, errorCode, errorDescription, validatedUrl) => {
    if (errorCode !== -3) {
      sendBrowserState({ error: `${errorDescription}: ${validatedUrl}` });
    }
  });

  await updateState((nextState) => {
    const nextProfile = findProfile(nextState, profile.id);
    nextProfile.lastOpenedAt = nowIso();
    nextProfile.updatedAt = nowIso();
    nextState.lastProfileId = profile.id;
    return nextState;
  });

  await webContents.loadURL(normalizeUrl(profile.url, state.settings.defaultUrl));
  sendBrowserState();
  return profile;
}

async function clearProfileBrowserData(profileId, removeFiles = false) {
  const profileSession = session.fromPartition(partitionName(profileId));
  await profileSession.clearStorageData({
    storages: [
      "cookies",
      "filesystem",
      "indexdb",
      "localstorage",
      "shadercache",
      "websql",
      "serviceworkers",
      "cachestorage"
    ]
  });
  await profileSession.clearCache();

  if (removeFiles) {
    try {
      await fs.rm(partitionStorageDir(profileId), { recursive: true, force: true });
    } catch (error) {
      console.warn(`Cannot remove profile storage directory: ${error.message}`);
    }
  }
}

function createMainWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 960,
    minHeight: 640,
    title: "Site Profiles Client",
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });

  mainWindow.loadFile(path.join(__dirname, "renderer", "index.html"));
  mainWindow.on("closed", () => {
    destroyBrowserView();
    mainWindow = null;
  });
}

app.whenReady().then(async () => {
  await ensureStatePath();
  createMainWindow();

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createMainWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

ipcMain.handle("profiles:list", async () => publicState(await readState()));

ipcMain.handle("profiles:create", async (_event, input) => {
  const createdAt = nowIso();
  const saved = await updateState((state) => {
    const profile = {
      id: makeProfileId(),
      name: String(input.name || "").trim() || "Untitled profile",
      url: normalizeUrl(input.url, state.settings.defaultUrl),
      note: String(input.note || ""),
      color: String(input.color || "#3b82f6"),
      createdAt,
      updatedAt: createdAt,
      lastOpenedAt: null
    };
    state.profiles.push(profile);
    return state;
  });
  return publicState(saved);
});

ipcMain.handle("profiles:update", async (_event, profileId, input) => {
  const saved = await updateState((state) => {
    const profile = findProfile(state, profileId);
    if (!profile) {
      throw new Error("Profile not found");
    }
    profile.name = String(input.name || "").trim() || profile.name;
    profile.url = normalizeUrl(input.url, state.settings.defaultUrl);
    profile.note = String(input.note || "");
    profile.color = String(input.color || profile.color || "#3b82f6");
    profile.updatedAt = nowIso();
    return state;
  });
  return publicState(saved);
});

ipcMain.handle("profiles:delete", async (_event, profileId) => {
  if (activeProfileId === profileId) {
    destroyBrowserView();
  }

  await clearProfileBrowserData(profileId, true);
  const saved = await updateState((state) => {
    state.profiles = state.profiles.filter((profile) => profile.id !== profileId);
    if (state.lastProfileId === profileId) {
      state.lastProfileId = null;
    }
    return state;
  });
  return publicState(saved);
});

ipcMain.handle("profiles:clearData", async (_event, profileId) => {
  if (activeProfileId === profileId) {
    destroyBrowserView();
  }
  await clearProfileBrowserData(profileId, false);
  return publicState(await readState());
});

ipcMain.handle("settings:update", async (_event, input) => {
  const saved = await updateState((state) => {
    state.settings.defaultUrl = normalizeUrl(input.defaultUrl, DEFAULT_STATE.settings.defaultUrl);
    state.settings.launchMode = input.launchMode === "last" ? "last" : "profiles";
    return state;
  });
  return publicState(saved);
});

ipcMain.handle("browser:openProfile", async (_event, profileId, bounds) => {
  return openProfile(profileId, bounds);
});

ipcMain.handle("browser:setBounds", async (_event, bounds) => {
  applyBrowserBounds(bounds);
});

ipcMain.handle("browser:navigate", async (_event, url) => {
  if (!browserView) {
    return;
  }
  await browserView.webContents.loadURL(normalizeUrl(url));
});

ipcMain.handle("browser:goBack", async () => {
  if (browserView && browserView.webContents.canGoBack()) {
    browserView.webContents.goBack();
  }
});

ipcMain.handle("browser:goForward", async () => {
  if (browserView && browserView.webContents.canGoForward()) {
    browserView.webContents.goForward();
  }
});

ipcMain.handle("browser:reload", async () => {
  if (browserView) {
    browserView.webContents.reload();
  }
});

ipcMain.handle("browser:openExternal", async () => {
  if (browserView) {
    const url = browserView.webContents.getURL();
    if (url) {
      await shell.openExternal(url);
    }
  }
});

ipcMain.handle("browser:close", async () => {
  destroyBrowserView();
});

ipcMain.handle("app:confirm", async (_event, options) => {
  const result = await dialog.showMessageBox(mainWindow, {
    type: options.type || "warning",
    title: options.title || "Confirm",
    message: options.message || "Are you sure?",
    detail: options.detail || "",
    buttons: options.buttons || ["Cancel", "OK"],
    cancelId: 0,
    defaultId: 0,
    noLink: true
  });
  return result.response;
});
