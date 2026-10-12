import json
from pathlib import Path

from PySide6.QtCore import QObject, Property, Signal, Slot

from .core.process_manager import ModuleProcessManager
from .core.registry import ModuleRegistry


class HubBackend(QObject):
    modulesChanged = Signal()
    alertRequested = Signal(str, str, str)

    def __init__(self, package_root=None, process_manager=None):
        super().__init__()
        self.package_root = Path(package_root or Path(__file__).resolve().parent)
        self.registry = ModuleRegistry(
            self.package_root / "modules",
            allowed_root=self.package_root.parent,
        )
        self.process_manager = process_manager or ModuleProcessManager()
        self._modules = []
        self._manifests = {}
        self._version = self._read_version()
        self.refreshModules()

    def _read_version(self):
        try:
            data = json.loads((self.package_root / "version.json").read_text(encoding="utf-8"))
            return str(data["version"])
        except (OSError, KeyError, TypeError, ValueError):
            return "0.0.0"

    @Property(str, constant=True)
    def version(self):
        return self._version

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
        return True
