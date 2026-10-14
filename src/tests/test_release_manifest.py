import json
from pathlib import Path
import re
import unittest

from src.update_support import validate_release_manifest


ROOT = Path(__file__).resolve().parents[2]


def load_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


class ReleaseManifestTests(unittest.TestCase):
    def test_component_versions_match_their_source_versions(self):
        manifest = validate_release_manifest(load_json(ROOT / "release-manifest.json"))
        components = manifest["components"]
        package = load_json(ROOT / "src/version.json")
        launcher_version = load_json(ROOT / "src/launcher-version.json")["version"]
        hub_version = load_json(ROOT / "game_tools_hub/version.json")["version"]
        pw_hub_version = load_json(ROOT / "pw_hub_recovered/version.json")["version"]

        self.assertEqual(manifest["releaseVersion"], package["version"])
        self.assertEqual(components["gameLauncherBot"]["version"], launcher_version)
        self.assertEqual(components["gameToolsHub"]["version"], hub_version)
        self.assertEqual(components["pwHub"]["version"], pw_hub_version)
        assembly_info = (ROOT / "pw_hub_recovered/src/Properties/AssemblyInfo.cs").read_text(
            encoding="utf-8"
        )
        assembly_version = re.search(
            r'AssemblyFileVersion\("(\d+\.\d+\.\d+)\.\d+"\)', assembly_info
        )
        self.assertIsNotNone(assembly_version)
        self.assertEqual(assembly_version.group(1), pw_hub_version)

    def test_component_versions_are_independent(self):
        components = load_json(ROOT / "release-manifest.json")["components"]
        versions = {component["version"] for component in components.values()}
        self.assertGreater(len(versions), 1)

    def test_module_versions_match_release_manifest(self):
        components = load_json(ROOT / "release-manifest.json")["components"]
        launcher = load_json(
            ROOT / "game_tools_hub/modules/game_launcher_bot/module.json"
        )
        pw_hub = load_json(ROOT / "game_tools_hub/modules/pw_hub/module.json")
        self.assertEqual(launcher["version"], components["gameLauncherBot"]["version"])
        self.assertEqual(pw_hub["version"], components["pwHub"]["version"])
        self.assertEqual(pw_hub["status"], "ready")

    def test_pw_hub_browser_extension_has_scoped_permissions(self):
        extension = load_json(ROOT / "pw_hub_browser_extension/manifest.json")
        host_permissions = set(extension["host_permissions"])

        self.assertEqual(extension["manifest_version"], 3)
        self.assertNotIn("<all_urls>", host_permissions)
        self.assertIn("http://127.0.0.1:43827/*", host_permissions)
        self.assertIn("https://*.pwonline.ru/*", host_permissions)
        self.assertIn("https://*.pw.mail.ru/*", host_permissions)
        self.assertNotIn("https://*.mail.ru/*", host_permissions)
        self.assertEqual(set(extension["permissions"]), {"cookies", "storage"})

    def test_windows_bundle_includes_pw_hub_browser_extension(self):
        build_script = (ROOT / "src/build.ps1").read_text(encoding="utf-8")

        self.assertIn('"pw_hub_browser_extension"', build_script)
        self.assertIn('"PwHubBrowserExtension"', build_script)
        self.assertIn('"manifest.json"', build_script)


if __name__ == "__main__":
    unittest.main()
