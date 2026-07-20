const api = window.siteProfiles;

let appState = null;
let activeProfile = null;
let unsubscribeBrowserState = null;

const elements = {
  profilesView: document.getElementById("profiles-view"),
  browserView: document.getElementById("browser-view"),
  searchInput: document.getElementById("search-input"),
  profilesList: document.getElementById("profiles-list"),
  emptyState: document.getElementById("empty-state"),
  profileCount: document.getElementById("profile-count"),
  newProfileButton: document.getElementById("new-profile-button"),
  defaultUrlInput: document.getElementById("default-url-input"),
  launchModeSelect: document.getElementById("launch-mode-select"),
  saveSettingsButton: document.getElementById("save-settings-button"),
  profileDialog: document.getElementById("profile-dialog"),
  profileForm: document.getElementById("profile-form"),
  dialogTitle: document.getElementById("dialog-title"),
  profileIdInput: document.getElementById("profile-id-input"),
  profileNameInput: document.getElementById("profile-name-input"),
  profileUrlInput: document.getElementById("profile-url-input"),
  profileColorInput: document.getElementById("profile-color-input"),
  profileNoteInput: document.getElementById("profile-note-input"),
  cancelDialogButton: document.getElementById("cancel-dialog-button"),
  backButton: document.getElementById("back-button"),
  forwardButton: document.getElementById("forward-button"),
  reloadButton: document.getElementById("reload-button"),
  addressInput: document.getElementById("address-input"),
  goButton: document.getElementById("go-button"),
  externalButton: document.getElementById("external-button"),
  profilesButton: document.getElementById("profiles-button"),
  activeProfileName: document.getElementById("active-profile-name"),
  browserTitle: document.getElementById("browser-title"),
  browserFrame: document.getElementById("browser-frame")
};

