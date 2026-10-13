# Architecture

```text
Game Tools Hub (shell)
├── module registry
├── process manager
├── shared-package update coordinator
├── shared navigation and status UI
└── module manifests
    ├── Game Launcher Bot adapter ──> existing independent process
    ├── PW Account Hub adapter ─────> independent .NET process
    └── additional tools ───────────> future independent processes
```

## Module contract

Every module has a `module.json` containing stable identity, display metadata,
supported platforms, capabilities, lifecycle status and process entry points.
The shell validates manifests before exposing them to the UI.

The module lifecycle is intentionally small:

1. discover manifests;
2. show available and planned tools;
3. start a ready tool in its own process;
4. prevent accidental duplicate starts.

## Release package

`release-manifest.json` is the source of truth for installed project versions and
lists Game Tools Hub, Game Launcher Bot and PW Account Hub separately. Those
versions are independent. The release ZIP contains all component executables.
The shared updater verifies the archive, preserves user data, keeps the existing
root directory and rolls back its contents if the new shell cannot start.

The archive keeps the historical `GameLauncherBot-win64.zip` name so an older
launcher can perform the migration without understanding the new manifest.

A future IPC contract should be versioned independently and use local JSON
messages. It can add health, focus, shutdown and notification operations without
coupling module implementations to the shell.

## PW Account Hub boundary

The account manager is a new implementation, not an extension of Game Launcher
Bot. Its internal layers should be:

```text
PW Account Hub
├── account catalog (groups, labels, metadata)
├── browser session service
│   └── isolated private context per opened account
├── navigation and automation service
├── optional Lua/JavaScript scripting service
├── encrypted secret provider
└── account manager UI
```

Browser cookies and authentication state must never cross account contexts.
Secrets must not be sent to the shell or written to logs. The reference
`Pw.Hub.exe` and its scripts may be inspected to document observable behavior,
but the new implementation should have its own maintainable source tree and
tests.
