import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
from threading import Thread

from PySide6.QtCore import QCoreApplication, QObject, Property, QTimer, Signal, Slot

from .core.process_manager import ModuleProcessManager
from .core.registry import ModuleRegistry

try:
    from src.update_support import (
        find_update,
        installed_release_manifest,
        start_updater,
        version_tuple,
    )
except ImportError:
    from update_support import (
        find_update,
        installed_release_manifest,
        start_updater,
        version_tuple,
    )


COMPONENT_ORDER = ("gameToolsHub", "gameLauncherBot", "pwHub")


class HubBackend(QObject):
    modulesChanged = Signal()
    updateBusyChanged = Signal()
    alertRequested = Signal(str, str, str)
    updateConfirmationRequested = Signal(str, str)
    _updateResult = Signal(object)
    _updateError = Signal(str)

    def __init__(self, package_root=None, install_root=None, process_manager=None):
        super().__init__()
        self.package_root = Path(package_root or Path(__file__).resolve().parent)
        self.frozen = bool(getattr(sys, "frozen", False))
        self.install_root = Path(
            install_root
            or (Path(sys.executable).resolve().parent if self.frozen else self.package_root.parent)
        ).resolve()
        modules_root = self.install_root / "modules" if self.frozen else self.package_root / "modules"
        self.registry = ModuleRegistry(
            modules_root,
            allowed_root=self.install_root,
            entry_mode="windows" if self.frozen else "all",
        )
        self.process_manager = process_manager or ModuleProcessManager()
        self._modules = []
        self._manifests = {}
        self._version = self._read_version()
        self._release_manifest = self._read_release_manifest()
        self._package_version = self._release_manifest.get("releaseVersion", "0.0.0")
        self._update_busy = False
        self._pending_release = None
        self._updateResult.connect(self._show_update_result)
        self._updateError.connect(self._show_update_error)
        self.refreshModules()

    def _read_version(self):
        try:
            data = json.loads((self.package_root / "version.json").read_text(encoding="utf-8"))
            return str(data["version"])
        except (OSError, KeyError, TypeError, ValueError):
            return "0.0.0"

    def _read_release_manifest(self):
        candidates = [
            self.install_root / "release-manifest.json",
            self.package_root.parent / "release-manifest.json",
        ]
        for candidate in candidates:
            if candidate.is_file():
                try:
                    return installed_release_manifest(candidate)
                except (OSError, TypeError, ValueError):
                    continue
        return {"releaseVersion": "0.0.0", "components": {}}

    @Property(str, constant=True)
    def version(self):
        return self._version

    @Property(bool, notify=updateBusyChanged)
    def updateBusy(self):
        return self._update_busy

    @Property("QVariantList", constant=True)
    def components(self):
        components = self._release_manifest.get("components", {})
        return [
            {
                "id": component_id,
                "name": components[component_id].get("displayName", component_id),
                "version": components[component_id].get("version", "0.0.0"),
            }
            for component_id in COMPONENT_ORDER
            if component_id in components
        ]

    @Property("QVariantList", notify=modulesChanged)
    def modules(self):
        return self._modules

    @Slot()
    def refreshModules(self):
        manifests = self.registry.discover()
        self._manifests = {manifest.module_id: manifest for manifest in manifests}
        self._modules = [manifest.to_ui_dict() for manifest in manifests]
        self.modulesChanged.emit()

    @Slot(str, result=bool)
    def launchModule(self, module_id):
        manifest = self._manifests.get(module_id)
        if not manifest:
            self.alertRequested.emit("Module", f"Unknown module: {module_id}", "error")
            return False
        if manifest.status != "ready":
            self.alertRequested.emit(
                manifest.display_name,
                "This module is planned and is not available yet.",
                "info",
            )
            return False
        if not manifest.is_platform_supported():
            self.alertRequested.emit(
                manifest.display_name,
                "This module is available on Windows only.",
                "warning",
            )
            return False
        try:
            self.process_manager.start(manifest)
        except (OSError, RuntimeError, ValueError) as exc:
            self.alertRequested.emit(manifest.display_name, str(exc), "error")
            return False
        QTimer.singleShot(1500, lambda: self._report_early_module_exit(module_id))
        return True

    def _report_early_module_exit(self, module_id):
        manifest = self._manifests.get(module_id)
        if not manifest:
            return
        exit_code = self.process_manager.exit_code(module_id)
        if exit_code is None or exit_code == 0:
            return
        message = f"The application exited during startup (code {exit_code})."
        if manifest.diagnostic_log_hint:
            message += "\n\n" + manifest.diagnostic_log_hint
        self.alertRequested.emit(f"{manifest.display_name} failed to start", message, "error")

    def _set_update_busy(self, value):
        if self._update_busy != value:
            self._update_busy = value
            self.updateBusyChanged.emit()

    @Slot()
    def checkForUpdates(self):
        if self._update_busy:
            return
        if not self.frozen:
            self.alertRequested.emit(
                "Updates",
                "Update checks are available in the built Windows application.",
                "info",
            )
            return
        self._set_update_busy(True)

        def task():
            try:
                self._updateResult.emit(find_update(self._package_version))
            except Exception as exc:
                self._updateError.emit(str(exc))

        Thread(target=task, daemon=True).start()

    @Slot(object)
    def _show_update_result(self, release):
        self._set_update_busy(False)
        if not release:
            self.alertRequested.emit(
                "Updates",
                "All installed tools are up to date.",
                "info",
            )
            return
        available = release.get("manifest")
        if not available:
            self.alertRequested.emit(
                "Updates",
                "The release does not contain a component manifest.",
                "error",
            )
            return
        current_components = self._release_manifest.get("components", {})
        available_components = available.get("components", {})
        changes = []
        for component_id in COMPONENT_ORDER:
            remote = available_components.get(component_id)
            if not remote:
                continue
            local_version = str(
                current_components.get(component_id, {}).get("version", "0.0.0")
            )
            remote_version = str(remote.get("version", "0.0.0"))
            if version_tuple(remote_version) > version_tuple(local_version):
                name = remote.get("displayName", component_id)
                changes.append(f"{name}: {local_version} → {remote_version}")
        if not changes:
            changes.append("Shared updater and package files will be refreshed.")
        self._pending_release = release
        self.updateConfirmationRequested.emit(
            release["version"],
            "\n".join(changes),
        )

    @Slot(str)
    def _show_update_error(self, message):
        self._set_update_busy(False)
        self.alertRequested.emit("Update check failed", message, "error")

    @Slot()
    def installPendingUpdate(self):
        if not self._pending_release:
            return
        running = self.process_manager.running_module_ids()
        if running:
            names = [
                self._manifests[module_id].display_name
                for module_id in running
                if module_id in self._manifests
            ]
            self.alertRequested.emit(
                "Close running tools",
                "Close these tools before installing the update:\n" + "\n".join(names),
                "warning",
            )
            return
        try:
            start_updater(
                self.install_root,
                self._pending_release,
                launch_executable="GameToolsHub.exe",
            )
        except Exception as exc:
            self.alertRequested.emit("Update", str(exc), "error")
            return
        QCoreApplication.quit()

    def signal_update_ready(self):
        marker = os.environ.get("GAME_LAUNCHER_UPDATE_READY_FILE")
        temporary_dir = os.environ.get("GAME_LAUNCHER_UPDATE_TEMP")
        if not marker or not temporary_dir:
            return
        expected_marker = os.path.realpath(os.path.join(temporary_dir, "ready"))
        if os.path.realpath(marker) != expected_marker:
            return
        try:
            Path(marker).write_text(self._package_version, encoding="utf-8")
        except OSError:
            pass

    def cleanup_update_temp(self, attempts=12):
        path = os.environ.get("GAME_LAUNCHER_UPDATE_TEMP")
        if not path:
            return
        resolved = os.path.realpath(path)
        temp_root = os.path.realpath(tempfile.gettempdir())
        if (
            os.path.dirname(resolved) != temp_root
            or not os.path.basename(resolved).startswith("GameLauncherBot-update-")
        ):
            return
        try:
            shutil.rmtree(resolved)
            os.environ.pop("GAME_LAUNCHER_UPDATE_TEMP", None)
            os.environ.pop("GAME_LAUNCHER_UPDATE_READY_FILE", None)
        except OSError:
            if attempts > 1:
                QTimer.singleShot(5000, lambda: self.cleanup_update_temp(attempts - 1))
