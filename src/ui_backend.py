import configparser
import json
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
from datetime import datetime
from threading import Thread

from PySide6.QtCore import (
    QObject, Property, QCoreApplication, QRect, QTimer, QUrl, Qt, Signal, Slot,
)
from PySide6.QtGui import QColor, QGuiApplication, QPainter, QPen
from PySide6.QtWidgets import QWidget

try:
    from .accounts_config import (
        AccountDefinition,
        load_account_definitions,
        replace_account_group,
        write_account_definitions,
    )
    from .background_memory import start_background_cleaner, stop_background_cleaner
    from .screen_settings import POINT_NAMES, REGION_SECTIONS, rectangle_from_points, validate_screen_entries
    from .update_support import UPDATE_NOTICE, find_update, installed_version, start_updater
except ImportError:
    from accounts_config import (
        AccountDefinition,
        load_account_definitions,
        replace_account_group,
        write_account_definitions,
    )
    from background_memory import start_background_cleaner, stop_background_cleaner
    from screen_settings import POINT_NAMES, REGION_SECTIONS, rectangle_from_points, validate_screen_entries
    from update_support import UPDATE_NOTICE, find_update, installed_version, start_updater


def resource_path(relative_path):
    if getattr(sys, "frozen", False):
        base_path = os.path.dirname(sys.executable)
    else:
        base_path = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base_path, relative_path)


def bundled_path(relative_path):
    base_path = getattr(sys, "_MEIPASS", os.path.dirname(os.path.abspath(__file__)))
    return os.path.join(base_path, relative_path)


def read_config_with_fallback(path):
    if not os.path.exists(path):
        raise FileNotFoundError(f"Not found: {path}")
    last_error = None
    for encoding in ("utf-8", "utf-8-sig", "cp1251"):
        try:
            config = configparser.ConfigParser()
            with open(path, encoding=encoding) as config_file:
                config.read_file(config_file)
            return config
        except (UnicodeError, configparser.Error) as exc:
            last_error = exc
    raise ValueError(f"Cannot read {path}: {last_error}")


def _variant(value):
    return value.toVariant() if hasattr(value, "toVariant") else value


class ScreenPicker(QWidget):
    picked = Signal(str, str, "QVariantMap")
    cancelled = Signal()

    def __init__(self, section, name=None):
        super().__init__()
        self.section = section
        self.name = name or ""
        self.start_point = None
        self.end_point = None
        self.screen = QGuiApplication.primaryScreen()
        if not self.screen:
            raise RuntimeError("Primary screen is unavailable")
        self.screen_geometry = self.screen.geometry()
        self.screenshot = self.screen.grabWindow(0)
        self.scale = max(1.0, float(self.screenshot.devicePixelRatio()))
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint | Qt.Tool)
        self.setCursor(Qt.CrossCursor)
        self.setGeometry(self.screen_geometry)
        self.setFocusPolicy(Qt.StrongFocus)

    def paintEvent(self, _event):
        painter = QPainter(self)
        painter.drawPixmap(self.rect(), self.screenshot)
        if self.start_point and self.end_point and not self.name:
            painter.setPen(QPen(QColor("#35d8ff"), 3))
            painter.setBrush(QColor(30, 170, 255, 45))
            painter.drawRect(QRect(self.start_point, self.end_point).normalized())

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.start_point = event.position().toPoint()
            self.end_point = self.start_point
            self.update()

    def mouseMoveEvent(self, event):
        if self.start_point:
            self.end_point = event.position().toPoint()
            self.update()

    def mouseReleaseEvent(self, event):
        if event.button() != Qt.LeftButton or not self.start_point:
            return
        self.end_point = event.position().toPoint()
        origin_x = round(self.screen_geometry.x() * self.scale)
        origin_y = round(self.screen_geometry.y() * self.scale)
        if self.name:
            values = {
                f"{self.name}_x": origin_x + round(self.end_point.x() * self.scale),
                f"{self.name}_y": origin_y + round(self.end_point.y() * self.scale),
            }
        else:
            x, y, width, height = rectangle_from_points(
                (self.start_point.x(), self.start_point.y()),
                (self.end_point.x(), self.end_point.y()),
            )
            if width < 5 or height < 5:
                return
            values = {
                "region_x": origin_x + round(x * self.scale),
                "region_y": origin_y + round(y * self.scale),
                "region_w": round(width * self.scale),
                "region_h": round(height * self.scale),
            }
        self.picked.emit(self.section, self.name, values)
        self.close()

    def keyPressEvent(self, event):
        if event.key() == Qt.Key_Escape:
            self.cancelled.emit()
            self.close()
            return
        super().keyPressEvent(event)


