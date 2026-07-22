import sys
import unittest
from contextlib import redirect_stdout
from io import StringIO

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


if __name__ == "__main__":
    unittest.main()
