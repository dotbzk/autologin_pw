import argparse
import ctypes
import json
from ctypes import wintypes
import os
from pathlib import Path
from queue import Empty, Queue
import subprocess
import sys
import threading
import time
import tkinter as tk
from tkinter import messagebox, ttk

from update_support import download_release, install_release
from background_memory import stop_background_cleaner


def wait_for_parent_exit(pid):
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.WaitForSingleObject.argtypes = (wintypes.HANDLE, wintypes.DWORD)
    kernel32.WaitForSingleObject.restype = wintypes.DWORD
    kernel32.CloseHandle.argtypes = (wintypes.HANDLE,)
    handle = kernel32.OpenProcess(0x00100000, False, pid)
    if not handle:
        if ctypes.get_last_error() == 87:
            return
        raise OSError(ctypes.get_last_error(), "Cannot wait for the running application")
    try:
        result = kernel32.WaitForSingleObject(handle, 60000)
        if result != 0:
            raise TimeoutError("The running application did not close within 60 seconds")
    finally:
        kernel32.CloseHandle(handle)


def run_update(args, events):
    archive_path = Path(sys.executable).resolve().parent / "release.zip"

    def status(value):
        events.put(("status", value))

    try:
        status("Waiting for the application to close...")
        wait_for_parent_exit(args.parent_pid)
        stop_background_cleaner(args.install_dir)
        status("Downloading update...")
        download_release(
            args.url, args.sha256, archive_path,
            lambda received, total: events.put((
                "progress", received / total * 100 if total else 0,
            )),
        )
        status("Installing update...")
        notes_path = archive_path.parent / "release-notes.json"
        release_notes = ""
        if notes_path.is_file():
            with open(notes_path, encoding="utf-8") as notes_file:
                release_notes = json.load(notes_file).get("notes", "")

        def launch_app(executable):
            environment = os.environ.copy()
            environment["GAME_LAUNCHER_UPDATE_TEMP"] = str(archive_path.parent)
            ready_file = archive_path.parent / "ready"
            ready_file.unlink(missing_ok=True)
            environment["GAME_LAUNCHER_UPDATE_READY_FILE"] = str(ready_file)
            process = subprocess.Popen(
                [str(executable)], cwd=executable.parent, env=environment,
                close_fds=True,
            )
            deadline = time.monotonic() + 30
            while time.monotonic() < deadline:
                if process.poll() is not None:
                    raise RuntimeError("The updated application exited during startup")
                if ready_file.is_file():
                    return
                time.sleep(0.25)
            process.terminate()
            process.wait(timeout=10)
            raise TimeoutError("The updated application did not finish starting")

        install_release(
            archive_path, args.install_dir, args.version, launch_app,
            progress=status,
            release_notes=release_notes,
        )
        events.put(("done", "Update installed"))
    except Exception as exc:
        events.put(("error", str(exc)))
    finally:
        try:
            archive_path.unlink(missing_ok=True)
        except OSError:
            pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--install-dir", required=True)
    parser.add_argument("--parent-pid", required=True, type=int)
    parser.add_argument("--version", required=True)
    parser.add_argument("--url", required=True)
    parser.add_argument("--sha256", required=True)
    args = parser.parse_args()

    root = tk.Tk()
    root.title("Game Launcher Bot Update")
    root.geometry("460x155")
    root.resizable(False, False)
    root.attributes("-topmost", True)
    frame = ttk.Frame(root, padding=20)
    frame.pack(fill="both", expand=True)
    status = tk.StringVar(value="Preparing update...")
    ttk.Label(frame, textvariable=status).pack(anchor="w", pady=(0, 15))
    progress = ttk.Progressbar(frame, length=410, mode="determinate", maximum=100)
    progress.pack(fill="x", pady=(0, 15))
    close_button = ttk.Button(frame, text="Close", command=root.destroy, state="disabled")
    close_button.pack(anchor="e")
    root.protocol("WM_DELETE_WINDOW", lambda: None)

    events = Queue()

    def process_events():
        try:
            while True:
                kind, value = events.get_nowait()
                if kind == "status":
                    status.set(value)
                elif kind == "progress":
                    progress["value"] = value
                elif kind == "done":
                    status.set(value)
                    progress["value"] = 100
                    root.after(1500, root.destroy)
                elif kind == "error":
                    status.set("Update failed")
                    close_button.configure(state="normal")
                    root.protocol("WM_DELETE_WINDOW", root.destroy)
                    messagebox.showerror("Update failed", value, parent=root)
        except Empty:
            pass
        root.after(100, process_events)

    threading.Thread(target=run_update, args=(args, events), daemon=True).start()
    root.after(100, process_events)
    root.mainloop()


if __name__ == "__main__":
    main()
