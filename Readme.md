# GameLauncherBot

A Windows application for launching multiple Perfect World accounts through VK Play.

The bot opens the account list, recognizes account names with OCR, selects the requested account, and performs the configured launch sequence.

> The application controls the mouse and keyboard. Do not move the mouse, resize VK Play, or switch windows while the bot is running.

## Features

- Account groups
- Compact vertical account list with class icons
- Multiple account selection
- Group creation, editing, and deletion through the application UI
- Batch account creation inside a group
- OCR-based account search without per-account PNG templates
- Immediate launch when the requested account is already selected
- Game launch confirmation by detecting a new Windows window
- Automatic account-list scrolling
- Retry for accounts that were not found
- Stop control
- Progress and normal/debug real-time log modes
- Per-run log files in `logs/run_YYYY-MM-DD_HH-MM-SS.txt`
- Always-on-top application window
- Frameless PySide6/QML interface with DPI-aware artwork and controls
- Automatic VK Play foreground restoration between game launches
- Editable coordinates and delays
- Reproducible Windows build with PyInstaller

## Requirements

- Windows 10 or Windows 11
- Python 3.12 for running from source or building the application
- PySide6 6.8 or newer (installed by `requirements.txt`)
- VK Play running and visible
- Consistent screen resolution and Windows display scaling

The default coordinates in `src/configs/config.ini` are configured for a `2560×1080` display. Other resolutions require different coordinates and OCR region settings.

## Run from source

```powershell
git clone https://github.com/dotbzk/autologin_pw.git
cd autologin_pw

python -m venv src\.venv
.\src\.venv\Scripts\Activate.ps1
python -m pip install -r src\requirements.txt
python src/app.py
```

### Preview the interface on macOS

The macOS preview installs only PySide6 into an isolated environment and starts
the application with Windows automation disabled:

```bash
./src/run-ui-macos.sh
```

In preview mode, **RUN** simulates progress for the selected clients and **STOP**
stops that simulation. It does not import the Windows automation stack, move the
mouse, or start VK Play. To use an existing Python environment containing
PySide6, set `PYTHON_BIN=/path/to/python` before the command.

## Build the Windows application

