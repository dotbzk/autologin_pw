import unittest

from src.screen_settings import POINT_NAMES, rectangle_from_points, validate_screen_entries


def settings_values():
    values = {}
    for name in POINT_NAMES:
        values[("COORDINATES", f"{name}_x")] = "100"
        values[("COORDINATES", f"{name}_y")] = "100"
    for section in ("SEARCH", "CURRENT_ACCOUNT"):
        values[(section, "region_x")] = "20"
        values[(section, "region_y")] = "30"
        values[(section, "region_w")] = "200"
        values[(section, "region_h")] = "80"
    return values


class ScreenSettingsTests(unittest.TestCase):
    def test_rectangle_supports_reverse_drag(self):
        self.assertEqual(rectangle_from_points((100, 80), (20, 10)), (20, 10, 80, 70))

    def test_rejects_coordinates_outside_primary_monitor(self):
        values = settings_values()
        values[("COORDINATES", "play_button_x")] = "-100"
        with self.assertRaisesRegex(ValueError, "play_button is outside"):
            validate_screen_entries(values, (0, 0, 1920, 1080))

    def test_rejects_point_outside_screen(self):
        values = settings_values()
        values[("COORDINATES", "dropdown_x")] = "1920"
        with self.assertRaisesRegex(ValueError, "dropdown is outside"):
            validate_screen_entries(values, (0, 0, 1920, 1080))

    def test_rejects_region_outside_screen(self):
        values = settings_values()
        values[("SEARCH", "region_x")] = "1900"
        with self.assertRaisesRegex(ValueError, "SEARCH.*outside"):
            validate_screen_entries(values, (0, 0, 1920, 1080))

    def test_rejects_invalid_size_and_text(self):
        values = settings_values()
        values[("SEARCH", "region_w")] = "0"
        with self.assertRaisesRegex(ValueError, "width and height"):
            validate_screen_entries(values, (0, 0, 1920, 1080))
        values[("SEARCH", "region_w")] = "abc"
        with self.assertRaisesRegex(ValueError, "region_w must be an integer"):
            validate_screen_entries(values, (0, 0, 1920, 1080))


if __name__ == "__main__":
    unittest.main()
