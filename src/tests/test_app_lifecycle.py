import configparser
import json
import os
from pathlib import Path
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
os.environ.setdefault("QT_QUICK_CONTROLS_STYLE", "Basic")

from PySide6.QtCore import QCoreApplication, QEvent, QMetaObject, QUrl
from PySide6.QtGui import QImage
from PySide6.QtQml import QQmlApplicationEngine
from PySide6.QtWidgets import QApplication

from src import ui_backend
from src.ui_backend import AppBackend
from src.update_support import UPDATE_NOTICE


QT_APP = QApplication.instance() or QApplication([])
QML_KEEPALIVE = []


def tearDownModule():
    for _backend, engine, root in QML_KEEPALIVE:
        root.deleteLater()
        QCoreApplication.sendPostedEvents(None, QEvent.DeferredDelete)
        QT_APP.processEvents()
        engine.deleteLater()
        QCoreApplication.sendPostedEvents(None, QEvent.DeferredDelete)
        QT_APP.processEvents()
    QML_KEEPALIVE.clear()


class AppBackendTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        (self.root / "configs/classes").mkdir(parents=True)
        (self.root / "accounts").mkdir()
        (self.root / "logs").mkdir()
        (self.root / "version.json").write_text(
            json.dumps({"version": "1.2.0", "changes": ["New interface"]}),
            encoding="utf-8",
        )
        (self.root / "configs/config.ini").write_text(
            """[UI]
account_icon_size=32
[LIST_OF_CLASSES]
mage=mage
tank=tank
[UPDATES]
auto_check=yes
[MEMORY_CLEANUP]
enabled=no
background_enabled=no
trim_processes=yes
trim_file_cache=yes
purge_standby_list=yes
[COORDINATES]
play_button_x=10
play_button_y=10
dropdown_x=20
dropdown_y=20
open_new_client_x=30
open_new_client_y=30
scroll_x=40
scroll_y=40
[SEARCH]
region_x=0
region_y=0
region_w=50
region_h=50
[CURRENT_ACCOUNT]
region_x=0
region_y=0
region_w=50
region_h=50
""",
            encoding="utf-8",
        )
        (self.root / "accounts/accounts.ini").write_text(
            """[ACCOUNT:mage_one]
view_name=mage_one
server=alpha
class=mage
[ACCOUNT:tank_two]
view_name=tank_two
server=beta
class=tank
""",
            encoding="utf-8",
        )
        for name in ("mage", "tank"):
            (self.root / f"configs/classes/{name}.png").write_bytes(b"image")
        self.resource_patch = patch(
            "src.ui_backend.resource_path",
            side_effect=lambda relative: str(self.root / relative),
        )
        self.resource_patch.start()
        self.backend = AppBackend()
        self.backend._set_ready(True)

    def tearDown(self):
        self.resource_patch.stop()
        self.directory.cleanup()

    def test_group_and_account_selection_actions(self):
        self.assertEqual(self.backend.groups, ["alpha", "beta"])
        self.assertEqual(self.backend.currentGroup, "alpha")
        self.backend.selectAll()
        self.assertTrue(all(account["selected"] for account in self.backend.accounts))
        self.backend.unselectAll()
        self.assertFalse(any(account["selected"] for account in self.backend.accounts))
        self.backend.toggleAccount("mage_one")
        self.assertTrue(self.backend.accounts[0]["selected"])
        self.backend.selectGroup("beta")
        self.assertEqual(self.backend.accounts[0]["name"], "tank_two")

    def test_debug_log_and_progress_actions(self):
        self.backend.toggleDebug()
        self.backend.debug_log("details")
        self.backend._set_progress(72)
        self.assertTrue(self.backend.debugEnabled)
        self.assertIn("details", self.backend.logText)
        self.assertEqual(self.backend.progress, 72)

    def test_run_and_stop_buttons_drive_worker_state(self):
        self.backend.selectAll()
        worker = Mock()
        with patch.object(ui_backend, "Thread", return_value=worker) as thread:
            self.backend.runBot()
        thread.assert_called_once()
        worker.start.assert_called_once()
        self.assertTrue(self.backend.running)
        self.backend.stopBot()
        self.assertTrue(self.backend.stop_requested)
        self.assertIn("Stop requested", self.backend.logText)

    def test_ui_test_mode_simulates_run_without_starting_worker(self):
        self.backend._ui_test_mode = True
        self.backend.selectAll()
        with patch.object(ui_backend, "Thread") as thread, \
                patch.object(ui_backend.QTimer, "singleShot") as single_shot:
            self.backend.runBot()
        thread.assert_not_called()
        single_shot.assert_called_once_with(250, self.backend._advance_ui_test_run)
        self.assertTrue(self.backend.running)
        self.backend.stopBot()
        self.assertFalse(self.backend.running)
        self.assertIn("simulated run stopped", self.backend.logText)

    def test_memory_cleanup_checkbox_persists(self):
        self.backend.setMemoryCleanupEnabled(True)
        config = configparser.ConfigParser()
        config.read(self.root / "configs/config.ini")
        self.assertTrue(config.getboolean("MEMORY_CLEANUP", "enabled"))
        self.assertTrue(self.backend.memoryCleanupEnabled)

    def test_group_editor_saves_and_deletes(self):
        rows = [
            {"name": "mage_new", "characterClass": "mage"},
            {"name": "tank_new", "characterClass": "tank"},
        ]
        self.assertTrue(self.backend.saveGroup("alpha", "gamma", rows))
        self.assertIn("gamma", self.backend.groups)
        self.assertEqual(len(self.backend.groupData("gamma")["accounts"]), 2)
        self.assertTrue(self.backend.deleteGroup("gamma"))
        self.assertNotIn("gamma", self.backend.groups)

    def test_settings_expose_boolean_controls_and_save(self):
        rows = self.backend.settingsData()
        auto = next(row for row in rows if row.get("section") == "UPDATES" and row.get("key") == "auto_check")
        self.assertEqual(auto["kind"], "boolean")
        auto["value"] = False
        with patch.object(self.backend, "screen_bounds", return_value=(0, 0, 100, 100)):
            self.assertTrue(self.backend.saveSettings(rows))
        config = configparser.ConfigParser()
        config.read(self.root / "configs/config.ini")
        self.assertFalse(config.getboolean("UPDATES", "auto_check"))

    def test_settings_expose_coordinate_verification_actions(self):
        rows = self.backend.settingsData()
        actions = {
            (row.get("section"), row.get("key")): row.get("testKind")
            for row in rows
            if row.get("testKind")
        }
        self.assertEqual(actions, {
            ("COORDINATES", "play_button_x"): "play",
            ("SEARCH", "region_x"): "search",
            ("CURRENT_ACCOUNT", "region_x"): "current",
        })

    def test_coordinate_verification_uses_unsaved_settings_values(self):
        rows = self.backend.settingsData()
        for row in rows:
            if row.get("section") == "COORDINATES" and row.get("key") == "play_button_x":
                row["value"] = "71"
            if row.get("section") == "COORDINATES" and row.get("key") == "play_button_y":
                row["value"] = "72"
        worker = Mock()
        hidden = []
        self.backend.automationVerificationChanged.connect(hidden.append)
        with patch.object(ui_backend.sys, "platform", "win32"), \
                patch.object(self.backend, "screen_bounds", return_value=(0, 0, 100, 100)), \
                patch.object(
                    self.backend,
                    "_perform_setting_verification",
                    return_value=("Play Button Test", "Done", "info"),
                ) as perform, \
                patch.object(ui_backend, "Thread", return_value=worker) as thread:
            self.backend.verifySetting("play", "", rows)
            worker.start.assert_called_once()
            self.assertTrue(self.backend.verificationBusy)
            self.assertEqual(hidden, [True])
            thread.call_args.kwargs["target"]()
            values = perform.call_args.args[2]
        self.assertEqual(values[("COORDINATES", "play_button_x")], "71")
        self.assertEqual(values[("COORDINATES", "play_button_y")], "72")

    def test_play_coordinate_verification_prepares_main_view_and_clicks(self):
        values = self.backend._settings_values(self.backend.settingsData())
        values[("COORDINATES", "play_button_x")] = "61"
        values[("COORDINATES", "play_button_y")] = "62"
        launcher = Mock()
        automation = SimpleNamespace(
            GameLauncher=Mock(return_value=launcher),
            pyautogui=Mock(),
        )
        with patch.object(self.backend, "_load_automation_module", return_value=automation):
            title, message, level = self.backend._perform_setting_verification(
                "play", "", values
            )
        launcher.prepare_launcher_main_view.assert_called_once_with()
        automation.pyautogui.click.assert_called_once_with(61, 62)
        self.assertEqual(title, "Play Button Test")
        self.assertIn("(61, 62)", message)
        self.assertEqual(level, "info")

    def test_current_account_verification_reports_detected_account(self):
        values = self.backend._settings_values(self.backend.settingsData())
        launcher = Mock()
        launcher.detect_current_account.return_value = ("mage_one", "mage_one (99%)")
        automation = SimpleNamespace(GameLauncher=Mock(return_value=launcher), pyautogui=Mock())
        with patch.object(self.backend, "_load_automation_module", return_value=automation):
            title, message, level = self.backend._perform_setting_verification(
                "current", "", values
            )
        launcher.prepare_launcher_main_view.assert_called_once_with()
        self.assertEqual(title, "Current Account Test")
        self.assertIn("Current account: mage_one", message)
        self.assertEqual(level, "info")

    def test_account_search_verification_clicks_and_confirms_match(self):
        values = self.backend._settings_values(self.backend.settingsData())
        launcher = Mock()
        launcher.wait_after_dropdown_delay = 0
        launcher.scroll_up_attempts = 0
        launcher.scroll_up_amount = 10
        launcher.scroll_up_attempts_delay = 0
        launcher.account_switch_delay = 0
        launcher.open_account_list.return_value = True
        launcher.find_account.return_value = (75, 76)
        launcher.wait_for_account_selected.return_value = True
        automation = SimpleNamespace(GameLauncher=Mock(return_value=launcher), pyautogui=Mock())
        with patch.object(self.backend, "_load_automation_module", return_value=automation), \
                patch.object(ui_backend.time, "sleep"):
            title, message, level = self.backend._perform_setting_verification(
                "search", "mage_one", values
            )
        self.assertEqual(
            automation.pyautogui.click.call_args_list,
            [unittest.mock.call(75, 76)],
        )
        launcher.open_account_list.assert_called_once_with()
        launcher.find_account.assert_called_once_with("mage_one")
        launcher.wait_for_account_selected.assert_called_once_with("mage_one")
        self.assertIn("confirmed as current", message)
        self.assertEqual((title, level), ("Account Search Test", "info"))

    def test_account_search_verification_stops_before_ocr_when_list_does_not_open(self):
        values = self.backend._settings_values(self.backend.settingsData())
        launcher = Mock()
        launcher.open_account_list.return_value = False
        automation = SimpleNamespace(GameLauncher=Mock(return_value=launcher), pyautogui=Mock())
        with patch.object(self.backend, "_load_automation_module", return_value=automation):
            title, message, level = self.backend._perform_setting_verification(
                "search", "mage_one", values
            )
        launcher.open_account_list.assert_called_once_with()
        launcher.find_account.assert_not_called()
        self.assertIn("stopped before OCR", message)
        self.assertEqual((title, level), ("Account Search Test", "warning"))

    def test_screen_picker_hides_and_restores_the_launcher(self):
        visibility = []
        self.backend.screenPickingChanged.connect(visibility.append)
        with patch.object(ui_backend.QTimer, "singleShot") as single_shot:
            self.backend.pickScreenTarget("COORDINATES", "play_button")
        self.assertEqual(visibility, [True])
        single_shot.assert_called_once()
        self.backend._on_picker_cancelled()
        self.assertEqual(visibility, [True, False])

    def test_update_result_prompts_only_when_new_release_exists(self):
        prompts = []
        alerts = []
        self.backend.updateConfirmationRequested.connect(prompts.append)
        self.backend.alertRequested.connect(lambda title, message, kind: alerts.append((title, message, kind)))
        self.backend._show_update_result(None, True)
        self.assertEqual(prompts, [])
        self.assertEqual(alerts, [])
        self.backend._show_update_result({"version": "1.3.0"}, True)
        self.assertEqual(prompts, ["1.3.0"])

    def test_auto_update_defaults_on_and_respects_disabled_setting(self):
        config = configparser.ConfigParser()
        config.read(self.root / "configs/config.ini")
        config.remove_section("UPDATES")
        with (self.root / "configs/config.ini").open("w", encoding="utf-8") as config_file:
            config.write(config_file)
        with patch.object(ui_backend.sys, "frozen", True, create=True), \
                patch.object(self.backend, "checkForUpdates") as check:
            self.backend.auto_check_updates()
            check.assert_called_once_with(True)
            check.reset_mock()
            config["UPDATES"] = {"auto_check": "no"}
            with (self.root / "configs/config.ini").open("w", encoding="utf-8") as config_file:
                config.write(config_file)
            self.backend.auto_check_updates()
            check.assert_not_called()

    def test_auto_update_waits_for_account_run(self):
        self.backend._set_running(True)
        with patch.object(ui_backend.sys, "frozen", True, create=True), \
                patch.object(self.backend, "checkForUpdates") as check, \
                patch.object(ui_backend.QTimer, "singleShot") as single_shot:
            self.backend.auto_check_updates()
        check.assert_not_called()
        single_shot.assert_called_once_with(3000, self.backend.auto_check_updates)

    def test_overlapping_update_checks_are_ignored(self):
        with patch.object(ui_backend.sys, "frozen", True, create=True), \
                patch.object(ui_backend, "Thread") as thread:
            self.backend.checkForUpdates()
            self.backend.checkForUpdates()
        thread.assert_called_once()

    def test_automatic_update_errors_are_silent_but_logged(self):
        alerts = []
        self.backend.alertRequested.connect(lambda *args: alerts.append(args))
        self.backend._show_update_error("offline", True)
        self.assertEqual(alerts, [])
        self.assertIn("Update check failed: offline", self.backend.logText)

    def test_manual_update_errors_display_dialog_signal(self):
        alerts = []
        self.backend.alertRequested.connect(lambda *args: alerts.append(args))
        self.backend._show_update_error("offline", False)
        self.assertEqual(alerts[-1], ("Update", "offline", "error"))

    def test_closing_ignores_inflight_update_result(self):
        prompts = []
        self.backend.updateConfirmationRequested.connect(prompts.append)
        self.backend.closing = True
        self.backend._show_update_result({"version": "1.3.0"}, True)
        self.assertEqual(prompts, [])

    def test_update_restart_does_not_start_background_cleaner(self):
        self.backend._pending_release = {"version": "1.3.0"}
        with patch.object(ui_backend, "start_updater") as updater, \
                patch.object(ui_backend, "start_background_cleaner") as cleaner, \
                patch.object(QCoreApplication, "quit") as quit_app:
            self.backend.installPendingUpdate()
        updater.assert_called_once()
        cleaner.assert_not_called()
        quit_app.assert_called_once()

    def test_close_waits_for_worker(self):
        self.backend.worker_thread = Mock()
        self.backend.worker_thread.is_alive.return_value = True
        with patch.object(ui_backend.QTimer, "singleShot") as single_shot, \
                patch.object(QCoreApplication, "quit") as quit_app:
            self.backend.requestClose()
        self.assertTrue(self.backend.stop_requested)
        quit_app.assert_not_called()
        single_shot.assert_called_once_with(100, self.backend._finish_close)

    def test_disabled_background_option_closes_without_spawning(self):
        with patch.object(ui_backend, "start_background_cleaner") as cleaner, \
                patch.object(QCoreApplication, "quit") as quit_app:
            self.backend._finish_close()
        cleaner.assert_not_called()
        quit_app.assert_called_once()

    def test_update_notice_is_shown_only_once(self):
        notice = self.root / UPDATE_NOTICE
        notice.write_text(
            json.dumps({"version": "1.2.0", "notes": "New feature"}),
            encoding="utf-8",
        )
        changelogs = []
        self.backend.changelogRequested.connect(
            lambda version, notes: changelogs.append((version, notes))
        )
        with patch.dict(os.environ, {}, clear=True):
            self.backend.show_update_changelog()
            self.assertFalse(notice.exists())
            self.backend.show_update_changelog()
        self.assertEqual(changelogs, [("1.2.0", "New feature")])

    def test_first_update_from_old_updater_uses_bundled_notes(self):
        changelogs = []
        self.backend.changelogRequested.connect(
            lambda version, notes: changelogs.append((version, notes))
        )
        with patch.dict(os.environ, {"GAME_LAUNCHER_UPDATE_READY_FILE": "ready"}, clear=True):
            self.backend.show_update_changelog()
        self.assertEqual(changelogs, [("1.2.0", "• New interface")])

    def test_old_updater_handoff_repairs_permissions_before_ready_marker(self):
        update_temp = self.root / "update-temp"
        update_temp.mkdir()
        marker = update_temp / "ready"
        environment = {
            "GAME_LAUNCHER_UPDATE_READY_FILE": str(marker),
            "GAME_LAUNCHER_UPDATE_TEMP": str(update_temp),
        }
        with patch.dict(os.environ, environment, clear=True), \
                patch.object(ui_backend, "reset_install_permissions", return_value=True) as reset:
            self.backend.signal_update_ready()
        reset.assert_called_once_with(str(self.root))
        self.assertEqual(marker.read_text(encoding="utf-8"), "1.2.0")


