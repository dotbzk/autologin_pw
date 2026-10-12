import os
from pathlib import Path
import sys

from PySide6.QtCore import QTimer, QUrl
from PySide6.QtQml import QQmlApplicationEngine
from PySide6.QtWidgets import QApplication

from .backend import HubBackend


PACKAGE_ROOT = Path(__file__).resolve().parent


def main():
    os.environ.setdefault("QT_QUICK_CONTROLS_STYLE", "Basic")
    app = QApplication(sys.argv)
    app.setApplicationName("Game Tools Hub")
    app.setOrganizationName("GameToolsHub")

    backend = HubBackend(PACKAGE_ROOT)
    engine = QQmlApplicationEngine()
    engine.rootContext().setContextProperty("backend", backend)
    engine.load(QUrl.fromLocalFile(str(PACKAGE_ROOT / "qml" / "Main.qml")))
    if not engine.rootObjects():
        return 1
    backend.signal_update_ready()
    QTimer.singleShot(1500, backend.cleanup_update_temp)
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
