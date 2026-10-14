import sys
import unittest
from contextlib import redirect_stdout
from io import StringIO
from types import SimpleNamespace
from unittest.mock import Mock, patch

from src import memory_cleanup
from src.memory_cleanup import MemoryCleanupResult, cleanup_memory, main


class MemoryCleanupTests(unittest.TestCase):
    def test_returns_unsupported_result_off_windows(self):
        if sys.platform == "win32":
            self.skipTest("Non-Windows behavior only")

        result = cleanup_memory()

        self.assertFalse(result.supported)
        self.assertIn("supported only on Windows", result.summary())

    def test_cli_returns_error_off_windows(self):
        if sys.platform == "win32":
            self.skipTest("Non-Windows behavior only")

        with redirect_stdout(StringIO()):
            self.assertEqual(main([]), 1)

    def test_summary_includes_available_memory_delta(self):
        result = MemoryCleanupResult(supported=True)

        self.assertIn("trimmed_processes=0", result.summary())

    def test_missing_optional_privileges_are_warnings(self):
        with patch.object(memory_cleanup, "_load_windows_dlls", return_value=object()), \
                patch.object(memory_cleanup, "get_memory_snapshot", return_value=None), \
                patch.object(
                    memory_cleanup,
                    "_trim_file_cache",
                    return_value=(False, "SeIncreaseQuotaPrivilege is unavailable", True),
                ), \
                patch.object(
                    memory_cleanup,
                    "_purge_standby_list",
                    return_value=(False, "SeProfileSingleProcessPrivilege is unavailable", True),
                ):
            result = cleanup_memory(trim_processes=False)

        self.assertTrue(result.success)
        self.assertEqual(result.errors, [])
        self.assertEqual(len(result.warnings), 2)
        self.assertIn("warnings=", result.summary())

    def test_not_all_assigned_is_an_optional_privilege_limit(self):
        dlls = {
            "kernel32": SimpleNamespace(
                GetCurrentProcess=Mock(return_value=1),
                CloseHandle=Mock(),
            ),
            "advapi32": SimpleNamespace(
                OpenProcessToken=Mock(return_value=True),
                LookupPrivilegeValueW=Mock(return_value=True),
                AdjustTokenPrivileges=Mock(return_value=True),
            ),
        }
        with patch.object(memory_cleanup.ctypes, "set_last_error", create=True), \
                patch.object(
                    memory_cleanup.ctypes,
                    "get_last_error",
                    return_value=memory_cleanup.ERROR_NOT_ALL_ASSIGNED,
                    create=True,
                ):
            enabled, message, permission_limited = memory_cleanup._enable_privilege(
                dlls, "SeIncreaseQuotaPrivilege"
            )

        self.assertFalse(enabled)
        self.assertTrue(permission_limited)
        self.assertIn("current Windows permissions", message)


if __name__ == "__main__":
    unittest.main()
