"""Exercise app orchestration without a display or Windows automation."""

import configparser
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

from src import accounts_config, background_memory, screen_settings, update_support


def load_headless_app():
    ctk = Mock()
    ctk.CTk = object
    modules = {
        "customtkinter": ctk, "tkinter": Mock(), "PIL": Mock(),
        "win32con": Mock(), "win32gui": Mock(), "autologin_pw": Mock(),
        "accounts_config": accounts_config, "screen_settings": screen_settings,
        "update_support": update_support, "background_memory": background_memory,
    }
    path = Path(__file__).resolve().parents[1] / "app.py"
    spec = importlib.util.spec_from_file_location("headless_app", path)
    module = importlib.util.module_from_spec(spec)
    with patch.dict("sys.modules", modules), patch("sys.platform", "headless-test"):
        spec.loader.exec_module(module)
    return module


app_module = load_headless_app()


class AppLifecycleTests(unittest.TestCase):
    def setUp(self):
        self.app = app_module.App.__new__(app_module.App)
        self.app.closing = False
        self.app.background_ready = True
        self.app.background_preparing = False
        self.app.update_check_running = False
        self.app.worker_thread = None
        self.app.config_path = "unused.ini"
        self.app.app_version = "1.2.0"
        for name in ("log", "after", "run_button", "update_button", "destroy"):
            setattr(self.app, name, Mock())
        app_module.messagebox.reset_mock()

    def test_auto_update_defaults_on_and_respects_disabled_setting(self):
        config = configparser.ConfigParser()
        self.app.check_for_updates = Mock()
        with patch.object(app_module.sys, "frozen", True, create=True), \
                patch.object(app_module, "read_config_with_fallback", return_value=config):
            self.app.auto_check_updates()
            self.app.check_for_updates.assert_called_once_with(automatic=True)
            self.app.check_for_updates.reset_mock()
            config["UPDATES"] = {"auto_check": "no"}
            self.app.auto_check_updates()
            self.app.check_for_updates.assert_not_called()

    def test_auto_update_waits_for_account_run(self):
        self.app.worker_thread = Mock()
        self.app.worker_thread.is_alive.return_value = True
        self.app.check_for_updates = Mock()
        with patch.object(app_module.sys, "frozen", True, create=True), \
                patch.object(app_module, "read_config_with_fallback", return_value=configparser.ConfigParser()):
            self.app.auto_check_updates()
        self.app.check_for_updates.assert_not_called()
        self.app.after.assert_called_once_with(3000, self.app.auto_check_updates)

    def test_overlapping_update_checks_are_ignored(self):
        with patch.object(app_module.sys, "frozen", True, create=True), \
                patch.object(app_module, "Thread") as thread:
            self.app.check_for_updates()
            self.app.check_for_updates()
            thread.assert_called_once()

    def test_automatic_no_update_and_errors_are_silent(self):
        self.app.show_update_result(None, automatic=True)
        self.app.show_update_error("offline", automatic=True)
        app_module.messagebox.showinfo.assert_not_called()
        app_module.messagebox.showerror.assert_not_called()
        self.app.log.assert_called_with("Update check failed: offline")

    def test_manual_errors_still_display_dialog(self):
        self.app.show_update_error("offline")
        app_module.messagebox.showerror.assert_called_once()

    def test_closing_ignores_inflight_update_result(self):
        self.app.closing = True
        self.app.show_update_result({"version": "1.3.0"}, automatic=True)
        app_module.messagebox.askyesno.assert_not_called()

    def test_update_restart_does_not_start_background_cleaner(self):
        app_module.messagebox.askyesno.return_value = True
        with patch.object(app_module, "start_updater") as updater, \
                patch.object(app_module, "start_background_cleaner") as cleaner:
            self.app.show_update_result({"version": "1.3.0"})
            updater.assert_called_once()
            cleaner.assert_not_called()
        self.app.destroy.assert_called_once()

    def test_close_waits_for_worker(self):
        self.app.worker_thread = Mock()
        self.app.worker_thread.is_alive.return_value = True
        self.app.close_app()
        self.assertTrue(self.app.stop_requested)
        self.app.destroy.assert_not_called()
        self.app.after.assert_called_once_with(100, self.app.finish_close)

    def test_disabled_background_option_closes_without_spawning(self):
        with patch.object(app_module, "read_config_with_fallback", return_value=configparser.ConfigParser()), \
                patch.object(app_module, "start_background_cleaner") as cleaner:
            self.app.finish_close()
            cleaner.assert_not_called()
        self.app.destroy.assert_called_once()

    def test_update_notice_is_shown_only_once(self):
        with tempfile.TemporaryDirectory() as directory:
            notice = Path(directory) / update_support.UPDATE_NOTICE
            notice.write_text(json.dumps({"version": "1.2.0", "notes": "New feature"}), encoding="utf-8")
            app_module.ctk.reset_mock()
            with patch.object(app_module, "resource_path", side_effect=lambda name: str(Path(directory) / name)), \
                    patch.dict(app_module.os.environ, {}, clear=True):
                self.app.show_update_changelog()
                self.assertFalse(notice.exists())
                self.app.show_update_changelog()
            app_module.ctk.CTkToplevel.assert_called_once()
            app_module.ctk.CTkTextbox.return_value.insert.assert_called_with("1.0", "New feature")

    def test_first_update_from_old_updater_uses_bundled_notes(self):
        with tempfile.TemporaryDirectory() as directory:
            version = Path(directory) / "version.json"
            version.write_text(json.dumps({"version": "1.2.0", "changes": ["New feature"]}), encoding="utf-8")
            app_module.ctk.reset_mock()
            with patch.object(app_module, "resource_path", side_effect=lambda name: str(Path(directory) / name)), \
                    patch.dict(app_module.os.environ, {"GAME_LAUNCHER_UPDATE_READY_FILE": "ready"}):
                self.app.show_update_changelog()
            app_module.ctk.CTkTextbox.return_value.insert.assert_called_with("1.0", "• New feature")


if __name__ == "__main__":
    unittest.main()
