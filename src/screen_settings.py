POINT_NAMES = ("play_button", "dropdown", "open_new_client", "scroll")
REGION_SECTIONS = ("SEARCH", "CURRENT_ACCOUNT")


def rectangle_from_points(start, end):
    left = min(start[0], end[0])
    top = min(start[1], end[1])
    return left, top, abs(end[0] - start[0]), abs(end[1] - start[1])


def validate_screen_entries(values, screen_bounds):
    left, top, width, height = screen_bounds
    right = left + width
    bottom = top + height

    def number(section, key):
        try:
            return int(values[(section, key)].strip())
        except (KeyError, ValueError) as exc:
            raise ValueError(f"[{section}] {key} must be an integer") from exc

    for name in POINT_NAMES:
        x = number("COORDINATES", f"{name}_x")
        y = number("COORDINATES", f"{name}_y")
        if not (left <= x < right and top <= y < bottom):
            raise ValueError(f"[COORDINATES] {name} is outside the screen")

    for section in REGION_SECTIONS:
        x = number(section, "region_x")
        y = number(section, "region_y")
        region_width = number(section, "region_w")
        region_height = number(section, "region_h")
        if region_width <= 0 or region_height <= 0:
            raise ValueError(f"[{section}] region width and height must be positive")
        if x < left or y < top or x + region_width > right or y + region_height > bottom:
            raise ValueError(f"[{section}] region is outside the screen")
