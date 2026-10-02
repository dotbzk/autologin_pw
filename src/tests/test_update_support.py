import hashlib
from io import BytesIO
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from src.update_support import (
    download_release,
    extract_release,
    install_release,
    parse_release,
    preserve_user_data,
    read_ini,
    start_updater,
    UPDATE_NOTICE,
    version_tuple,
)


class UpdateSupportTests(unittest.TestCase):
    def test_versions_are_numeric(self):
        self.assertGreater(version_tuple("v1.10.0"), version_tuple("1.9.9"))
        with self.assertRaises(ValueError):
            version_tuple("main")

    def test_release_requires_named_asset_and_digest(self):
        digest = hashlib.sha256(b"archive").hexdigest()
        release = parse_release({
            "tag_name": "v1.2.0",
            "body": "## Changes\n- Added background cleanup",
            "assets": [{"name": "GameLauncherBot-win64.zip", "digest": f"sha256:{digest}",
                        "browser_download_url": "https://github.com/example/repo/releases/download/v1.2.0/GameLauncherBot-win64.zip"}],
        })
        self.assertEqual(release["version"], "1.2.0")
        self.assertIn("Added background cleanup", release["notes"])
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            parse_release({"tag_name": "v1.2.0", "assets": [{"name": "GameLauncherBot-win64.zip",
                           "browser_download_url": release["url"]}]})

    def test_extract_rejects_parent_path(self):
        with tempfile.TemporaryDirectory() as directory:
            archive_path = Path(directory) / "bad.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.writestr("../outside.txt", "bad")
            with self.assertRaisesRegex(ValueError, "Unsafe path"):
                extract_release(archive_path, Path(directory) / "stage")

    def test_download_checks_archive_digest(self):
        class Response(BytesIO):
            headers = {"Content-Length": "7"}

        with tempfile.TemporaryDirectory() as directory:
            destination = Path(directory) / "release.zip"
            expected = hashlib.sha256(b"archive").hexdigest()
            with patch("src.update_support.urlopen", return_value=Response(b"archive")):
                download_release("https://github.com/example/release.zip", expected, destination)
            self.assertEqual(destination.read_bytes(), b"archive")
            with patch("src.update_support.urlopen", return_value=Response(b"archive")):
                with self.assertRaisesRegex(ValueError, "SHA-256"):
                    download_release("https://github.com/example/release.zip", "0" * 64, destination)

    def test_install_preserves_user_data_and_removes_old_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            install = root / "client"
            (install / "configs").mkdir(parents=True)
            (install / "accounts").mkdir()
            (install / "logs").mkdir()
            (install / "configs" / "classes").mkdir()
            (install / "configs" / "config.ini").write_text("[UI]\nsize = 42\n", encoding="utf-8")
            (install / "accounts" / "accounts.ini").write_text("[ACCOUNT:mine]\nview_name=mine\n", encoding="utf-8")
            (install / "logs" / "old.txt").write_text("log", encoding="utf-8")
            (install / "old.dll").write_text("old", encoding="utf-8")
            (install / "configs" / "classes" / "custom.png").write_bytes(b"icon")

            archive_path = root / "release.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.writestr("GameLauncherBot.exe", "new")
                archive.writestr("Updater.exe", "new")
                archive.writestr("version.json", json.dumps({"version": "1.2.0"}))
                archive.writestr("configs/config.ini", "[UI]\nsize = 32\nnew_key = yes\n")
                archive.writestr("accounts/accounts.ini", "[ACCOUNT:default]\nview_name=default\n")
                archive.writestr("_internal/new.dll", "new")

            launched = []
            install_release(archive_path, install, "1.2.0", launched.append,
                            release_notes="Added background cleanup")
            notice = json.loads((install / UPDATE_NOTICE).read_text(encoding="utf-8"))
            self.assertEqual(notice, {"version": "1.2.0", "notes": "Added background cleanup"})
            self.assertEqual(launched, [(install / "GameLauncherBot.exe").resolve()])
            self.assertFalse((install / "old.dll").exists())
            self.assertIn("size = 42", (install / "configs" / "config.ini").read_text())
            self.assertIn("new_key = yes", (install / "configs" / "config.ini").read_text())
            self.assertIn("mine", (install / "accounts" / "accounts.ini").read_text())
            self.assertTrue((install / "logs" / "old.txt").exists())
            self.assertEqual((install / "configs" / "classes" / "custom.png").read_bytes(), b"icon")

    def test_install_rolls_back_if_launch_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            install = root / "client"
            install.mkdir()
            (install / "GameLauncherBot.exe").write_text("old", encoding="utf-8")
            archive_path = root / "release.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.writestr("GameLauncherBot.exe", "new")
                archive.writestr("Updater.exe", "new")
                archive.writestr("version.json", '{"version": "1.2.0"}')
                archive.writestr("_internal/new.dll", "new")
                archive.writestr("configs/config.ini", "[UI]\nsize=32\n")
                archive.writestr("accounts/accounts.ini", "[ACCOUNT:default]\nview_name=default\n")

            def fail(_):
                raise OSError("launch failed")

            with self.assertRaisesRegex(OSError, "launch failed"):
                install_release(archive_path, install, "1.2.0", fail)
            self.assertEqual((install / "GameLauncherBot.exe").read_text(), "old")
            self.assertFalse((install / UPDATE_NOTICE).exists())

    def test_update_adds_new_preferences_and_keeps_user_choices(self):
        with tempfile.TemporaryDirectory() as directory:
            previous = Path(directory) / "old"
            staged = Path(directory) / "new"
            for root in (previous, staged):
                (root / "configs").mkdir(parents=True)
            old_path = previous / "configs/config.ini"
            new_path = staged / "configs/config.ini"
            defaults = "[UPDATES]\nauto_check=yes\n[MEMORY_CLEANUP]\nbackground_enabled=no\n"
            old_path.write_text("[UI]\naccount_icon_size=42\n", encoding="utf-8")
            new_path.write_text(defaults, encoding="utf-8")
            preserve_user_data(previous, staged)
            self.assertTrue(read_ini(new_path).getboolean("UPDATES", "auto_check"))
            self.assertFalse(read_ini(new_path).getboolean("MEMORY_CLEANUP", "background_enabled"))
            old_path.write_text("[UPDATES]\nauto_check=no\n[MEMORY_CLEANUP]\nbackground_enabled=yes\n", encoding="utf-8")
            preserve_user_data(previous, staged)
            self.assertFalse(read_ini(new_path).getboolean("UPDATES", "auto_check"))
            self.assertTrue(read_ini(new_path).getboolean("MEMORY_CLEANUP", "background_enabled"))

    def test_updater_receives_unicode_release_notes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Updater.exe").write_bytes(b"updater")
            stage = root / "stage"
            stage.mkdir()
            release = {"version": "1.2.0", "url": "https://github.com/test", "sha256": "0" * 64,
                       "notes": "Добавлена фоновая очистка\nИсправлена ошибка"}
            with patch("src.update_support.tempfile.mkdtemp", return_value=str(stage)), \
                    patch("src.update_support.subprocess.Popen"):
                start_updater(root, release)
            notes = json.loads((stage / "release-notes.json").read_text(encoding="utf-8"))
            self.assertEqual(notes["notes"], release["notes"])


if __name__ == "__main__":
    unittest.main()
