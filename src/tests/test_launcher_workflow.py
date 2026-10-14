import sys
import unittest
from unittest.mock import Mock, call, patch

from src import account_ocr, accounts_config, memory_cleanup, window_detection


with patch.dict(
    sys.modules,
    {
        "pyautogui": Mock(),
        "win32api": Mock(),
        "win32con": Mock(),
        "win32gui": Mock(),
        "win32process": Mock(),
        "account_ocr": account_ocr,
        "accounts_config": accounts_config,
        "memory_cleanup": memory_cleanup,
        "window_detection": window_detection,
    },
):
    from src import autologin_pw


class LauncherViewPreparationTests(unittest.TestCase):
    def setUp(self):
        self.launcher = object.__new__(autologin_pw.GameLauncher)
        self.launcher.activate_launcher = Mock()
        self.launcher.ensure_launcher_active = Mock()
        self.launcher.debug = Mock()
        self.launcher._stop_requested = False
        self.launcher.stop_flag = None
        self.launcher.dropdown = (120, 80)
        self.launcher.scroll_position = (320, 240)
        self.launcher.wait_after_dropdown_delay = 0
        self.launcher.scroll_up_attempts = 2
        self.launcher.scroll_up_amount = 25
        self.launcher.scroll_up_attempts_delay = 0
        self.launcher.scroll_account_list = Mock()

    def test_main_view_closes_overlays_before_testing_main_controls(self):
        with patch.object(autologin_pw, "pyautogui") as pyautogui, \
                patch.object(autologin_pw.time, "sleep"):
            self.launcher.prepare_launcher_main_view()

        self.launcher.activate_launcher.assert_called_once_with()
        pyautogui.press.assert_called_once_with("esc")
        self.launcher.ensure_launcher_active.assert_called_once_with()

    def test_account_list_is_opened_and_rewound_before_search(self):
        self.launcher.prepare_launcher_main_view = Mock()
        with patch.object(autologin_pw, "pyautogui") as pyautogui, \
                patch.object(autologin_pw.time, "sleep"):
            opened = self.launcher.open_account_list()

        self.assertTrue(opened)
        self.launcher.prepare_launcher_main_view.assert_called_once_with()
        pyautogui.click.assert_called_once_with(120, 80)
        pyautogui.moveTo.assert_called_once_with(320, 240, duration=0.2)
        self.assertEqual(
            self.launcher.scroll_account_list.call_args_list,
            [call(25), call(25)],
        )
        self.launcher.ensure_launcher_active.assert_called_once_with()

    def test_account_list_preparation_honors_stop_request(self):
        self.launcher.prepare_launcher_main_view = Mock()
        self.launcher.stop_flag = Mock(return_value=True)
        with patch.object(autologin_pw, "pyautogui"), \
                patch.object(autologin_pw.time, "sleep"):
            opened = self.launcher.open_account_list()

        self.assertFalse(opened)
        self.launcher.scroll_account_list.assert_not_called()


if __name__ == "__main__":
    unittest.main()
