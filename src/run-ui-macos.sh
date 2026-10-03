#!/usr/bin/env bash

set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This preview script is intended for macOS." >&2
    exit 1
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(dirname -- "$script_dir")"

if [[ -n "${PYTHON_BIN:-}" ]]; then
    python_bin="$PYTHON_BIN"
    if [[ ! -x "$python_bin" ]] && ! command -v "$python_bin" >/dev/null 2>&1; then
        echo "Python executable not found: $python_bin" >&2
        exit 1
    fi
else
    if ! command -v python3 >/dev/null 2>&1; then
        echo "Python 3 is required. Install it with Homebrew or from python.org." >&2
        exit 1
    fi

    venv_dir="$project_root/src/.venv-macos"
    python_bin="$venv_dir/bin/python"
    if [[ ! -x "$python_bin" ]]; then
        echo "Creating macOS UI preview environment..."
        python3 -m venv "$venv_dir"
    fi

    if ! "$python_bin" -c 'import PySide6' >/dev/null 2>&1; then
        echo "Installing PySide6 for the UI preview..."
        "$python_bin" -m pip install 'PySide6>=6.8,<7'
    fi
fi

if ! "$python_bin" -c 'import PySide6' >/dev/null 2>&1; then
    echo "PySide6 is not available for: $python_bin" >&2
    echo "Install PySide6 or run without PYTHON_BIN to use the managed preview environment." >&2
    exit 1
fi

echo "Starting Game Launcher Bot in safe UI test mode..."
echo "RUN simulates progress; no game clients or desktop automation will start."
cd "$project_root"
GAME_LAUNCHER_UI_TEST=1 \
QT_QUICK_CONTROLS_STYLE=Basic \
"$python_bin" "$script_dir/app.py"
