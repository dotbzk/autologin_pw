import pyautogui
import time
import configparser
import os
import sys

import win32gui
import win32con
import win32api
import win32process

from accounts_config import load_account_definitions
from account_ocr import (
    AccountTextRecognizer,
    find_exact_combined_match,
    find_matching_line,
)
from window_detection import WindowInfo, find_new_windows


def read_config_with_fallback(path):
    encodings = ["utf-8", "utf-8-sig", "cp1251"]

    for enc in encodings:
        try:
            config = configparser.ConfigParser()
            config.read(path, encoding=enc)
            return config
        except Exception:
            continue

    raise Exception(f"❌ Cannot read config: {path}")


def resource_path(relative_path):
    if getattr(sys, 'frozen', False):
        base_path = os.path.dirname(sys.executable)
    else:
        base_path = os.path.dirname(os.path.abspath(__file__))

    return os.path.join(base_path, relative_path)


def get_number(config, section, key):
    value = config[section][key]
    try:
        if "." in value:
            return float(value)
        return int(value)
    except ValueError:
        raise Exception(f"❌ Invalid number: {section}.{key} = {value}")


class GameLauncher:
    def __init__(
        self,
        selected_accounts=None,
        selected_group=None,
        stop_flag=None,
        log_func=None,
        debug_log_func=None,
        progress_func=None,
    ):

        self.selected_accounts = selected_accounts
        self.selected_group = selected_group

        self.stop_flag = stop_flag
        self.log = log_func if log_func else print
        self.debug = debug_log_func if debug_log_func else (lambda text: None)
        self.progress = progress_func if progress_func else (lambda x: None)

        self.finish_callback = None

        self.results = {"launched": [], "failed": []}
        self._stop_requested = False
        self._last_scroll_hwnd = None
        self.launcher_hwnd = None
        self.launcher_pid = None
        self.account_recognizer = AccountTextRecognizer()

        self.load_config()
        self.load_accounts()

    def should_stop(self):
        if self.stop_flag and self.stop_flag():
            self._stop_requested = True
        return self._stop_requested

    def load_config(self):
        config = read_config_with_fallback(resource_path("configs/config.ini"))

        self.launcher_title = config.get("GENERAL", "launcher_title", fallback="Игровой центр")

        self.play_button = (get_number(config, "COORDINATES", "play_button_x"),
                            get_number(config, "COORDINATES", "play_button_y"))

        self.dropdown = (get_number(config, "COORDINATES", "dropdown_x"),
                         get_number(config, "COORDINATES", "dropdown_y"))

        self.open_new_client = (get_number(config, "COORDINATES", "open_new_client_x"),
                                get_number(config, "COORDINATES", "open_new_client_y"))

        self.scroll_position = (get_number(config, "COORDINATES", "scroll_x"),
                                get_number(config, "COORDINATES", "scroll_y"))

        self.search_region = (
            get_number(config, "SEARCH", "region_x"),
            get_number(config, "SEARCH", "region_y"),
            get_number(config, "SEARCH", "region_w"),
            get_number(config, "SEARCH", "region_h")
        )

        self.scroll_up_attempts = get_number(config, "SEARCH", "scroll_up_attempts")
        self.scroll_up_amount = get_number(config, "SEARCH", "scroll_up")
        self.search_attempts = get_number(config, "SEARCH", "search_attempts")
        self.search_scroll = get_number(config, "SEARCH", "scroll")
        self.ocr_min_confidence = get_number(config, "SEARCH", "ocr_min_confidence")
        self.ocr_fuzzy_threshold = get_number(config, "SEARCH", "ocr_fuzzy_threshold")

        self.current_account_region = (
            get_number(config, "CURRENT_ACCOUNT", "region_x"),
            get_number(config, "CURRENT_ACCOUNT", "region_y"),
            get_number(config, "CURRENT_ACCOUNT", "region_w"),
            get_number(config, "CURRENT_ACCOUNT", "region_h")
        )
        self.selection_confirm_attempts = config.getint(
            "CURRENT_ACCOUNT", "confirm_attempts", fallback=5
        )
        self.selection_confirm_delay = config.getfloat(
            "CURRENT_ACCOUNT", "confirm_delay", fallback=1.0
        )

        self.launch_window_timeout = config.getfloat(
            "LAUNCH_VERIFICATION", "timeout", fallback=30.0
        )
        self.launch_window_poll_interval = config.getfloat(
            "LAUNCH_VERIFICATION", "poll_interval", fallback=0.5
        )
        self.launch_window_observations = config.getint(
            "LAUNCH_VERIFICATION", "required_observations", fallback=2
        )
        self.launch_window_min_width = config.getint(
            "LAUNCH_VERIFICATION", "min_window_width", fallback=300
        )
        self.launch_window_min_height = config.getint(
            "LAUNCH_VERIFICATION", "min_window_height", fallback=200
        )

        self.launch_delay = get_number(config, "DELAYS", "launch_delay")
        self.account_switch_delay = get_number(config, "DELAYS", "account_switch_delay")
        self.scroll_delay = get_number(config, "DELAYS", "scroll_delay")
        self.wait_after_dropdown_delay = get_number(config, "DELAYS", "wait_after_dropdown_delay")
        self.scroll_up_attempts_delay = get_number(config, "DELAYS", "scroll_up_attempts_delay")
        self.perv_count_delay = get_number(config, "DELAYS", "perv_count_delay")

    def load_accounts(self):
        self.accounts = []

        for account in load_account_definitions(
            resource_path("accounts/accounts.ini")
        ):
            if (
                self.selected_group
                and account.server != str(self.selected_group)
            ):
                continue

            if (
                self.selected_accounts
                and account.view_name not in self.selected_accounts
            ):
                continue

            self.accounts.append({"name": account.view_name})

    def activate_launcher(self):
        if self.launcher_hwnd and win32gui.IsWindow(self.launcher_hwnd):
            hwnd = self.launcher_hwnd
        else:
            hwnds = []

            def enum(window_hwnd, _):
                title = win32gui.GetWindowText(window_hwnd)
                normalized_title = title.casefold()
                if (
                    self.launcher_title.casefold() in normalized_title
                    or "vk play" in normalized_title
                    or "игровой центр" in normalized_title
                ):
                    left, top, right, bottom = win32gui.GetWindowRect(window_hwnd)
                    area = max(0, right - left) * max(0, bottom - top)
                    hwnds.append((area, window_hwnd))

            win32gui.EnumWindows(enum, None)

            if not hwnds:
                raise Exception("Launcher not found")

            _, hwnd = max(hwnds, key=lambda item: item[0])
            self.launcher_hwnd = hwnd
            _, self.launcher_pid = win32process.GetWindowThreadProcessId(hwnd)

        win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
        win32gui.ShowWindow(hwnd, win32con.SW_MAXIMIZE)
        win32gui.BringWindowToTop(hwnd)

        for _ in range(3):
            try:
                win32api.keybd_event(win32con.VK_MENU, 0, 0, 0)
                win32gui.SetForegroundWindow(hwnd)
            finally:
                win32api.keybd_event(
                    win32con.VK_MENU, 0, win32con.KEYEVENTF_KEYUP, 0
                )

            time.sleep(0.3)
            if self.is_launcher_active():
                title = win32gui.GetWindowText(hwnd)
                self.debug(f"Launcher active: {title} (HWND {hwnd})")
                return hwnd

            win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
            win32gui.BringWindowToTop(hwnd)

        foreground = win32gui.GetForegroundWindow()
        foreground_title = win32gui.GetWindowText(foreground) if foreground else ""
        raise Exception(
            f"Cannot activate launcher; foreground is '{foreground_title}' (HWND {foreground})"
        )

    def is_launcher_active(self):
        if not self.launcher_pid:
            return False

        foreground = win32gui.GetForegroundWindow()
        if not foreground:
            return False

        try:
            _, foreground_pid = win32process.GetWindowThreadProcessId(foreground)
        except Exception:
            return False

        return foreground_pid == self.launcher_pid

    def ensure_launcher_active(self):
        if not self.is_launcher_active():
            self.debug("Launcher is not active; restoring foreground")
            self.activate_launcher()

    def find_account(self, account_name):
        for attempt in range(self.search_attempts):

            if self.should_stop():
                return None

            try:
                screenshot = pyautogui.screenshot(region=self.search_region)
                lines = self.account_recognizer.recognize(screenshot)
                match = find_matching_line(
                    account_name,
                    lines,
                    min_confidence=self.ocr_min_confidence,
                    fuzzy_threshold=self.ocr_fuzzy_threshold,
                )
                if match:
                    center_x, center_y = match.center
                    screen_x = self.search_region[0] + center_x
                    screen_y = self.search_region[1] + center_y
                    self.debug(f"✅ OCR: {match.text} ({match.confidence:.0%})")
                    return (round(screen_x), round(screen_y))

                recognized = [
                    line.text
                    for line in lines
                    if line.confidence >= self.ocr_min_confidence
                ]
                visible_names = ", ".join(recognized) if recognized else "nothing"
                self.debug(
                    f"OCR scan {attempt + 1}/{self.search_attempts}: {visible_names}"
                )
            except Exception as e:
                self.log(f"⚠️ {e}")

            if self.should_stop():
                return None

            pyautogui.moveTo(*self.scroll_position, duration=0.2)
            time.sleep(0.2)
            self.debug(f"Scrolling down: {self.search_scroll} steps")
            try:
                self.scroll_account_list(-self.search_scroll)
            except Exception as exc:
                self.log(f"⚠️ Cannot scroll account list: {exc}")
                return None
            time.sleep(self.scroll_delay)

        return None

    def is_account_selected(self, account_name):
        try:
            screenshot = pyautogui.screenshot(region=self.current_account_region)
            screenshot = screenshot.resize(
                (screenshot.width * 3, screenshot.height * 3)
            )
            lines = self.account_recognizer.recognize(screenshot)
            match = find_matching_line(
                account_name,
                lines,
                min_confidence=self.ocr_min_confidence,
                fuzzy_threshold=self.ocr_fuzzy_threshold,
            )
            if match:
                self.log(f"✅ Selected account: {account_name}")
                self.debug(
                    f"Current account OCR match: {match.text} "
                    f"({match.confidence:.0%})"
                )
                return True

            combined_match = find_exact_combined_match(
                account_name,
                lines,
                min_confidence=self.ocr_min_confidence,
            )
            if combined_match:
                recognized_parts = " + ".join(
                    repr(line.text) for line in combined_match
                )
                self.log(f"✅ Selected account: {account_name}")
                self.debug(
                    f"Current account OCR combined match: {recognized_parts}"
                )
                return True

            recognized = ", ".join(
                f"{line.text} ({line.confidence:.0%})" for line in lines
            ) or "nothing"
            self.debug(f"Current account OCR for '{account_name}': {recognized}")
        except Exception as exc:
            self.log(f"⚠️ Current account OCR failed: {exc}")

        return False

    def wait_for_account_selected(self, account_name):
        attempts = max(1, int(self.selection_confirm_attempts))

        for attempt in range(attempts):
            if self.should_stop():
                return False
            if self.is_account_selected(account_name):
                return True

            self.debug(
                f"Account confirmation attempt {attempt + 1}/{attempts} failed: "
                f"{account_name}"
            )
            if attempt + 1 < attempts:
                time.sleep(max(0, self.selection_confirm_delay))

        return False

    def scroll_account_list(self, steps):
        self.ensure_launcher_active()
        x, y = self.scroll_position
        hwnd = win32gui.WindowFromPoint((x, y))
        if not hwnd:
            raise Exception(f"Scroll target not found at ({x}, {y})")

        _, target_pid = win32process.GetWindowThreadProcessId(hwnd)
        if target_pid != self.launcher_pid:
            target_class = win32gui.GetClassName(hwnd)
            raise Exception(
                f"Scroll target is not VK Play: {target_class} "
                f"(HWND {hwnd}, PID {target_pid})"
            )

        if hwnd != self._last_scroll_hwnd:
            self._last_scroll_hwnd = hwnd
            self.debug(
                f"Native scroll target: {win32gui.GetClassName(hwnd)} (HWND {hwnd})"
            )

        steps = max(-100, min(100, int(steps)))
        wheel_delta = steps * win32con.WHEEL_DELTA
        wparam = win32api.MAKELONG(0, wheel_delta & 0xFFFF)
        lparam = win32api.MAKELONG(x, y)
        win32gui.PostMessage(hwnd, win32con.WM_MOUSEWHEEL, wparam, lparam)

    def get_client_window_candidates(self):
        windows = []
        excluded_pids = {os.getpid()}
        if self.launcher_pid:
            excluded_pids.add(self.launcher_pid)

        def enum(hwnd, _):
            if not win32gui.IsWindowVisible(hwnd):
                return
            title = win32gui.GetWindowText(hwnd).strip()
            if not title:
                return
            try:
                _, pid = win32process.GetWindowThreadProcessId(hwnd)
                left, top, right, bottom = win32gui.GetWindowRect(hwnd)
            except Exception:
                return
            width = max(0, right - left)
            height = max(0, bottom - top)
            if (
                pid in excluded_pids
                or width < self.launch_window_min_width
                or height < self.launch_window_min_height
            ):
                return
            windows.append(WindowInfo(hwnd, pid, title, width, height))

        win32gui.EnumWindows(enum, None)
        return windows

    def wait_for_new_client_window(self, baseline_hwnds):
        deadline = time.monotonic() + max(1, self.launch_window_timeout)
        required = max(1, self.launch_window_observations)
        observations = {}
        self.debug(
            f"Waiting for a new client window; timeout="
            f"{self.launch_window_timeout:g}s, baseline={len(baseline_hwnds)}"
        )

        while time.monotonic() < deadline:
            if self.should_stop():
                return None

            candidates = find_new_windows(
                baseline_hwnds,
                self.get_client_window_candidates(),
            )
            visible_hwnds = {window.hwnd for window in candidates}
            observations = {
                hwnd: count
                for hwnd, count in observations.items()
                if hwnd in visible_hwnds
            }
            for window in candidates:
                observations[window.hwnd] = observations.get(window.hwnd, 0) + 1
                if observations[window.hwnd] >= required:
                    self.debug(
                        f"New client window: {window.title} "
                        f"(HWND {window.hwnd}, PID {window.pid}, "
                        f"{window.width}x{window.height})"
                    )
                    return window

            time.sleep(max(0.1, self.launch_window_poll_interval))

        return None

    def run(self):
        total = len(self.accounts)
        pending = self.accounts.copy()
        self.progress(0)

        for round_num in range(2):
            if not pending or self.should_stop():
                break

            self.debug(f"\n🔁 PASS {round_num+1}")
            next_pending = []

            for index, acc in enumerate(pending):

                if self.should_stop():
                    next_pending.extend((item, "stopped") for item in pending[index:])
                    break

                name = acc["name"]
                self.log(f"🔎 Searching account: {name}")

                try:
                    self.activate_launcher()
                except Exception as exc:
                    self.log(f"⚠️ Launcher activation failed: {exc}")
                    next_pending.append((acc, "launcher error"))
                    continue

                if not self.is_account_selected(name):
                    try:
                        self.ensure_launcher_active()
                        pyautogui.click(*self.dropdown)
                        time.sleep(self.wait_after_dropdown_delay)

                        pyautogui.moveTo(*self.scroll_position, duration=0.2)
                        time.sleep(0.2)
                        for _ in range(self.scroll_up_attempts):
                            self.scroll_account_list(self.scroll_up_amount)
                            time.sleep(self.scroll_up_attempts_delay)
                    except Exception as exc:
                        self.log(f"⚠️ Account list activation failed: {exc}")
                        next_pending.append((acc, "account list error"))
                        continue

                    found = self.find_account(name)

                    if not found:
                        if self.should_stop():
                            next_pending.extend(
                                (item, "stopped") for item in pending[index:]
                            )
                            break
                        self.log(f"❌ Account not found: {name}")
                        next_pending.append((acc, "not found"))
                        continue

                    self.log(f"✅ Account found: {name}")
                    pyautogui.click(found)
                    time.sleep(self.account_switch_delay)

                    if not self.wait_for_account_selected(name):
                        if self.should_stop():
                            next_pending.extend(
                                (item, "stopped") for item in pending[index:]
                            )
                            break
                        self.log(f"❌ Account selection not confirmed: {name}")
                        next_pending.append((acc, "selection not confirmed"))
                        continue

                try:
                    self.ensure_launcher_active()
                except Exception as exc:
                    self.log(f"⚠️ Launcher lost foreground before Play: {exc}")
                    next_pending.append((acc, "launcher foreground error"))
                    continue

                baseline_hwnds = {
                    window.hwnd for window in self.get_client_window_candidates()
                }
                pyautogui.click(*self.play_button)
                time.sleep(self.perv_count_delay)
                pyautogui.click(*self.open_new_client)

                client_window = self.wait_for_new_client_window(baseline_hwnds)
                if not client_window:
                    if self.should_stop():
                        next_pending.extend(
                            (item, "stopped") for item in pending[index:]
                        )
                        break
                    self.log(f"❌ Game window not detected: {name}")
                    next_pending.append((acc, "game window not detected"))
                    continue

                self.results["launched"].append(name)
                self.log(
                    f"🚀 Launch confirmed: {name} "
                    f"({len(self.results['launched'])}/{total})"
                )
                if total:
                    self.progress(len(self.results["launched"]) / total * 100)

                time.sleep(self.launch_delay)

            pending = [x[0] for x in next_pending]

        failure_reason = "stopped" if self.should_stop() else "not launched"
        for acc in pending:
            self.results["failed"].append((acc["name"], failure_reason))

        if self.finish_callback:
            self.finish_callback(self.results)
