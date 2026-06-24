from dataclasses import dataclass


@dataclass(frozen=True)
class WindowInfo:
    hwnd: int
    pid: int
    title: str
    width: int
    height: int

    @property
    def area(self):
        return self.width * self.height


def find_new_windows(baseline_hwnds, windows):
    return sorted(
        (window for window in windows if window.hwnd not in baseline_hwnds),
        key=lambda window: window.area,
        reverse=True,
    )
