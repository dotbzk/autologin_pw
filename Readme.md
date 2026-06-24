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
- Automatic VK Play foreground restoration between game launches
- Editable coordinates and delays
- Reproducible Windows build with PyInstaller

## Requirements

- Windows 10 or Windows 11
- Python 3.12 for running from source or building the application
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

### `CURRENT_ACCOUNT`

| Setting | Description |
| --- | --- |
| `region_x/y/w/h` | Screen region containing the currently selected account name |
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

### `DELAYS`

All delay values are specified in seconds. Increase them if VK Play or the game does not have enough time to react.

Settings can be edited manually or through the **Settings** button in the application.

## How account search works

1. The bot opens the VK Play account list.
2. It captures the configured `SEARCH` region.
3. RapidOCR returns recognized text and its screen coordinates.
4. The recognized text is compared with the requested account name.
5. On a match, the bot clicks the center of the recognized text.
6. If no match is found, the list is scrolled and scanned again.

OCR models are included in the application build and do not need to be downloaded at runtime.

## Project structure

```text
autologin_pw/
├── src/
│   ├── app.py                 # Desktop UI
│   ├── autologin_pw.py        # VK Play automation
│   ├── account_ocr.py         # OCR and account-name matching
│   ├── build.ps1              # Windows build script
│   ├── app.spec               # PyInstaller configuration
│   ├── requirements.txt       # Runtime dependencies
│   ├── requirements-dev.txt   # Build dependencies
│   ├── Pipfile
│   ├── tests/
│   ├── accounts/
│   │   └── accounts.ini
│   ├── configs/
│   │   ├── config.ini
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
