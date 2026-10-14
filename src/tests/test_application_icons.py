import unittest
from unittest.mock import Mock, patch

from game_tools_hub.app import (
    application_icon_path as hub_icon_path,
    configure_application_icon,
)


class ApplicationIconTests(unittest.TestCase):
    def test_source_icons_exist(self):
        self.assertTrue(hub_icon_path().is_file())
        self.assertEqual(hub_icon_path().suffix.lower(), ".ico")

    def test_game_tools_hub_assigns_application_icon(self):
        app = Mock()
        with patch("game_tools_hub.app.QIcon", side_effect=lambda path: ("icon", path)):
            path = configure_application_icon(app)
        app.setWindowIcon.assert_called_once_with(("icon", str(path)))

    def test_updater_assigns_and_bundles_window_icon(self):
        project_root = hub_icon_path().parents[3]
        updater_source = (project_root / "src" / "updater.py").read_text(encoding="utf-8")
        spec_source = (project_root / "src" / "app.spec").read_text(encoding="utf-8")
        self.assertIn("configure_window_icon(root)", updater_source)
        self.assertIn("root.iconbitmap(default=str(icon_path))", updater_source)
        self.assertIn("datas=icon_datas", spec_source)

    def test_missing_icon_is_not_assigned(self):
        app = Mock()
        with patch("game_tools_hub.app.application_icon_path", return_value=None):
            self.assertIsNone(configure_application_icon(app))
        app.setWindowIcon.assert_not_called()


if __name__ == "__main__":
    unittest.main()
