import os
from pathlib import Path
import unittest

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
        root.deleteLater()
        QCoreApplication.sendPostedEvents(None, QEvent.DeferredDelete)
        QT_APP.processEvents()
        engine.deleteLater()


if __name__ == "__main__":
    unittest.main()
