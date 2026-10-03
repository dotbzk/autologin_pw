import configparser
import json
import os
from pathlib import Path
import tempfile
import unittest
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
            self.backend.saveSettings(rows)
        config = configparser.ConfigParser()
        config.read(self.root / "configs/config.ini")
        self.assertFalse(config.getboolean("UPDATES", "auto_check"))

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


class QmlSmokeTests(unittest.TestCase):
    def test_background_artwork_has_real_transparency(self):
        artwork = Path(__file__).resolve().parents[1] / "configs/back.png"
        image = QImage(str(artwork))
        self.assertFalse(image.isNull())
        self.assertTrue(image.hasAlphaChannel())
        self.assertEqual(image.pixelColor(0, 0).alpha(), 0)

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
            "manageGroupsButton", "settingsButton", "updateButton",
            "memoryCleanupCheckBox", "logArea", "progressBar", "runButton",
            "stopButton", "debugButton", "minimizeButton", "closeButton",
        }
        self.assertEqual(
            {name for name in names if root.findChild(object, name) is None},
            set(),
        )
        QMetaObject.invokeMethod(root.findChild(object, "selectAllButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(all(account["selected"] for account in backend.accounts))
        QMetaObject.invokeMethod(root.findChild(object, "unselectAllButton"), "click")
        QMetaObject.invokeMethod(root.findChild(object, "debugButton"), "click")
        QT_APP.processEvents()
        self.assertFalse(any(account["selected"] for account in backend.accounts))
        self.assertTrue(backend.debugEnabled)
        accounts_list = root.findChild(object, "accountsList")
        accounts_list.setProperty("contentY", 24)
        QT_APP.processEvents()
        self.assertGreater(accounts_list.property("contentY"), 0)
        manage = root.findChild(object, "managePopup")
        settings = root.findChild(object, "settingsPopup")
        alert = root.findChild(object, "alertPopup")
        QMetaObject.invokeMethod(root.findChild(object, "manageGroupsButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(manage.property("visible"))
        QMetaObject.invokeMethod(manage, "close")
        QMetaObject.invokeMethod(root.findChild(object, "settingsButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(settings.property("visible"))
        QMetaObject.invokeMethod(settings, "close")
        QMetaObject.invokeMethod(root.findChild(object, "updateButton"), "click")
        QT_APP.processEvents()
        self.assertTrue(alert.property("visible"))
        root.setProperty("visible", False)
        # Keep the context object alive for the lifetime of its QML engine.
        QML_KEEPALIVE.append((backend, engine, root))


if __name__ == "__main__":
    unittest.main()
