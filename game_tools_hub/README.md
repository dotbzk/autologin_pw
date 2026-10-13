# Game Tools Hub

Game Tools Hub is a separate modular shell for Windows game utilities. It does
not contain or modify the implementation of Game Launcher Bot. Tools are
discovered from `modules/*/module.json` and run as isolated processes.

## Current modules

- **Game Launcher Bot** — an adapter for the existing application in `../src`.
- **PW Account Hub** — the recovered editable .NET account manager, published
  under `PwHub/` with isolated private browser sessions.

## Design boundaries

- The shell owns navigation, discovery, availability and process lifecycle.
- Each tool owns its UI, data, dependencies and component version.
- Each project owns and increments its own displayed version.
- Windows releases are distributed together with rollback protection.
- A module failure cannot crash another module.
- Manifests cannot point outside the repository/application root.
- Future cross-module communication must use a versioned local IPC contract;
  modules must not import each other's internal code.
- Account cookies, tokens and browser data belong to PW Account Hub only and
  must never be stored in manifests or shell settings.

## Updates

The built Hub displays the installed version of every component and checks the
latest GitHub Release through the shared updater. A release contains
`release-manifest.json`, which lists independent component versions and their
executable paths. Its package release number exists only for update transport and
legacy compatibility; it is not an application version. Update installation is
disabled while a launched module is still running.

If PW Account Hub exits during startup, the shell reports its exit code and points
to `PwHub\logs\startup-error.log` and the Local AppData fallback.

The legacy archive name `GameLauncherBot-win64.zip` is intentionally retained so
older Game Launcher Bot installations can install Game Tools Hub and PW Account
Hub with their first update.

## PW Account Hub

PW Account Hub keeps one browser context per account. Each context is
private and isolated from every other account, with no shared cookie container.
The implementation should expose account selection, page navigation and script
execution through its own service boundary. The shell only starts the module.

## Run the shell

From the repository root:

```bash
python -m game_tools_hub
```

On macOS this is a UI preview: Windows-only modules and release installation
remain disabled.

## Tests

```bash
python -m unittest discover -s game_tools_hub/tests -v
```