function formatDate(value) {
  if (!value) {
    return "Never opened";
  }
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function normalizeUrl(value) {
  const raw = String(value || "").trim();
  if (!raw) {
    return "";
  }
  if (/^[a-zA-Z][a-zA-Z\d+\-.]*:\/\//.test(raw)) {
    return raw;
  }
  return `https://${raw}`;
}

function profileMatchesSearch(profile, search) {
  if (!search) {
    return true;
  }
  const haystack = `${profile.name} ${profile.url} ${profile.note}`.toLowerCase();
  return haystack.includes(search.toLowerCase());
}

function currentBrowserBounds() {
  const rect = elements.browserFrame.getBoundingClientRect();
  return {
    x: rect.x,
    y: rect.y,
    width: rect.width,
    height: rect.height
  };
}

async function syncBrowserBounds() {
  if (elements.browserView.classList.contains("hidden")) {
    return;
  }
  await api.setBrowserBounds(currentBrowserBounds());
}

function renderProfiles() {
  const search = elements.searchInput.value.trim();
  const profiles = appState.profiles.filter((profile) => profileMatchesSearch(profile, search));
  elements.profilesList.replaceChildren();
  elements.profileCount.textContent = `${profiles.length} of ${appState.profiles.length} profiles`;
  elements.emptyState.classList.toggle("hidden", profiles.length > 0);

  for (const profile of profiles) {
    const card = document.createElement("article");
    card.className = "profile-card";

    const color = document.createElement("span");
    color.className = "profile-color";
    color.style.backgroundColor = profile.color;

    const details = document.createElement("div");
    details.className = "profile-details";

    const name = document.createElement("h3");
    name.textContent = profile.name;

    const url = document.createElement("p");
    url.className = "profile-url";
    url.textContent = profile.url;

    const meta = document.createElement("p");
    meta.className = "muted";
    meta.textContent = `Last opened: ${formatDate(profile.lastOpenedAt)}`;

    const note = document.createElement("p");
    note.className = "profile-note";
    note.textContent = profile.note || "No note";

    details.append(name, url, meta, note);

    const actions = document.createElement("div");
    actions.className = "profile-actions";

    const openButton = document.createElement("button");
    openButton.className = "primary";
    openButton.type = "button";
    openButton.textContent = "Open";
    openButton.addEventListener("click", () => openProfile(profile.id));

    const editButton = document.createElement("button");
    editButton.type = "button";
    editButton.textContent = "Edit";
    editButton.addEventListener("click", () => showProfileDialog(profile));

    const clearButton = document.createElement("button");
    clearButton.type = "button";
    clearButton.textContent = "Clear Data";
    clearButton.addEventListener("click", () => clearProfileData(profile));

    const deleteButton = document.createElement("button");
    deleteButton.type = "button";
    deleteButton.className = "danger";
    deleteButton.textContent = "Delete";
    deleteButton.addEventListener("click", () => deleteProfile(profile));

    actions.append(openButton, editButton, clearButton, deleteButton);
    card.append(color, details, actions);
    elements.profilesList.append(card);
  }
}

function renderSettings() {
  elements.defaultUrlInput.value = appState.settings.defaultUrl;
  elements.launchModeSelect.value = appState.settings.launchMode;
}

async function refreshState() {
  appState = await api.listProfiles();
  renderSettings();
  renderProfiles();
}

function showProfileDialog(profile = null) {
  elements.dialogTitle.textContent = profile ? "Edit Profile" : "New Profile";
  elements.profileIdInput.value = profile ? profile.id : "";
  elements.profileNameInput.value = profile ? profile.name : "";
  elements.profileUrlInput.value = profile ? profile.url : appState.settings.defaultUrl;
  elements.profileColorInput.value = profile ? profile.color : "#3b82f6";
  elements.profileNoteInput.value = profile ? profile.note : "";
  elements.profileDialog.showModal();
  elements.profileNameInput.focus();
}

function closeProfileDialog() {
  elements.profileDialog.close();
}

async function saveProfile(event) {
  event.preventDefault();
  const input = {
    name: elements.profileNameInput.value,
    url: elements.profileUrlInput.value,
    color: elements.profileColorInput.value,
    note: elements.profileNoteInput.value
  };
  const profileId = elements.profileIdInput.value;

  if (profileId) {
    appState = await api.updateProfile(profileId, input);
  } else {
    appState = await api.createProfile(input);
  }

  closeProfileDialog();
  renderSettings();
  renderProfiles();
}

async function deleteProfile(profile) {
  const response = await api.confirm({
    title: "Delete profile",
    message: `Delete "${profile.name}"?`,
    detail: "This will remove the profile and delete its local browser data, including cookies and saved session data.",
    buttons: ["Cancel", "Delete"]
  });
  if (response !== 1) {
    return;
  }

  appState = await api.deleteProfile(profile.id);
  renderProfiles();
}

async function clearProfileData(profile) {
  const response = await api.confirm({
    title: "Clear profile data",
    message: `Clear browser data for "${profile.name}"?`,
    detail: "Cookies, cache, local storage, IndexedDB, and service worker data for this profile will be cleared.",
    buttons: ["Cancel", "Clear Data"]
  });
  if (response !== 1) {
    return;
  }

  appState = await api.clearProfileData(profile.id);
  renderProfiles();
}

async function saveSettings() {
  appState = await api.updateSettings({
    defaultUrl: elements.defaultUrlInput.value,
    launchMode: elements.launchModeSelect.value
  });
  renderSettings();
}

function showBrowserShell(profile) {
  activeProfile = profile;
  elements.profilesView.classList.add("hidden");
  elements.browserView.classList.remove("hidden");
  elements.activeProfileName.textContent = profile.name;
}

function showProfilesShell() {
  activeProfile = null;
  elements.browserView.classList.add("hidden");
  elements.profilesView.classList.remove("hidden");
}

async function openProfile(profileId) {
  const profile = appState.profiles.find((item) => item.id === profileId);
  if (!profile) {
    return;
  }

  showBrowserShell(profile);
  await new Promise((resolve) => requestAnimationFrame(resolve));
  await api.openProfile(profile.id, currentBrowserBounds());
  await refreshState();
}

async function closeBrowser() {
  await api.closeBrowser();
  showProfilesShell();
  await refreshState();
}

function applyBrowserState(state) {
  elements.addressInput.value = state.url || "";
  elements.browserTitle.textContent = state.title || "";
  elements.backButton.disabled = !state.canGoBack;
  elements.forwardButton.disabled = !state.canGoForward;
  elements.reloadButton.disabled = false;

  if (state.activeProfileId && appState) {
    const profile = appState.profiles.find((item) => item.id === state.activeProfileId);
    if (profile) {
      elements.activeProfileName.textContent = profile.name;
    }
  }
}

async function navigateFromAddress() {
  const url = normalizeUrl(elements.addressInput.value);
  if (url) {
    await api.navigate(url);
  }
}

function bindEvents() {
  elements.newProfileButton.addEventListener("click", () => showProfileDialog());
  elements.cancelDialogButton.addEventListener("click", closeProfileDialog);
  elements.profileForm.addEventListener("submit", saveProfile);
  elements.searchInput.addEventListener("input", renderProfiles);
  elements.saveSettingsButton.addEventListener("click", saveSettings);

  elements.backButton.addEventListener("click", () => api.goBack());
  elements.forwardButton.addEventListener("click", () => api.goForward());
  elements.reloadButton.addEventListener("click", () => api.reload());
  elements.goButton.addEventListener("click", navigateFromAddress);
  elements.externalButton.addEventListener("click", () => api.openExternal());
  elements.profilesButton.addEventListener("click", closeBrowser);
  elements.addressInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      navigateFromAddress();
    }
  });

  window.addEventListener("resize", () => {
    syncBrowserBounds();
  });

  unsubscribeBrowserState = api.onBrowserState(applyBrowserState);
}

async function boot() {
  bindEvents();
  await refreshState();

  if (appState.settings.launchMode === "last" && appState.lastProfileId) {
    const lastProfile = appState.profiles.find((profile) => profile.id === appState.lastProfileId);
    if (lastProfile) {
      await openProfile(lastProfile.id);
    }
  }
}

window.addEventListener("beforeunload", () => {
  if (unsubscribeBrowserState) {
    unsubscribeBrowserState();
  }
});

boot().catch((error) => {
  console.error(error);
  document.body.textContent = `Application error: ${error.message}`;
});
