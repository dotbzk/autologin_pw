import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
os.environ.setdefault("QT_QUICK_CONTROLS_STYLE", "Basic")

from PySide6.QtCore import QCoreApplication, QEvent, QUrl
from PySide6.QtQml import QQmlApplicationEngine
from PySide6.QtWidgets import QApplication

from game_tools_hub.backend import HubBackend


QT_APP = QApplication.instance() or QApplication([])


class HubUiTests(unittest.TestCase):
    def test_default_modules_and_qml_shell_load(self):
        package_root = Path(__file__).resolve().parents[1]
        backend = HubBackend(package_root)
        module_ids = {module["id"] for module in backend.modules}
        self.assertEqual(module_ids, {"game-launcher-bot", "pw-hub"})

        engine = QQmlApplicationEngine()
        engine.rootContext().setContextProperty("backend", backend)
        engine.load(QUrl.fromLocalFile(str(package_root / "qml" / "Main.qml")))
        self.assertTrue(engine.rootObjects())
        root = engine.rootObjects()[0]
        self.assertIsNotNone(root.findChild(object, "moduleGrid"))
        self.assertIsNotNone(root.findChild(object, "hubAlertPopup"))
        self.assertIsNotNone(root.findChild(object, "checkUpdatesButton"))
        self.assertIsNotNone(root.findChild(object, "hubUpdatePopup"))
        root.deleteLater()
        QCoreApplication.sendPostedEvents(None, QEvent.DeferredDelete)
        QT_APP.processEvents()
        engine.deleteLater()

    def test_update_summary_reports_each_changed_component(self):
        package_root = Path(__file__).resolve().parents[1]
        backend = HubBackend(package_root)
        requested = []
        backend.updateConfirmationRequested.connect(
            lambda version, details: requested.append((version, details))
        )
        manifest = {
            "schemaVersion": 1,
            "releaseVersion": "1.1.0",
            "components": {
                "gameToolsHub": {"displayName": "Game Tools Hub", "version": "0.3.0"},
                "gameLauncherBot": {"displayName": "Game Launcher Bot", "version": "1.1.0"},
                "pwHub": {"displayName": "PW Account Hub", "version": "1.1.0"},
            },
        }
        backend._show_update_result({"version": "1.1.0", "manifest": manifest})
        self.assertEqual(requested[0][0], "1.1.0")
        self.assertIn("Game Tools Hub: 0.2.2 → 0.3.0", requested[0][1])
        self.assertIn("Game Launcher Bot: 1.0.3 → 1.1.0", requested[0][1])
        self.assertIn("PW Account Hub: 1.0.4 → 1.1.0", requested[0][1])

    def test_early_pw_hub_failure_reports_log_locations(self):
        package_root = Path(__file__).resolve().parents[1]
        process_manager = Mock()
        process_manager.exit_code.return_value = 1
        backend = HubBackend(package_root, process_manager=process_manager)
        alerts = []
        backend.alertRequested.connect(lambda *args: alerts.append(args))
        backend._report_early_module_exit("pw-hub")
        self.assertEqual(alerts[0][0], "PW Account Hub failed to start")
        self.assertIn("startup-error.log", alerts[0][1])

    def test_install_update_restarts_into_game_tools_hub(self):
        package_root = Path(__file__).resolve().parents[1]
        backend = HubBackend(package_root)
        backend._pending_release = {"version": "1.1.0"}
        with patch("game_tools_hub.backend.start_updater") as start, \
                patch("game_tools_hub.backend.QCoreApplication.quit") as quit_app:
            backend.installPendingUpdate()
        start.assert_called_once_with(
            backend.install_root,
            backend._pending_release,
            launch_executable="GameToolsHub.exe",
        )
        quit_app.assert_called_once_with()

    def test_old_updater_handoff_repairs_permissions_before_ready_marker(self):
        package_root = Path(__file__).resolve().parents[1]
        backend = HubBackend(package_root)
        with tempfile.TemporaryDirectory() as directory:
            update_temp = Path(directory)
            marker = update_temp / "ready"
            environment = {
                "GAME_LAUNCHER_UPDATE_READY_FILE": str(marker),
                "GAME_LAUNCHER_UPDATE_TEMP": str(update_temp),
            }
            with patch.dict(os.environ, environment, clear=True), \
                    patch("game_tools_hub.backend.reset_install_permissions", return_value=True) as reset:
                backend.signal_update_ready()
            reset.assert_called_once_with(backend.install_root)
            self.assertTrue(marker.is_file())


if __name__ == "__main__":
    unittest.main()
