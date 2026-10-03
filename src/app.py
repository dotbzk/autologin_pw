import ctypes
import os
from pathlib import Path
import sys

from PySide6.QtCore import QCoreApplication, Qt, QUrl
from PySide6.QtGui import QGuiApplication, QIcon
from PySide6.QtQml import QQmlApplicationEngine
from PySide6.QtWidgets import QApplication

from ui_backend import AppBackend, bundled_path, resource_path


def main():
    os.environ.setdefault("QT_QUICK_CONTROLS_STYLE", "Basic")
    QGuiApplication.setHighDpiScaleFactorRoundingPolicy(
        Qt.HighDpiScaleFactorRoundingPolicy.PassThrough
    )
    if sys.platform == "win32":
        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(
            "autologin_pw.GameLauncherBot"
        )

    app = QApplication(sys.argv)
    app.setApplicationName("Game Launcher Bot")
    app.setOrganizationName("GameLauncherBot")
    icon_path = resource_path("configs/ico/app.ico")
    if Path(icon_path).is_file():
        app.setWindowIcon(QIcon(icon_path))

    engine = QQmlApplicationEngine()
    backend = AppBackend()
    engine.rootContext().setContextProperty("backend", backend)
    engine.load(QUrl.fromLocalFile(bundled_path("qml/Main.qml")))
    if not engine.rootObjects():
        return 1

    QCoreApplication.instance().aboutToQuit.connect(backend.shutdown)
    backend.start()
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
