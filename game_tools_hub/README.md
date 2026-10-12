# Game Tools Hub

Game Tools Hub is a separate modular shell for Windows game utilities. It does
not contain or modify the implementation of Game Launcher Bot. Tools are
discovered from `modules/*/module.json` and run as isolated processes.

## Current modules

- **Game Launcher Bot** — an adapter for the existing application in `../src`.
- **PW Account Hub** — a planned account manager with isolated private browser
  sessions. The reference binary under `../test` is research input only and is
  never launched or packaged by this shell.

## Design boundaries

- The shell owns navigation, discovery, availability and process lifecycle.
- Each tool owns its UI, data, dependencies, version and release process.
- A module failure cannot crash another module.
- Manifests cannot point outside the repository/application root.
- Future cross-module communication must use a versioned local IPC contract;
  modules must not import each other's internal code.
- Account cookies, tokens and browser data belong to PW Account Hub only and
  must never be stored in manifests or shell settings.

## PW Account Hub plan

PW Account Hub will keep one browser context per account. Each context will be
private and isolated from every other account, with no shared cookie container.
The implementation should expose account selection, page navigation and script
execution through its own service boundary. The shell only starts the module.

## Run the shell

From the repository root:

```bash
python -m game_tools_hub
```

On macOS this is a UI preview: Windows-only modules remain disabled.

## Tests

```bash
python -m unittest discover -s game_tools_hub/tests -v
```
