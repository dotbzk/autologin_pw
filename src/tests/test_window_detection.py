import unittest

from src.window_detection import WindowInfo, find_new_windows


class FindNewWindowsTests(unittest.TestCase):
    def test_returns_only_new_windows_largest_first(self):
        baseline = {10, 20}
        windows = [
            WindowInfo(10, 1, "Existing", 1000, 800),
            WindowInfo(30, 2, "Game", 1920, 1080),
            WindowInfo(40, 3, "Splash", 400, 300),
        ]

        self.assertEqual(
            [window.hwnd for window in find_new_windows(baseline, windows)],
            [30, 40],
        )

    def test_returns_empty_when_no_window_was_created(self):
        windows = [WindowInfo(10, 1, "Existing", 1000, 800)]
        self.assertEqual(find_new_windows({10}, windows), [])


if __name__ == "__main__":
    unittest.main()
