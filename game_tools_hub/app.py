import ctypes
import os
from pathlib import Path
import sys

from PySide6.QtCore import QTimer, QUrl
from PySide6.QtGui import QIcon
from PySide6.QtQml import QQmlApplicationEngine
from PySide6.QtWidgets import QApplication

from .backend import HubBackend


PACKAGE_ROOT = Path(__file__).resolve().parent


def application_icon_path():
    candidates = []
    if getattr(sys, "frozen", False):
        candidates.append(Path(sys.executable).resolve().parent / "configs" / "ico" / "app.ico")
        bundle_root = getattr(sys, "_MEIPASS", None)
        if bundle_root:
            candidates.append(Path(bundle_root) / "configs" / "ico" / "app.ico")
    else:
        candidates.append(PACKAGE_ROOT.parent / "src" / "configs" / "ico" / "app.ico")
    return next((path for path in candidates if path.is_file()), None)


def configure_application_icon(app):
    icon_path = application_icon_path()
    if icon_path is not None:
        app.setWindowIcon(QIcon(str(icon_path)))
    return icon_path


def main():
    os.environ.setdefault("QT_QUICK_CONTROLS_STYLE", "Basic")
    if sys.platform == "win32":
        try:
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(
                "autologin_pw.GameToolsHub"
            )
        except (AttributeError, OSError):
            pass
    app = QApplication(sys.argv)
    app.setApplicationName("Game Tools Hub")
    app.setOrganizationName("GameToolsHub")
    configure_application_icon(app)

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
