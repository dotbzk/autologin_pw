from pathlib import Path
import subprocess
import sys


class ModuleProcessManager:
    def __init__(self, popen=None):
        self._popen = popen or subprocess.Popen
        self._processes = {}

    def is_running(self, module_id):
        process = self._processes.get(module_id)
        return process is not None and process.poll() is None

    def running_module_ids(self):
        return [
            module_id for module_id in self._processes
            if self.is_running(module_id)
        ]

    def build_command(self, manifest, frozen=None):
        if manifest.launch_type != "external-process":
            raise ValueError(f"Module {manifest.module_id} has no executable entry")
        use_frozen = getattr(sys, "frozen", False) if frozen is None else frozen
        entry = manifest.windows_entry if use_frozen else manifest.source_entry
        if not entry:
            raise ValueError(f"Module {manifest.module_id} has no entry for this build")
        entry = Path(entry)
        if not entry.is_file():
            raise ValueError(f"Module entry was not found: {entry}")
        if entry.suffix.casefold() == ".py":
            return [sys.executable, str(entry)]
        return [str(entry)]

    def start(self, manifest):
        if self.is_running(manifest.module_id):
            raise RuntimeError(f"{manifest.display_name} is already running")
        command = self.build_command(manifest)
        process = self._popen(command, cwd=str(Path(command[-1]).parent))
        self._processes[manifest.module_id] = process
        return process
