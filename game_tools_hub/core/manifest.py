from dataclasses import dataclass
from pathlib import Path
import re
import sys


MODULE_ID = re.compile(r"^[a-z][a-z0-9-]{1,63}$")
VALID_STATUSES = {"ready", "planned", "disabled"}
VALID_LAUNCH_TYPES = {"external-process", "none"}


@dataclass(frozen=True)
class ModuleManifest:
    module_id: str
    display_name: str
    version: str
    description: str
    short_name: str
    status: str
    platforms: tuple[str, ...]
    capabilities: tuple[str, ...]
    launch_type: str
    source_entry: Path | None
    windows_entry: Path | None
    manifest_path: Path

    @classmethod
    def from_dict(cls, data, manifest_path, allowed_root, entry_mode="all"):
        manifest_path = Path(manifest_path).resolve()
        allowed_root = Path(allowed_root).resolve()
        module_id = str(data.get("id", ""))
        if not MODULE_ID.fullmatch(module_id):
            raise ValueError(f"Invalid module id in {manifest_path}: {module_id!r}")

        status = str(data.get("status", "planned"))
        if status not in VALID_STATUSES:
            raise ValueError(f"Invalid module status for {module_id}: {status}")

        launch = data.get("launch") or {}
        launch_type = str(launch.get("type", "none"))
        if launch_type not in VALID_LAUNCH_TYPES:
            raise ValueError(f"Invalid launch type for {module_id}: {launch_type}")

        if entry_mode not in {"all", "source", "windows"}:
            raise ValueError(f"Invalid manifest entry mode: {entry_mode}")

        def resolve_entry(key, mode):
            if entry_mode not in {"all", mode}:
                return None
            raw_value = launch.get(key)
            if not raw_value:
                return None
            resolved = (manifest_path.parent / str(raw_value)).resolve()
            try:
                resolved.relative_to(allowed_root)
            except ValueError as exc:
                raise ValueError(
                    f"Module entry for {module_id} leaves the application root"
                ) from exc
            return resolved

        platforms = tuple(str(value) for value in data.get("platforms", []))
        if not platforms:
            raise ValueError(f"Module {module_id} must declare at least one platform")

        return cls(
            module_id=module_id,
            display_name=str(data.get("displayName") or module_id),
            version=str(data.get("version") or "0.0.0"),
            description=str(data.get("description") or ""),
            short_name=str(data.get("shortName") or module_id[:2].upper()),
            status=status,
            platforms=platforms,
            capabilities=tuple(str(value) for value in data.get("capabilities", [])),
            launch_type=launch_type,
            source_entry=resolve_entry("sourceEntry", "source"),
            windows_entry=resolve_entry("windowsEntry", "windows"),
            manifest_path=manifest_path,
        )

    def is_platform_supported(self, platform=None):
        return (platform or sys.platform) in self.platforms

    def to_ui_dict(self):
        supported = self.is_platform_supported()
        if self.status == "planned":
            status_text = "COMING SOON"
        elif self.status == "disabled":
            status_text = "DISABLED"
        elif not supported:
            status_text = "WINDOWS ONLY"
        else:
            status_text = "READY"
        return {
            "id": self.module_id,
            "name": self.display_name,
            "version": self.version,
            "description": self.description,
            "shortName": self.short_name,
            "status": self.status,
            "statusText": status_text,
            "capabilities": list(self.capabilities),
            "canLaunch": self.status == "ready" and supported,
        }
