const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("siteProfiles", {
  listProfiles: () => ipcRenderer.invoke("profiles:list"),
  createProfile: (input) => ipcRenderer.invoke("profiles:create", input),
  updateProfile: (profileId, input) => ipcRenderer.invoke("profiles:update", profileId, input),
  deleteProfile: (profileId) => ipcRenderer.invoke("profiles:delete", profileId),
  clearProfileData: (profileId) => ipcRenderer.invoke("profiles:clearData", profileId),
  updateSettings: (input) => ipcRenderer.invoke("settings:update", input),
  openProfile: (profileId, bounds) => ipcRenderer.invoke("browser:openProfile", profileId, bounds),
  setBrowserBounds: (bounds) => ipcRenderer.invoke("browser:setBounds", bounds),
  navigate: (url) => ipcRenderer.invoke("browser:navigate", url),
  goBack: () => ipcRenderer.invoke("browser:goBack"),
  goForward: () => ipcRenderer.invoke("browser:goForward"),
  reload: () => ipcRenderer.invoke("browser:reload"),
  openExternal: () => ipcRenderer.invoke("browser:openExternal"),
  closeBrowser: () => ipcRenderer.invoke("browser:close"),
  confirm: (options) => ipcRenderer.invoke("app:confirm", options),
  onBrowserState: (callback) => {
    const listener = (_event, state) => callback(state);
    ipcRenderer.on("browser:state", listener);
    return () => ipcRenderer.removeListener("browser:state", listener);
  }
});