class QmlSmokeTests(unittest.TestCase):
    def test_background_artwork_has_real_transparency(self):
        artwork = Path(__file__).resolve().parents[1] / "configs/theme/ea.png"
        image = QImage(str(artwork))
        self.assertFalse(image.isNull())
        self.assertTrue(image.hasAlphaChannel())
        self.assertEqual(image.pixelColor(0, 0).alpha(), 0)

    def test_application_stays_alive_while_coordinate_test_hides_window(self):
        app_source = (Path(__file__).resolve().parents[1] / "app.py").read_text(
            encoding="utf-8"
        )
        self.assertIn("app.setQuitOnLastWindowClosed(False)", app_source)

    def test_main_qml_loads_and_exposes_all_primary_controls(self):
        backend = AppBackend()
        backend._set_ready(True)
        engine = QQmlApplicationEngine()
        engine.rootContext().setContextProperty("backend", backend)
        qml = Path(__file__).resolve().parents[1] / "qml/Main.qml"
        engine.load(QUrl.fromLocalFile(str(qml)))
        self.assertTrue(engine.rootObjects())
        root = engine.rootObjects()[0]
        names = {
            "groupCombo", "accountsList", "selectAllButton", "unselectAllButton",
            "settingsMenuPopup", "advancedSettingsButton", "manageGroupsButton",
            "settingsButton", "updateButton",
            "memoryCleanupCheckBox", "logArea", "progressBar", "runButton",
            "stopButton", "debugButton", "minimizeButton", "closeButton",
            "accountSearchPopup", "accountSearchField",
        }
        self.assertEqual(
            {name for name in names if root.findChild(object, name) is None},
            set(),
        )
        QMetaObject.invokeMethod(root.findChild(object, "selectAllButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(all(account["selected"] for account in backend.accounts))
        QMetaObject.invokeMethod(root.findChild(object, "unselectAllButton"), "click")
        settings_menu = root.findChild(object, "settingsMenuPopup")
        QMetaObject.invokeMethod(root.findChild(object, "settingsButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(settings_menu.property("visible"))
        QMetaObject.invokeMethod(root.findChild(object, "debugButton"), "click")
        QT_APP.processEvents()
        self.assertFalse(any(account["selected"] for account in backend.accounts))
        self.assertTrue(backend.debugEnabled)
        accounts_list = root.findChild(object, "accountsList")
        accounts_list.setProperty("contentY", 24)
        QT_APP.processEvents()
        self.assertGreater(accounts_list.property("contentY"), 0)
        scroll_position = accounts_list.property("contentY")
        backend.toggleAccount(backend.accounts[-1]["name"])
        QT_APP.processEvents()
        QT_APP.processEvents()
        self.assertAlmostEqual(
            accounts_list.property("contentY"), scroll_position, delta=0.5
        )
        manage = root.findChild(object, "managePopup")
        settings = root.findChild(object, "settingsPopup")
        unsaved_settings = root.findChild(object, "unsavedSettingsPopup")
        alert = root.findChild(object, "alertPopup")
        QMetaObject.invokeMethod(root.findChild(object, "manageGroupsButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(manage.property("visible"))
        QMetaObject.invokeMethod(manage, "close")
        QMetaObject.invokeMethod(root.findChild(object, "settingsButton"), "click")
        QT_APP.processEvents()
        QMetaObject.invokeMethod(root.findChild(object, "advancedSettingsButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(settings.property("visible"))
        settings.setProperty("dirty", True)
        QMetaObject.invokeMethod(settings, "requestClose")
        QT_APP.processEvents()
        self.assertTrue(settings.property("visible"))
        self.assertTrue(unsaved_settings.property("visible"))
        QMetaObject.invokeMethod(
            root.findChild(object, "discardSettingsButton"), "click"
        )
        QT_APP.processEvents()
        self.assertFalse(settings.property("visible"))
        self.assertFalse(unsaved_settings.property("visible"))
        QMetaObject.invokeMethod(root.findChild(object, "settingsButton"), "click")
        QT_APP.processEvents()
        QMetaObject.invokeMethod(root.findChild(object, "updateButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(alert.property("visible"))
        root.setProperty("visible", False)
        # Keep the context object alive for the lifetime of its QML engine.
        QML_KEEPALIVE.append((backend, engine, root))


if __name__ == "__main__":
    unittest.main()