Run PowerShell from the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build.ps1
```

The local build script waits for Enter before closing so build errors remain
visible. Automated environments can disable the prompt with `-NoPause`.

The build script will:

1. Create a `src/.venv` virtual environment
2. Install application and build dependencies
3. Run the test suite
4. Remove the previous GameLauncherBot build
5. Build the application with PyInstaller
6. Copy the editable INI files next to the executable

Build output:

```text
client\GameLauncherBot.exe
```

To rebuild without reinstalling dependencies:

```powershell
.\src\build.ps1 -SkipInstall
```

Local builds preserve the existing `client/configs/config.ini`,
`client/accounts/accounts.ini`, and logs. Use `-FreshConfig` to build with the
defaults from `src` instead.

## Updates

The built application has an **Update** button. It checks the latest published
GitHub Release, asks before installing a newer version, and starts a separate
updater. The updater closes the current application, verifies and installs the
release archive, restarts the application, and removes temporary files. Account
definitions, logs, and custom class icons are kept; user settings are merged with
new default settings.

The first build containing the updater must be installed manually, preferably
outside the Git checkout. Future releases can then be installed from the
application. To publish a release:

1. Increase the version in `src/version.json` and commit it to `main`.
2. Run `bash ./src/release.sh` from the project root. Add `--dry-run` to check
   the version and commit without creating
   a tag.
3. The script tags the current `origin/main`. The Windows CI build then
   publishes `GameLauncherBot-win64.zip` to GitHub Releases.

The script uses your existing Git push credentials. It does not need a personal
access token: GitHub Actions publishes the release with its `GITHUB_TOKEN`.
Do not commit a token to this repository.

The release script requires Bash, Git, and Python 3. It automatically tries
`python3`, then `python`, and checks the interpreter before accessing Git.
To select a specific interpreter, run
`PYTHON_BIN=/path/to/python3 bash ./src/release.sh --dry-run`.

The update button uses published releases, not arbitrary commits on `main`.

**Settings → UPDATES → Check for updates at startup** is enabled by default,
including when upgrading from an older configuration. The built application checks
once at startup in a background thread. It only prompts when a newer release is
available and always asks before installation. Automatic checks log network errors
without showing an error dialog; the **Update** button still supports manual checks.

After updating, a small **What's new** window shows the release description once.
Keep GitHub Release descriptions concise and user-facing. If no description is
available, the bundled `changes` list in `src/version.json` is used. Update that
list together with the version for each release; it also supports the first upgrade
from an older updater.

## Configure accounts

Accounts are defined in `src/accounts/accounts.ini`. Each account uses its own
section:

```ini
[ACCOUNT:luk_kapela]
view_name = luk_kapela
server = kapela
class = luk
```

- `view_name` is the account name displayed in VK Play.
- `server` is the account group shown in the application.
- `class` selects an icon from `src/configs/classes` for the account row.
- Every account name must be unique.
- OCR matching ignores letter case, spaces, hyphens, and underscores.

For example, `Luk Fenrir`, `luk-fenrir`, and `luk_fenrir` are treated as the same name.

Groups can also be managed with **Manage Groups**. The dialog can create a group
with multiple accounts, edit account names and classes, rename a group, remove
individual accounts, or delete the complete group. Changes are written to
`accounts.ini` and shown without restarting the application.

## Configure the interface

Application settings are stored in `src/configs/config.ini`.

### `UI`

| Setting | Description |
| --- | --- |
| `account_icon_size` | Account-card icon width and height in pixels |

### `LIST_OF_CLASSES`

The keys in this section populate class selectors in **Manage Groups**.
dialog. Every key must have a matching image in `src/configs/classes`.

### `COORDINATES`

| Setting | Description |
| --- | --- |
| `play_button_x/y` | Game launch button position |
| `dropdown_x/y` | Account-list button position |
| `open_new_client_x/y` | New-client confirmation position |
| `scroll_x/y` | A point inside the scrollable account list |

### `SEARCH`

| Setting | Description |
| --- | --- |
| `region_x/y/w/h` | Screen region used for OCR |
| `scroll_up_attempts` | Number of scroll operations used to reach the top |
| `scroll_up` | Mouse-wheel steps used for each upward reset |
| `search_attempts` | Maximum number of account-list screens to scan |
| `scroll` | Mouse-wheel steps per search attempt (not pixels) |
| `ocr_min_confidence` | Minimum accepted OCR confidence |
| `ocr_fuzzy_threshold` | Minimum similarity for fuzzy account-name matching |
| `account_click_offset_x/y` | Offset from the recognized account text center to the final click point |

### `CURRENT_ACCOUNT`

| Setting | Description |
| --- | --- |
| `region_x/y/w/h` | Screen region containing the currently selected account name; keep it wide enough for the longest account name |
| `confirm_attempts` | Number of OCR checks after selecting an account |
| `confirm_delay` | Delay in seconds between confirmation checks |

The current-account image is enlarged before OCR to improve recognition of the
small text in the VK Play header.

### `LAUNCH_VERIFICATION`

| Setting | Description |
| --- | --- |
| `timeout` | Maximum seconds to wait for a new game window |
| `poll_interval` | Interval between window checks |
| `required_observations` | Consecutive checks required to confirm the window |
| `min_window_width/height` | Minimum accepted game-window size |

### `MEMORY_CLEANUP`

| Setting | Description |
| --- | --- |
| `enabled` | Enables memory cleanup before every client launch |
| `background_enabled` | Starts a hidden memory cleaner after closing the application (default: `no`) |
| `trim_processes` | Trims process working sets through the Windows `EmptyWorkingSet` API |
| `trim_file_cache` | Attempts to trim the system file cache |
| `purge_standby_list` | Attempts to purge the Windows standby memory list |

The main window also has a **Clean memory before each client** checkbox. It
updates `MEMORY_CLEANUP.enabled` in `config.ini`.

The cleaner is implemented in `src/memory_cleanup.py` and can be used
separately:

```powershell
python .\src\memory_cleanup.py
```

The PyInstaller build also includes `MemoryCleaner.exe` in the `client`
directory. Some cleanup operations require administrator privileges; if they are
not available, the bot logs a warning and continues launching clients.

In **Settings**, enable **Clean memory after closing the app (every 3 min)** to
start a separate, hidden `MemoryCleaner.exe` when closing GameLauncherBot normally.
It waits for the application to exit, cleans immediately, then every 180 seconds.
It uses the same `trim_processes`, `trim_file_cache`, and `purge_standby_list` settings
as the foreground cleaner. The pre-launch checkbox and background checkbox are
independent.

Reopening GameLauncherBot stops the background cleaner. To keep it off, uncheck
the background option and save settings before closing. Changes to the INI flag
are also detected while the cleaner runs. Only one cleaner per installation can
run, and the updater stops it before replacing application files. Background
activity is recorded in `logs/memory-cleaner.log`, with two rotated backups.
There is no Windows service or startup registration; after a reboot or forced
termination of GameLauncherBot, open and close the application to start it again.

### `DELAYS`

All delay values are specified in seconds. Increase them if VK Play or the game does not have enough time to react.

Settings can be edited manually or through the **Settings** button in the application.
In **Settings**, use **Select screen area** to drag an OCR region over a screenshot,
or use the corresponding **Select ... on screen** button to choose a click or scroll
point. Press Escape to cancel. All coordinate fields remain editable by hand.
The application checks that points and OCR regions fit on the primary display
before saving.

## Interface artwork

The desktop interface is implemented with PySide6 and QML. `src/configs/back.png`
is the visual background; all controls placed over its right-hand panel are live
QML components. Their positions use the same proportional coordinate space as
the background, so Windows display scaling does not move controls away from the
panel.

The current `back.png` is a 1122×1402 RGBA image with a real alpha channel. Its
transparent pixels allow the desktop to remain visible around the character and
decorative frame without color-key removal or a simulated solid background.

## How account search works

1. The bot opens the VK Play account list.
2. It captures the configured `SEARCH` region.
3. RapidOCR returns recognized text and its screen coordinates.
4. The recognized text is compared with the requested account name.
5. On a match, the bot clicks the same row with the configured `account_click_offset_x/y`.
6. If no match is found, the list is scrolled and scanned again.

If OCR splits an account name into adjacent fragments on the same row, the bot
can combine those fragments before clicking. Fragments from different rows are
ignored to avoid mixing similar account names.

For account names that start with a numbered prefix, such as
`x3_mist_fenrir` or `v3_mist_fenrir`, the prefix must match exactly. Fuzzy OCR
matching is still allowed for the rest of the name.

OCR models are included in the application build and do not need to be downloaded at runtime.

## Project structure

```text
autologin_pw/
├── src/
│   ├── app.py                 # Desktop UI
│   ├── ui_backend.py          # QML bridge and UI action handlers
│   ├── qml/
│   │   └── Main.qml           # Interactive frameless interface
│   ├── autologin_pw.py        # VK Play automation
│   ├── account_ocr.py         # OCR and account-name matching
│   ├── background_memory.py   # Background memory-cleaner lifecycle and scheduler
│   ├── build.ps1              # Windows build script
│   ├── run-ui-macos.sh        # Safe macOS interface preview
│   ├── app.spec               # PyInstaller configuration
│   ├── requirements.txt       # Runtime dependencies
│   ├── requirements-dev.txt   # Build dependencies
│   ├── Pipfile
│   ├── tests/
│   ├── accounts/
│   │   └── accounts.ini
│   ├── configs/
│   │   ├── config.ini
│   │   ├── back.png           # Transparent interface artwork background
│   │   ├── classes/           # Account class images
│   │   └── ico/               # Application icon
├── client/                    # Built Windows artifact
├── README.md
└── .gitignore
```

## Tests

```powershell
python -m unittest discover -s src\tests -v
```

Tests are also executed automatically before local and CI builds.
The build script removes temporary PyInstaller files and Python bytecode caches
after completion. The finished `client` directory and reusable `src/.venv` are kept.

## Troubleshooting

### `Launcher not found`

- Make sure VK Play is running.
- The window title must contain `VK Play` or `Игровой центр`.

### Account not found

- Verify the account name in `accounts.ini`.
- Make sure the account list opens after the configured click.
- Check the `SEARCH.region_*` values.
- Reduce `ocr_min_confidence` if OCR confidence is too low.
- Check the screen resolution and Windows display scaling.

### The bot clicks the wrong position

- Update the values in `COORDINATES`.
- Make sure VK Play is maximized on the expected monitor.
- Do not move or resize the window while the bot is running.

### VK Play does not react in time

Increase the corresponding value in the `DELAYS` section.

## Limitations

- Windows only
- VK Play must remain in the foreground
- VK Play interface changes may require updated coordinates and OCR region settings

## License

Private project / internal use.
