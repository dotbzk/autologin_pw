import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

from game_tools_hub.core.process_manager import ModuleProcessManager
from game_tools_hub.core.registry import ModuleRegistry


def write_manifest(path, **overrides):
    data = {
        "id": "sample-tool",
        "displayName": "Sample Tool",
        "shortName": "ST",
        "description": "A sample module.",
        "status": "ready",
        "platforms": [sys.platform],
        "capabilities": ["Testing"],
        "launch": {"type": "external-process", "sourceEntry": "tool.py"},
    }
    data.update(overrides)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data), encoding="utf-8")


class ModuleRegistryTests(unittest.TestCase):
    def test_discovers_manifest_and_resolves_entry(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            module = root / "modules" / "sample"
            (module / "tool.py").parent.mkdir(parents=True)
            (module / "tool.py").write_text("pass\n", encoding="utf-8")
            write_manifest(module / "module.json")
            manifests = ModuleRegistry(root / "modules", root).discover()
            self.assertEqual(len(manifests), 1)
            self.assertEqual(manifests[0].module_id, "sample-tool")
            self.assertEqual(manifests[0].source_entry, (module / "tool.py").resolve())

    def test_rejects_entry_outside_application_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest_path = root / "modules" / "sample" / "module.json"
            write_manifest(
                manifest_path,
                launch={"type": "external-process", "sourceEntry": "../../../../outside.py"},
            )
            with self.assertRaisesRegex(ValueError, "leaves the application root"):
                ModuleRegistry(root / "modules", root).discover()

    def test_rejects_duplicate_module_ids(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_manifest(root / "modules" / "one" / "module.json")
            write_manifest(root / "modules" / "two" / "module.json")
            with self.assertRaisesRegex(ValueError, "Duplicate module id"):
                ModuleRegistry(root / "modules", root).discover()


class ModuleProcessManagerTests(unittest.TestCase):
    def test_python_module_uses_current_interpreter(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            entry = root / "tool.py"
            entry.write_text("pass\n", encoding="utf-8")
            write_manifest(root / "modules" / "sample" / "module.json")
            manifest_file = root / "modules" / "sample" / "module.json"
            data = json.loads(manifest_file.read_text(encoding="utf-8"))
            data["launch"]["sourceEntry"] = "../../tool.py"
            manifest_file.write_text(json.dumps(data), encoding="utf-8")
            manifest = ModuleRegistry(root / "modules", root).discover()[0]
            command = ModuleProcessManager().build_command(manifest, frozen=False)
            self.assertEqual(command, [sys.executable, str(entry.resolve())])

    def test_does_not_start_duplicate_process(self):
        process = Mock()
        process.poll.return_value = None
        popen = Mock(return_value=process)
        manager = ModuleProcessManager(popen=popen)
        manifest = Mock(module_id="sample-tool", display_name="Sample Tool")
        with patch.object(manager, "build_command", return_value=["tool.exe"]):
            manager.start(manifest)
            with self.assertRaisesRegex(RuntimeError, "already running"):
                manager.start(manifest)
        popen.assert_called_once()


if __name__ == "__main__":
    unittest.main()
