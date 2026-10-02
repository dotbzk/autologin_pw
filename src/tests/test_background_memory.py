import logging
from pathlib import Path
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch

from src.background_memory import (
    cleanup_loop, object_name, run_background_cleaner,
    start_background_cleaner, stop_background_cleaner,
)


class BackgroundMemoryTests(unittest.TestCase):
    def settings(self, root, enabled="yes"):
        path = Path(root) / "config.ini"
        path.write_text(f"[MEMORY_CLEANUP]\nbackground_enabled={enabled}\n"
                        "trim_processes=no\ntrim_file_cache=yes\npurge_standby_list=no\n",
                        encoding="utf-8")
        return path

    def test_cleans_immediately_and_every_180_seconds(self):
        with tempfile.TemporaryDirectory() as root:
            path = self.settings(root)
            clock = [0]

            def wait(seconds):
                clock[0] += seconds
                return clock[0] > 360

            times = []
            clean = Mock(side_effect=lambda **kwargs: (times.append(clock[0]), Mock())[1])
            cleanup_loop(path, wait, clean, logging.getLogger("test"), lambda: clock[0])
            self.assertEqual(times, [0, 180, 360])
            clean.assert_called_with(trim_processes=False, trim_file_cache=True, purge_standby_list=False)

    def test_disabled_or_missing_option_does_not_clean(self):
        with tempfile.TemporaryDirectory() as root:
            path = self.settings(root, "no")
            clean = Mock()
            cleanup_loop(path, lambda _: False, clean, Mock())
            path.write_text("[MEMORY_CLEANUP]\nenabled=yes\n", encoding="utf-8")
            cleanup_loop(path, lambda _: False, clean, Mock())
            clean.assert_not_called()

    def test_switching_off_is_observed_between_cleanups(self):
        with tempfile.TemporaryDirectory() as root:
            path = self.settings(root)
            clean = Mock()

            def wait(seconds):
                if seconds:
                    self.settings(root, "no")
                return False

            cleanup_loop(path, wait, clean, Mock())
            clean.assert_called_once()

    def test_stop_signal_prevents_cleanup(self):
        clean = Mock()
        cleanup_loop("unused", lambda _: True, clean, Mock())
        clean.assert_not_called()

    def test_invalid_config_exits_without_cleaning(self):
        with tempfile.TemporaryDirectory() as root:
            path = self.settings(root, "invalid")
            clean = Mock()
            logger = Mock()
            cleanup_loop(path, lambda _: False, clean, logger)
            clean.assert_not_called()
            logger.warning.assert_called_once()

    def test_cleanup_error_does_not_end_scheduler(self):
        with tempfile.TemporaryDirectory() as root:
            path = self.settings(root)
            clean = Mock(side_effect=OSError("denied"))
            logger = Mock()
            cleanup_loop(path, Mock(side_effect=[False, True]), clean, logger)
            logger.exception.assert_called_once()

    def test_names_are_per_installation(self):
        self.assertEqual(object_name("."), object_name(Path.cwd()))
        self.assertNotEqual(object_name("one"), object_name("two"))

    @patch("src.background_memory.sys.platform", "win32")
    @patch("src.background_memory.WindowsSession")
    @patch("src.background_memory.subprocess.Popen")
    def test_existing_instance_does_not_spawn_duplicate(self, popen, session_type):
        session_type.return_value.acquire.return_value = False
        start_background_cleaner(".")
        popen.assert_not_called()
        session_type.return_value.close.assert_called_once()

    @patch("src.background_memory.sys.platform", "win32")
    @patch("src.background_memory.WindowsSession")
    def test_stop_signals_event_and_waits_for_exit(self, session_type):
        session = session_type.return_value
        session.acquire.return_value = True
        stop_background_cleaner(".")
        session.events.SetEvent.assert_called_once_with(session.stop)
        session.acquire.assert_called_once_with(15000)
        session.wait_process_exit.assert_called_once()
        session.close.assert_called_once()

    @patch("src.background_memory.sys.platform", "win32")
    @patch("src.background_memory.WindowsSession")
    def test_stop_timeout_is_reported(self, session_type):
        session_type.return_value.acquire.return_value = False
        with self.assertRaises(TimeoutError):
            stop_background_cleaner(".")
        session_type.return_value.close.assert_called_once()

    @patch("src.background_memory.sys.platform", "win32")
    @patch("src.background_memory.sys.frozen", True, create=True)
    @patch("src.background_memory.subprocess.CREATE_NO_WINDOW", 0x08000000, create=True)
    @patch("src.background_memory.WindowsSession")
    @patch("src.background_memory.subprocess.Popen")
    def test_built_cleaner_is_started_without_console_and_with_parent_pid(self, popen, session_type):
        session = session_type.return_value
        session.acquire.return_value = True
        session.events.WaitForSingleObject.return_value = 0
        start_background_cleaner(".")
        args, options = popen.call_args
        self.assertEqual(Path(args[0][0]).name, "MemoryCleaner.exe")
        self.assertIn("--background", args[0])
        self.assertIn("--parent-pid", args[0])
        self.assertEqual(options["creationflags"], 0x08000000)

    @patch("src.background_memory.sys.platform", "win32")
    @patch("src.background_memory.WindowsSession")
    @patch("src.background_memory.cleanup_loop")
    def test_cleaner_waits_for_parent_exit_before_cleaning(self, loop, session_type):
        session = session_type.return_value
        session.acquire.return_value = True
        session.events.WaitForSingleObject.side_effect = [258, 0]
        session.wait_stop.return_value = False
        with tempfile.TemporaryDirectory() as directory, \
                patch.dict("sys.modules", {"pywintypes": SimpleNamespace(error=OSError)}):
            run_background_cleaner(directory, 123, Mock())
        session.api.OpenProcess.assert_called_once_with(0x00100000, False, 123)
        session.wait_stop.assert_called_once_with(0.2)
        loop.assert_called_once()
        session.close.assert_called_once()

    @patch("src.background_memory.sys.platform", "win32")
    @patch("src.background_memory.WindowsSession")
    @patch("src.background_memory.cleanup_loop")
    def test_stop_while_waiting_for_parent_does_not_clean(self, loop, session_type):
        session = session_type.return_value
        session.acquire.return_value = True
        session.events.WaitForSingleObject.return_value = 258
        session.wait_stop.return_value = True
        with tempfile.TemporaryDirectory() as directory, \
                patch.dict("sys.modules", {"pywintypes": SimpleNamespace(error=OSError)}):
            run_background_cleaner(directory, 123, Mock())
        loop.assert_not_called()
        session.close.assert_called_once()


if __name__ == "__main__":
    unittest.main()
