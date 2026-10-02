"""Optional, per-installation Windows cleaner; no service or startup registration."""

import hashlib
import configparser
import logging
import mmap
from logging.handlers import RotatingFileHandler
import os
from pathlib import Path
import subprocess
import sys
import time

try:
    from .update_support import read_ini
except ImportError:
    from update_support import read_ini

INTERVAL_SECONDS = 180


def object_name(install_dir):
    path = os.path.normcase(str(Path(install_dir).resolve()))
    return "Local\\GameLauncherBot-Cleaner-" + hashlib.sha256(path.encode()).hexdigest()[:24]


class WindowsSession:
    """Named mutex prevents duplicates; events provide cooperative stop/readiness."""

    def __init__(self, install_dir):
        import win32api
        import win32event
        self.api = win32api
        self.events = win32event
        self.name = object_name(install_dir)
        self.stop = win32event.CreateEvent(None, True, False, self.name + "-stop")
        self.ready = win32event.CreateEvent(None, True, False, self.name + "-ready")
        self.mutex = win32event.CreateMutex(None, False, self.name)
        self.process_id = mmap.mmap(-1, 8, tagname=self.name + "-pid")
        self.owned = False

    def acquire(self, timeout_ms=0):
        result = self.events.WaitForSingleObject(self.mutex, timeout_ms)
        if result in (0, 0x80):  # WAIT_OBJECT_0 / WAIT_ABANDONED
            self.owned = True
            return True
        if result == 258:  # WAIT_TIMEOUT
            return False
        raise OSError(f"Cannot wait for cleaner mutex: {result}")

    def wait_stop(self, seconds):
        return self.events.WaitForSingleObject(self.stop, int(seconds * 1000)) == 0

    def close(self):
        if self.owned:
            self.events.ReleaseMutex(self.mutex)
        for handle in (self.mutex, self.ready, self.stop):
            self.api.CloseHandle(handle)
        self.process_id.close()

    def wait_process_exit(self):
        import pywintypes
        pid = int.from_bytes(self.process_id[:8], "little")
        if not pid:
            return
        try:
            process = self.api.OpenProcess(0x00100000, False, pid)
        except pywintypes.error as exc:
            if exc.winerror == 87:
                return
            raise
        try:
            if self.events.WaitForSingleObject(process, 15000) != 0:
                raise TimeoutError("Background memory cleaner process did not exit")
        finally:
            self.api.CloseHandle(process)


def stop_background_cleaner(install_dir):
    if sys.platform != "win32":
        return
    session = WindowsSession(install_dir)
    try:
        session.events.SetEvent(session.stop)
        if not session.acquire(15000):
            raise TimeoutError("Background memory cleaner is still stopping. Try again shortly.")
        # The mutex can be released just before process exit. Wait for the process
        # too, so the updater can replace its EXE and shared DLLs on Windows.
        session.wait_process_exit()
    finally:
        session.close()


def start_background_cleaner(install_dir):
    if sys.platform != "win32":
        raise OSError("Background memory cleanup requires Windows")
    install_dir = Path(install_dir).resolve()
    session = WindowsSession(install_dir)
    try:
        # A running instance already owns the mutex; do not spawn duplicates.
        if not session.acquire():
            return
        session.events.ResetEvent(session.stop)
        session.events.ResetEvent(session.ready)
        session.process_id[:8] = bytes(8)
        session.events.ReleaseMutex(session.mutex)
        session.owned = False
        if getattr(sys, "frozen", False):
            command = [str(install_dir / "MemoryCleaner.exe")]
        else:
            command = [sys.executable, str(install_dir / "memory_cleanup.py")]
        command += ["--background", "--install-dir", str(install_dir),
                    "--parent-pid", str(os.getpid())]
        process = subprocess.Popen(
            command, cwd=install_dir, stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW, close_fds=True,
        )
        deadline = time.monotonic() + 10
        while session.events.WaitForSingleObject(session.ready, 100) != 0:
            if process.poll() is not None:
                raise RuntimeError("Background memory cleaner failed to start")
            if time.monotonic() >= deadline:
                session.events.SetEvent(session.stop)
                raise TimeoutError("Background memory cleaner startup timed out")
    finally:
        session.close()


def cleanup_loop(config_path, wait_stop, clean, logger, monotonic=time.monotonic):
    """Clean immediately, then every three minutes; reread settings every second."""
    next_cleanup = monotonic()
    while not wait_stop(0):
        try:
            config = read_ini(config_path)
            if not config.getboolean("MEMORY_CLEANUP", "background_enabled", fallback=False):
                return
            options = {key: config.getboolean("MEMORY_CLEANUP", key, fallback=True)
                       for key in ("trim_processes", "trim_file_cache", "purge_standby_list")}
        except (OSError, ValueError, configparser.Error) as exc:
            logger.warning("Cannot read cleaner settings: %s", exc)
            return
        if monotonic() >= next_cleanup:
            try:
                logger.info(clean(**options).summary())
            except Exception:
                logger.exception("Background memory cleanup failed")
            next_cleanup = monotonic() + INTERVAL_SECONDS
        if wait_stop(1):
            return


def run_background_cleaner(install_dir, parent_pid, clean):
    if sys.platform != "win32":
        return 1
    import pywintypes
    install_dir = Path(install_dir).resolve()
    session = WindowsSession(install_dir)
    parent = None
    handler = None
    logger = logging.getLogger("background_memory")
    try:
        if not session.acquire():
            return 0
        session.process_id[:8] = os.getpid().to_bytes(8, "little")
        log_dir = install_dir / "logs"
        log_dir.mkdir(exist_ok=True)
        handler = RotatingFileHandler(log_dir / "memory-cleaner.log", maxBytes=1_000_000,
                                      backupCount=2, encoding="utf-8")
        handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(message)s"))
        logger.addHandler(handler)
        logger.setLevel(logging.INFO)
        try:
            parent = session.api.OpenProcess(0x00100000, False, parent_pid)
        except pywintypes.error as exc:
            if exc.winerror != 87:  # Parent may already have exited.
                raise
        session.events.SetEvent(session.ready)
        if parent:
            while session.events.WaitForSingleObject(parent, 0) != 0:
                if session.wait_stop(0.2):
                    return 0
        logger.info("Background cleaner started; interval=%ss", INTERVAL_SECONDS)
        cleanup_loop(install_dir / "configs" / "config.ini", session.wait_stop, clean, logger)
        logger.info("Background cleaner stopped")
        return 0
    finally:
        if parent:
            session.api.CloseHandle(parent)
        if handler:
            logger.removeHandler(handler)
            handler.close()
        session.close()
