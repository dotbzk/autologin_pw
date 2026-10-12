# Recovery record

- Reference application: `test/pw-hub-1.0.2/Pw.Hub.exe`
- Reference EXE SHA-256: `1351f362e3241d88b4c0fbc753d4b0cc2747554d1eebc641da1b0831359b26ca`
- Embedded assembly: `Pw.Hub.dll`
- Embedded DLL SHA-256: `1e866f736eda5051f43218b900c3c9eb3b9ff113235aa84c196e05dd93de51ea`
- Runtime target: `.NETCoreApp,Version=v8.0/win-x64`
- Decompiler: ILSpy CLI 9.1.0.7988
- Recovered C# files and resources: 159 before adding documentation and scripts

The reference EXE was read only. Bundle extraction was performed in a temporary
directory and the original distribution was not modified.

## Current build status

- `dotnet restore`: successful
- `dotnet build -c Debug -p:EnableWindowsTargeting=true`: successful
- Compiler result: 0 errors, 23 warnings
- Windows UI/WebView2 runtime test: not performed from macOS
