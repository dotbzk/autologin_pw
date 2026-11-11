import sys
import unittest
from types import SimpleNamespace
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

    def test_run_cleans_memory_before_clicking_play_for_each_client(self):
        launcher = object.__new__(autologin_pw.GameLauncher)
        launcher.accounts = [{"name": "mage_one"}]
        launcher.results = {"launched": [], "failed": []}
        launcher.finish_callback = None
        launcher.progress = Mock()
        launcher.debug = Mock()
        launcher.log = Mock()
        launcher.should_stop = Mock(return_value=False)
        launcher.activate_launcher = Mock()
        launcher.is_account_selected = Mock(return_value=True)
        launcher.cleanup_memory_before_launch = Mock(return_value=True)
        launcher.prepare_launcher_main_view = Mock()
        launcher.get_client_window_candidates = Mock(return_value=[])
        launcher.wait_for_new_client_window = Mock(
            return_value=SimpleNamespace(
                hwnd=101, pid=202, title="Perfect World", width=1280, height=720,
            )
        )
        launcher.play_button = (10, 20)
        launcher.open_new_client = (30, 40)
        launcher.perv_count_delay = 0
        launcher.launch_delay = 0

        calls = []
        launcher.cleanup_memory_before_launch.side_effect = (
            lambda name: calls.append(("cleanup", name)) or True
        )
        with patch.object(autologin_pw, "pyautogui") as pyautogui, \
                patch.object(autologin_pw.time, "sleep"):
            pyautogui.click.side_effect = lambda *point: calls.append(("click", point))
            launcher.run()

        self.assertEqual(calls[0], ("cleanup", "mage_one"))
        self.assertEqual(calls[1], ("click", (10, 20)))
        launcher.cleanup_memory_before_launch.assert_called_once_with("mage_one")

    def test_memory_cleanup_success_is_visible_in_normal_log(self):
        launcher = object.__new__(autologin_pw.GameLauncher)
        launcher.memory_cleanup_enabled = True
        launcher.memory_cleanup_trim_processes = True
        launcher.memory_cleanup_trim_file_cache = True
        launcher.memory_cleanup_purge_standby_list = True
        launcher.should_stop = Mock(return_value=False)
        launcher.log = Mock()
        launcher.debug = Mock()
        result = memory_cleanup.MemoryCleanupResult(
            supported=True,
            trimmed_processes=4,
            file_cache_trimmed=True,
            standby_list_purged=True,
        )

        with patch.object(autologin_pw, "cleanup_memory", return_value=result):
            self.assertTrue(launcher.cleanup_memory_before_launch("mage_one"))

        self.assertTrue(any(
            "Memory cleanup completed" in call.args[0]
            for call in launcher.log.call_args_list
        ))


if __name__ == "__main__":
    unittest.main()