class AppBackend(QObject):
    groupsChanged = Signal()
    currentGroupChanged = Signal()
    accountsChanged = Signal()
    logTextChanged = Signal()
    progressChanged = Signal()
    runningChanged = Signal()
    debugEnabledChanged = Signal()
    memoryCleanupEnabledChanged = Signal()
    readyChanged = Signal()

    alertRequested = Signal(str, str, str)
    updateConfirmationRequested = Signal(str)
    changelogRequested = Signal(str, str)
    summaryRequested = Signal(int, int, "QVariantList", str)
    settingPicked = Signal(str, str, str)
    screenPickingChanged = Signal(bool)

    _logRequested = Signal(str)
    _progressRequested = Signal(int)
    _workerDone = Signal()
    _summaryReady = Signal(object, str)
    _backgroundPrepared = Signal(str)
    _closeCompleted = Signal(str)
    _updateResult = Signal(object, bool)
    _updateError = Signal(str, bool)

    def __init__(self):
        super().__init__()
        self.config_path = resource_path("configs/config.ini")
        self.accounts_path = resource_path("accounts/accounts.ini")
        self.version_path = resource_path("version.json")
        self.app_version = installed_version(self.version_path)
        config = read_config_with_fallback(self.config_path)
        self.class_options = list(config["LIST_OF_CLASSES"].keys()) if config.has_section("LIST_OF_CLASSES") else []
        self.account_icon_size = max(16, min(64, config.getint("UI", "account_icon_size", fallback=32)))
        self._memory_cleanup_enabled = config.getboolean("MEMORY_CLEANUP", "enabled", fallback=False)
        self._groups = []
        self._current_group = ""
        self._accounts = []
        self.accounts_by_group = {}
        self._log_text = ""
        self._progress = 0
        self._running = False
        self._debug_enabled = False
        self._ready = False
        self.stop_requested = False
        self.worker_thread = None
        self.update_check_running = False
        self.closing = False
        self.background_preparing = False
        self._picker = None
        self._pending_release = None
        self.log_file_error_reported = False
        self._ui_test_mode = os.environ.get("GAME_LAUNCHER_UI_TEST") == "1"
        self._ui_test_accounts = []

        logs_dir = resource_path("logs")
        os.makedirs(logs_dir, exist_ok=True)
        started_at = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
        self.log_path = os.path.join(logs_dir, f"run_{started_at}.txt")

        self._logRequested.connect(self._append_log)
        self._progressRequested.connect(self._set_progress)
        self._workerDone.connect(self._finish_worker)
        self._summaryReady.connect(self._show_summary)
        self._backgroundPrepared.connect(self._on_background_prepared)
        self._closeCompleted.connect(self._on_close_completed)
        self._updateResult.connect(self._show_update_result)
        self._updateError.connect(self._show_update_error)
        self.reload_accounts()

    @Property(str, constant=True)
    def backgroundUrl(self):
        return QUrl.fromLocalFile(resource_path("configs/back.png")).toString()

    @Property(str, constant=True)
    def version(self):
        return self.app_version

    @Property("QStringList", notify=groupsChanged)
    def groups(self):
        return self._groups

    @Property(str, notify=currentGroupChanged)
    def currentGroup(self):
        return self._current_group

    @Property("QVariantList", notify=accountsChanged)
    def accounts(self):
        return self._accounts

    @Property("QStringList", constant=True)
    def classOptions(self):
        return self.class_options

    @Property(str, notify=logTextChanged)
    def logText(self):
        return self._log_text

    @Property(int, notify=progressChanged)
    def progress(self):
        return self._progress

    @Property(bool, notify=runningChanged)
    def running(self):
        return self._running

    @Property(bool, notify=debugEnabledChanged)
    def debugEnabled(self):
        return self._debug_enabled

    @Property(bool, notify=memoryCleanupEnabledChanged)
    def memoryCleanupEnabled(self):
        return self._memory_cleanup_enabled

    @Property(bool, notify=readyChanged)
    def ready(self):
        return self._ready

    @Property(bool, notify=readyChanged)
    def updateBusy(self):
        return self.update_check_running

    def _set_ready(self, value):
        if self._ready != value:
            self._ready = value
            self.readyChanged.emit()

    def _set_running(self, value):
        if self._running != value:
            self._running = value
            self.runningChanged.emit()

    @Slot()
    def start(self):
        self.prepare_background_cleaner()
        if self._ui_test_mode:
            self.log("UI test mode enabled: client automation is disabled")
        QTimer.singleShot(500, self.show_update_changelog)
        QTimer.singleShot(10000, self.cleanup_update_temp)
        QTimer.singleShot(20000, self.cleanup_completed_stages)

    @Slot()
    def shutdown(self):
        self.stop_requested = True

    def log(self, text):
        self._logRequested.emit(str(text))

    @Slot(str)
    def _append_log(self, text):
        timestamp = datetime.now().strftime("%H:%M:%S")
        line = f"[{timestamp}] {text}"
        self._log_text = (self._log_text + "\n" + line).lstrip("\n")[-50000:]
        self.logTextChanged.emit()
        try:
            with open(self.log_path, "a", encoding="utf-8") as log_file:
                log_file.write(line + "\n")
        except OSError as exc:
            if not self.log_file_error_reported:
                self.log_file_error_reported = True
                self._log_text += f"\n[{timestamp}] Cannot write log file: {exc}"
                self.logTextChanged.emit()

    def reload_accounts(self, preferred_group=None):
        self.accounts_by_group = {}
        for account in load_account_definitions(self.accounts_path):
            self.accounts_by_group.setdefault(account.server, []).append(account)
        groups = list(self.accounts_by_group)
        old_groups = self._groups
        self._groups = groups
        if old_groups != groups:
            self.groupsChanged.emit()
        group = preferred_group if preferred_group in groups else (
            self._current_group if self._current_group in groups else (groups[0] if groups else "")
        )
        self._select_group(group)

    def _select_group(self, group):
        if self._current_group != group:
            self._current_group = group
            self.currentGroupChanged.emit()
        self._accounts = [
            {
                "name": account.view_name,
                "characterClass": account.character_class,
                "icon": QUrl.fromLocalFile(resource_path(f"configs/classes/{account.character_class}.png")).toString(),
                "selected": False,
            }
            for account in self.accounts_by_group.get(group, [])
        ]
        self.accountsChanged.emit()

    @Slot(str)
    def selectGroup(self, group):
        self._select_group(group)

    @Slot(str)
    def toggleAccount(self, name):
        for account in self._accounts:
            if account["name"] == name:
                account["selected"] = not account["selected"]
                self.accountsChanged.emit()
                return

    @Slot()
    def selectAll(self):
        for account in self._accounts:
            account["selected"] = True
        self.accountsChanged.emit()

    @Slot()
    def unselectAll(self):
        for account in self._accounts:
            account["selected"] = False
        self.accountsChanged.emit()

    @Slot()
    def toggleDebug(self):
        self._debug_enabled = not self._debug_enabled
        self.debugEnabledChanged.emit()
        self.log(f"Debug logging {'enabled' if self._debug_enabled else 'disabled'}")

    def debug_log(self, text):
        if self._debug_enabled:
            self.log(text)

    @Slot(bool)
    def setMemoryCleanupEnabled(self, enabled):
        try:
            config = read_config_with_fallback(self.config_path)
            if not config.has_section("MEMORY_CLEANUP"):
                config.add_section("MEMORY_CLEANUP")
            config["MEMORY_CLEANUP"]["enabled"] = "yes" if enabled else "no"
            self._write_config(config)
        except Exception as exc:
            self.alertRequested.emit("Memory Cleanup", f"Cannot save memory cleanup setting:\n{exc}", "error")
            self.memoryCleanupEnabledChanged.emit()
            return
        self._memory_cleanup_enabled = enabled
        self.memoryCleanupEnabledChanged.emit()
        self.log(f"Memory cleanup {'enabled' if enabled else 'disabled'}")

    @Slot(result="QVariantList")
    def settingsData(self):
        config = read_config_with_fallback(self.config_path)
        booleans = {
            ("UPDATES", "auto_check"): ("Check for updates at startup", True),
            ("MEMORY_CLEANUP", "background_enabled"): ("Clean memory after closing the app (every 3 min)", False),
            ("MEMORY_CLEANUP", "enabled"): ("Clean memory before each client", False),
            ("MEMORY_CLEANUP", "trim_processes"): ("Trim process working sets", True),
            ("MEMORY_CLEANUP", "trim_file_cache"): ("Trim system file cache", True),
            ("MEMORY_CLEANUP", "purge_standby_list"): ("Purge standby memory", True),
        }
        for (section, key), (_, default) in booleans.items():
            if not config.has_section(section):
                config.add_section(section)
            if not config.has_option(section, key):
                config[section][key] = "yes" if default else "no"
        result = []
        for section in config.sections():
            result.append({"kind": "header", "section": section, "key": "", "label": f"[{section}]", "value": ""})
            for key, value in config[section].items():
                label, default = booleans.get((section, key), (key.replace("_", " "), False))
                pick_kind = ""
                pick_name = ""
                if section == "COORDINATES" and key.endswith("_x"):
                    candidate = key[:-2]
                    if candidate in POINT_NAMES:
                        pick_kind, pick_name = "point", candidate
                if section in REGION_SECTIONS and key == "region_x":
                    pick_kind = "region"
                result.append({
                    "kind": "boolean" if (section, key) in booleans else "value",
                    "section": section,
                    "key": key,
                    "label": label,
                    "value": (config.getboolean(section, key, fallback=default)
                              if (section, key) in booleans else value),
                    "pickKind": pick_kind,
                    "pickName": pick_name,
                })
        return result

    @Slot("QVariantList")
    def saveSettings(self, items):
        items = _variant(items)
        config = read_config_with_fallback(self.config_path)
        values = {}
        for item in items:
            item = _variant(item)
            if item.get("kind") == "header":
                continue
            section, key = item["section"], item["key"]
            value = item["value"]
            if isinstance(value, bool):
                value = "yes" if value else "no"
            values[(section, key)] = str(value)
        try:
            validate_screen_entries(values, self.screen_bounds())
            for (section, key), value in values.items():
                if not config.has_section(section):
                    config.add_section(section)
                config[section][key] = value
            self._write_config(config)
        except (OSError, ValueError, configparser.Error) as exc:
            self.alertRequested.emit("Settings", str(exc), "error")
            return
        self._memory_cleanup_enabled = config.getboolean("MEMORY_CLEANUP", "enabled", fallback=False)
        self.memoryCleanupEnabledChanged.emit()
        self.log("Config saved")
        self.alertRequested.emit("Settings", "Settings saved.", "info")

    def _write_config(self, config):
        temporary_path = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="w", encoding="utf-8", dir=os.path.dirname(self.config_path),
                prefix="config-", suffix=".tmp", delete=False,
            ) as temporary_file:
                temporary_path = temporary_file.name
                config.write(temporary_file)
            os.replace(temporary_path, self.config_path)
        finally:
            if temporary_path and os.path.exists(temporary_path):
                os.remove(temporary_path)

    def screen_bounds(self):
        screen = QGuiApplication.primaryScreen()
        if not screen:
            raise RuntimeError("Primary screen is unavailable")
        geometry = screen.geometry()
        scale = max(1.0, screen.devicePixelRatio())
        return (
            round(geometry.x() * scale), round(geometry.y() * scale),
            round(geometry.width() * scale), round(geometry.height() * scale),
        )

    @Slot(str, str)
    def pickScreenTarget(self, section, name):
        if self._picker is not None:
            return
        self.screenPickingChanged.emit(True)
        QTimer.singleShot(250, lambda: self._open_screen_picker(section, name))

    def _open_screen_picker(self, section, name):
        try:
            self._picker = ScreenPicker(section, name or None)
            self._picker.picked.connect(self._on_target_picked)
            self._picker.cancelled.connect(self._on_picker_cancelled)
            self._picker.showFullScreen()
            self._picker.activateWindow()
        except Exception as exc:
            self._picker = None
            self.screenPickingChanged.emit(False)
            self.alertRequested.emit("Select on screen", str(exc), "error")

    @Slot(str, str, "QVariantMap")
    def _on_target_picked(self, section, _name, values):
        for key, value in values.items():
            self.settingPicked.emit(section, key, str(value))
        self._picker = None
        self.screenPickingChanged.emit(False)

    @Slot()
    def _on_picker_cancelled(self):
        self._picker = None
        self.screenPickingChanged.emit(False)

    @Slot(str, result="QVariantMap")
    def groupData(self, group):
        return {
            "name": group,
            "accounts": [
                {"name": account.view_name, "characterClass": account.character_class}
                for account in self.accounts_by_group.get(group, [])
            ],
        }

    @Slot(str, str, "QVariantList", result=bool)
    def saveGroup(self, original_group, group_name, rows):
        if self._running:
            self.alertRequested.emit("Manage Groups", "Stop the current run before editing groups.", "warning")
            return False
        group_name = group_name.strip()
        rows = _variant(rows)
        if not re.fullmatch(r"[\w-]+", group_name):
            self.alertRequested.emit("Manage Groups", "Group name may contain only letters, numbers, _ and -.", "error")
            return False
        collision = any(
            group.casefold() == group_name.casefold()
            and (not original_group or group.casefold() != original_group.casefold())
            for group in self._groups
        )
        if collision:
            self.alertRequested.emit("Manage Groups", f"Group '{group_name}' already exists.", "error")
            return False
        replacements = []
        for row_number, raw_row in enumerate(rows, 1):
            row = _variant(raw_row)
            name = str(row.get("name", "")).strip()
            if not name:
                continue
            character_class = str(row.get("characterClass", "")).strip().casefold()
            if not re.fullmatch(r"[\w-]+", name):
                self.alertRequested.emit("Manage Groups", f"Invalid account name in row {row_number}: {name}", "error")
                return False
            if character_class not in self.class_options:
                self.alertRequested.emit("Manage Groups", f"Select a valid class in row {row_number}.", "error")
                return False
            if not os.path.exists(resource_path(f"configs/classes/{character_class}.png")):
                self.alertRequested.emit("Manage Groups", f"Image not found for class '{character_class}'.", "error")
                return False
            replacements.append(AccountDefinition(name, group_name, character_class))
        if not replacements:
            self.alertRequested.emit("Manage Groups", "Add at least one account to the group.", "error")
            return False
        all_accounts = [account for accounts in self.accounts_by_group.values() for account in accounts]
        try:
            updated = replace_account_group(all_accounts, original_group or None, group_name, replacements)
            write_account_definitions(self.accounts_path, updated)
        except (OSError, ValueError) as exc:
            self.alertRequested.emit("Manage Groups", str(exc), "error")
            return False
        self.reload_accounts(group_name)
        self.log(f"Group saved: {group_name} ({len(replacements)} accounts)")
        return True

    @Slot(str, result=bool)
    def deleteGroup(self, group):
        if self._running:
            self.alertRequested.emit("Delete Group", "Stop the current run before editing groups.", "warning")
            return False
        if not group:
            return False
        all_accounts = [account for accounts in self.accounts_by_group.values() for account in accounts]
        try:
            updated = replace_account_group(all_accounts, group, "", [])
            write_account_definitions(self.accounts_path, updated)
        except (OSError, ValueError) as exc:
            self.alertRequested.emit("Delete Group", str(exc), "error")
            return False
        self.reload_accounts()
        self.log(f"Group deleted: {group}")
        return True

    @Slot()
    def runBot(self):
        selected = [account["name"] for account in self._accounts if account["selected"]]
        if self._ui_test_mode:
            self._start_ui_test_run(selected)
            return
        self._start_run(selected, self._current_group, retry=False)

    def _start_ui_test_run(self, accounts):
        if self.closing or not self._ready or self._running:
            return
        if not accounts:
            self.log("No accounts selected")
            return
        self.stop_requested = False
        self._ui_test_accounts = list(accounts)
        self._set_progress(0)
        self._set_running(True)
        self.log(f"UI test mode: simulating {len(accounts)} selected client(s)")
        QTimer.singleShot(250, self._advance_ui_test_run)

    def _advance_ui_test_run(self):
        if not self._running or not self._ui_test_mode:
            return
        if self.stop_requested:
            self._set_running(False)
            self.log("UI test mode: simulated run stopped")
            return
        self._set_progress(min(100, self._progress + 10))
        if self._progress >= 100:
            launched = len(self._ui_test_accounts)
            self._set_running(False)
            self.log("UI test mode: simulated run completed")
            self.summaryRequested.emit(
                launched, launched, [], self._current_group,
            )
            return
        QTimer.singleShot(250, self._advance_ui_test_run)

    def _start_run(self, accounts, group, retry=False):
        if self.closing or not self._ready:
            return
        if self.worker_thread and self.worker_thread.is_alive():
            self.log("Bot is already running")
            return
        if not accounts:
            self.log("No accounts selected")
            return
        self.stop_requested = False
        self._set_running(True)
        self.log(f"{'Retrying failed' if retry else 'Start'} | group={group} accounts={accounts}")

        def task():
            try:
                from autologin_pw import GameLauncher
                launcher = GameLauncher(
                    selected_accounts=accounts,
                    selected_group=group,
                    stop_flag=lambda: self.stop_requested,
                    log_func=self.log,
                    debug_log_func=self.debug_log,
                    progress_func=lambda value: self._progressRequested.emit(int(value)),
                )
                launcher.finish_callback = lambda results: self._summaryReady.emit(results, group)
                launcher.run()
            except Exception as exc:
                self.log(f"ERROR{' (retry)' if retry else ''}: {exc}")
            finally:
                self._workerDone.emit()

        self.worker_thread = Thread(target=task, daemon=True)
        self.worker_thread.start()

    @Slot()
    def stopBot(self):
        self.stop_requested = True
        self.log("Stop requested")
        if self._ui_test_mode and self._running:
            self._set_running(False)
            self.log("UI test mode: simulated run stopped")

    @Slot("QVariantList", str)
    def retryFailed(self, accounts, group):
        self._start_run([str(value) for value in _variant(accounts)], group, retry=True)

    @Slot(int)
    def _set_progress(self, value):
        value = max(0, min(100, value))
        if self._progress != value:
            self._progress = value
            self.progressChanged.emit()

    @Slot()
    def _finish_worker(self):
        self.worker_thread = None
        self._set_running(False)
        if self.closing:
            self._finish_close()

    @Slot(object, str)
    def _show_summary(self, results, group):
        if self.closing:
            return
        launched = len(results.get("launched", []))
        failed = results.get("failed", [])
        names = [row[0] if isinstance(row, (list, tuple)) else str(row) for row in failed]
        self.summaryRequested.emit(launched, launched + len(failed), names, group)

    @Slot(bool)
    def checkForUpdates(self, automatic=False):
        if self.closing or self.update_check_running or not self._ready:
            return
        if not getattr(sys, "frozen", False):
            if not automatic:
                self.alertRequested.emit("Update", "Updates are available in the built application.", "info")
            return
        if self._running:
            if not automatic:
                self.alertRequested.emit("Update", "Stop the current run before updating.", "warning")
            return
        self.update_check_running = True
        self.readyChanged.emit()
        self.log("Checking for updates...")

        def task():
            try:
                self._updateResult.emit(find_update(self.app_version), automatic)
            except Exception as exc:
                self._updateError.emit(str(exc), automatic)
        Thread(target=task, daemon=True).start()

    @Slot(object, bool)
    def _show_update_result(self, release, automatic):
        self.update_check_running = False
        self.readyChanged.emit()
        if self.closing:
            return
        if not release:
            if not automatic:
                self.alertRequested.emit("Update", f"Version {self.app_version} is up to date.", "info")
            return
        self._pending_release = release
        self.updateConfirmationRequested.emit(release["version"])

    @Slot(str, bool)
    def _show_update_error(self, message, automatic):
        self.update_check_running = False
        self.readyChanged.emit()
        if self.closing:
            return
        self.log(f"Update check failed: {message}")
        if not automatic:
            self.alertRequested.emit("Update", message, "error")

    @Slot()
    def installPendingUpdate(self):
        if not self._pending_release or self._running:
            return
        try:
            start_updater(os.path.dirname(sys.executable), self._pending_release)
        except Exception as exc:
            self.alertRequested.emit("Update", str(exc), "error")
            return
        self.closing = True
        QCoreApplication.quit()

    def auto_check_updates(self):
        if self.closing or not getattr(sys, "frozen", False):
            return
        try:
            config = read_config_with_fallback(self.config_path)
            enabled = config.getboolean("UPDATES", "auto_check", fallback=True)
        except Exception as exc:
            self.log(f"Cannot read update settings: {exc}")
            return
        if enabled:
            if self._running:
                QTimer.singleShot(3000, self.auto_check_updates)
            else:
                self.checkForUpdates(True)

    def show_update_changelog(self):
        if self.closing:
            return
        notice_path = resource_path(UPDATE_NOTICE)
        has_notice = os.path.isfile(notice_path)
        if not has_notice and not os.environ.get("GAME_LAUNCHER_UPDATE_READY_FILE"):
            return
        try:
            with open(notice_path if has_notice else self.version_path, encoding="utf-8") as notice_file:
                notice = json.load(notice_file)
            if notice.get("version") != self.app_version:
                return
            notes = notice.get("notes") or "\n".join(
                f"• {change}" for change in notice.get("changes", [])
            ) or "The release author did not provide change notes."
            self.changelogRequested.emit(self.app_version, notes[:12000])
            if has_notice:
                os.remove(notice_path)
        except (OSError, ValueError, TypeError) as exc:
            self.log(f"Cannot display update notes: {exc}")

    def prepare_background_cleaner(self):
        if self.closing:
            return
        self.background_preparing = True
        self._set_ready(False)

        def task():
            try:
                stop_background_cleaner(resource_path(""))
                error = ""
            except Exception as exc:
                error = str(exc)
            self._backgroundPrepared.emit(error)
        Thread(target=task, daemon=True).start()

    @Slot(str)
    def _on_background_prepared(self, error):
        self.background_preparing = False
        if error:
            self.log(f"Cannot stop background cleaner: {error}")
            QTimer.singleShot(5000, self.prepare_background_cleaner)
            return
        self._set_ready(True)
        self.signal_update_ready()
        QTimer.singleShot(1500, self.auto_check_updates)

    @Slot()
    def requestClose(self):
        if self.closing:
            return
        self.closing = True
        self.stop_requested = True
        self.readyChanged.emit()
        self._finish_close()

    def _finish_close(self):
        if self.worker_thread and self.worker_thread.is_alive():
            QTimer.singleShot(100, self._finish_close)
            return
        if self.background_preparing:
            QTimer.singleShot(100, self._finish_close)
            return
        try:
            config = read_config_with_fallback(self.config_path)
            enabled = config.getboolean("MEMORY_CLEANUP", "background_enabled", fallback=False)
        except Exception as exc:
            self._on_close_completed(str(exc))
            return
        if not enabled or not self._ready:
            QCoreApplication.quit()
            return

        def task():
            try:
                start_background_cleaner(resource_path(""))
                error = ""
            except Exception as exc:
                error = str(exc)
            self._closeCompleted.emit(error)
        Thread(target=task, daemon=True).start()

    @Slot(str)
    def _on_close_completed(self, error):
        if error:
            self.log(f"Background cleaner startup failed: {error}")
        QCoreApplication.quit()

    def signal_update_ready(self):
        marker = os.environ.get("GAME_LAUNCHER_UPDATE_READY_FILE")
        temporary_dir = os.environ.get("GAME_LAUNCHER_UPDATE_TEMP")
        if not marker or not temporary_dir:
            return
        if os.path.realpath(marker) != os.path.realpath(os.path.join(temporary_dir, "ready")):
            return
        try:
            with open(marker, "w", encoding="utf-8") as ready_file:
                ready_file.write(self.app_version)
        except OSError:
            pass

    def cleanup_update_temp(self, attempts=12):
        path = os.environ.get("GAME_LAUNCHER_UPDATE_TEMP")
        if not path:
            return
        resolved = os.path.realpath(path)
        temp_root = os.path.realpath(tempfile.gettempdir())
        if os.path.dirname(resolved) != temp_root or not os.path.basename(resolved).startswith("GameLauncherBot-update-"):
            return
        try:
            shutil.rmtree(resolved)
            os.environ.pop("GAME_LAUNCHER_UPDATE_TEMP", None)
            os.environ.pop("GAME_LAUNCHER_UPDATE_READY_FILE", None)
        except OSError:
            if attempts > 1:
                QTimer.singleShot(5000, lambda: self.cleanup_update_temp(attempts - 1))

    def cleanup_completed_stages(self):
        install_parent = os.path.dirname(os.path.dirname(resource_path("version.json")))
        try:
            with os.scandir(install_parent) as entries:
                for entry in entries:
                    if (entry.name.startswith("GameLauncherBot-stage-")
                            and entry.is_dir(follow_symlinks=False)
                            and os.path.isfile(os.path.join(entry.path, "completed"))):
                        shutil.rmtree(entry.path, ignore_errors=True)
        except OSError:
            pass
