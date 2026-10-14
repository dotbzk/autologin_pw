# PW Account Hub — recovered source

This directory contains a best-effort source reconstruction of `Pw.Hub.exe`
1.0.2. It is separate from Game Launcher Bot and from the Game Tools Hub shell.

The C# project was recovered from the managed `Pw.Hub.dll` embedded in the
self-contained .NET application. Lua and JavaScript files were copied from the
open `Scripts` directory distributed with the reference application.

## Important limitations

- This is decompiled code, not the original repository.
- Original comments, solution structure, build scripts and most local variable
  names are unavailable.
- WPF views were recovered as compiled BAML resources and are packaged into the
  WPF-generated resource container. They are usable, but not editable XAML yet.
- Cross-target compilation succeeds with the .NET 8 SDK. Runtime verification
  still requires Windows and the WebView2 Runtime.
- Do not commit generated account databases, cookies, fingerprints, tokens or
  logs. They are excluded by `.gitignore`.

## Recovered stack

- .NET 8 / WPF / x64
- Microsoft WebView2
- Entity Framework Core with SQLite
- NLua and KeraLua
- AvalonEdit
- Markdig
- NotifyIconEx

## Build

On Windows with the .NET 8 SDK:

```powershell
dotnet restore .\src\Pw.Hub.csproj
dotnet build .\src\Pw.Hub.csproj -c Debug
```

The project file now uses NuGet package references instead of temporary paths
to DLLs extracted from the application bundle.

Bundled scripts use lowercase English `snake_case` file names and English code
comments. Russian text is retained only where runtime matching depends on the
Russian labels or responses returned by `pwonline.ru`.

The recovered project currently builds with zero errors and 23 compiler
warnings. Most warnings describe fire-and-forget async calls or unused fields
preserved by decompilation and should be reviewed before feature development.

The unified Windows build publishes this project into `client/PwHub` and launches
it through Game Tools Hub. Bundle updates preserve `pwhub.db`, cookies,
fingerprints, logs, and local `appsettings*.json` files.

Normal runtime logs are written to `PwHub\logs`. Failures before the main logger
starts are appended to `PwHub\logs\startup-error.log`. If the installation
directory is not writable, diagnostics fall back to
`%LOCALAPPDATA%\GameToolsHub\PwHub\logs\startup-error.log`.

The recovered legacy self-updater is intentionally disabled. PW Account Hub
updates are checked and installed only by Game Tools Hub, avoiding a second
PowerShell updater and duplicate elevation prompts.

## Browser isolation and session import

`WebCoreBrowser` supports two modes:

- `SeparateProfile` uses an isolated WebView2 user-data folder;
- `InPrivate` enables WebView2 private mode.

Normal account switching uses a stable `SeparateProfile` folder derived from the
account ID. These folders live under
`%LOCALAPPDATA%\GameToolsHub\PwHub\Profiles`, retain complete WebView2 site data
and are not deleted when switching accounts. Existing JSON cookie files are used
only to initialize a new persistent profile and remain available as a backup.

The browser extension in `../pw_hub_browser_extension` can import an authorized
PW Online session from Chrome or Edge. `BrowserSessionBridge` accepts requests
only on the IPv4 loopback interface, requires one-time pairing and a persistent
random access token, limits request sizes, and rejects cookies outside
`pwonline.ru` and `pw.mail.ru`.
