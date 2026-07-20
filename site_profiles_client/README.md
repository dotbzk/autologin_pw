# Site Profiles Client

Desktop Windows client for managing multiple isolated website account profiles.

Each profile opens the same or different site inside its own persistent Electron
browser session partition:

```text
persist:site-profile-<profile-id>
```

That keeps cookies, local storage, IndexedDB, cache, and service worker data
separate between profiles and preserved after closing the app. The app does not
store passwords separately.

## Run on Windows

Install Node.js LTS, then run from this directory:

```powershell
winget install OpenJS.NodeJS.LTS
```

```powershell
npm install
npm start
```

## Build Windows App

Recommended build command:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The script checks Node.js/npm, installs dependencies, and runs the Electron
Builder package step.

Build portable only:

```powershell
.\build.ps1 -Target portable
```

Rebuild without reinstalling dependencies:

```powershell
.\build.ps1 -SkipInstall
```

Manual build:

```powershell
npm install
npm run dist
```

Build output is written to:

```text
dist\
```

## MVP Features

- Create, rename, edit, and delete profiles.
- Store profile name, URL, note, color, and last opened date.
- Search profiles by name, URL, or note.
- Open a site in an isolated persistent browser profile.
- Keep sessions after closing and reopening the app.
- Clear local browser data for a selected profile.
- Delete profile with a confirmation that browser data will be removed.
- Set a default URL.
- Choose startup behavior: profile list or last opened profile.

## Local Data

Profile metadata is stored locally in Electron's `userData` directory:

```text
profiles.json
```

Browser data is stored by Electron under persistent session partition storage in
the same app data area. No login, password, cookie, or token data is sent to any
server by this app.

## Notes

- This app does not bypass 2FA, CAPTCHA, session expiration, or site security.
- If a site invalidates sessions, the user must log in again in that profile.
- Session storage persistence depends on Chromium behavior and the site.
