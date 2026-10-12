import json
from pathlib import Path

from .manifest import ModuleManifest


class ModuleRegistry:
    def __init__(self, modules_root, allowed_root):
        self.modules_root = Path(modules_root)
        self.allowed_root = Path(allowed_root)

    def discover(self):
        manifests = []
        seen = set()
        if not self.modules_root.is_dir():
            return manifests
        for manifest_path in sorted(self.modules_root.glob("*/module.json")):
            try:
                data = json.loads(manifest_path.read_text(encoding="utf-8"))
            except (OSError, TypeError, ValueError) as exc:
                raise ValueError(f"Cannot read module manifest {manifest_path}: {exc}") from exc
            manifest = ModuleManifest.from_dict(
                data,
                manifest_path,
                allowed_root=self.allowed_root,
            )
            if manifest.module_id in seen:
                raise ValueError(f"Duplicate module id: {manifest.module_id}")
            seen.add(manifest.module_id)
            manifests.append(manifest)
        return manifests
