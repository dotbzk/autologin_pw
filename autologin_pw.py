import pyautogui
import time
import configparser
import os
import sys

import win32gui
import win32con


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
        base_path = os.path.abspath(".")

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
    def __init__(self, selected_accounts=None, selected_group=None, stop_flag=None, log_func=None, progress_func=None):

        self.selected_accounts = selected_accounts
        self.selected_group = selected_group

        self.stop_flag = stop_flag
        self.log = log_func if log_func else print
        self.progress = progress_func if progress_func else (lambda x: None)

        self.finish_callback = None

        self.results = {"launched": [], "failed": []}
        self._stop_requested = False

        self.load_config()
        self.load_accounts()

    def should_stop(self):
        if self.stop_flag and self.stop_flag():
            self._stop_requested = True
        return self._stop_requested

    def load_config(self):
        config = read_config_with_fallback(resource_path("configs/config.ini"))

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
        self.search_attempts = get_number(config, "SEARCH", "search_attempts")
        self.search_scroll = get_number(config, "SEARCH", "scroll")

        self.launch_delay = get_number(config, "DELAYS", "launch_delay")
        self.account_switch_delay = get_number(config, "DELAYS", "account_switch_delay")
        self.scroll_delay = get_number(config, "DELAYS", "scroll_delay")
        self.wait_after_dropdown_delay = get_number(config, "DELAYS", "wait_after_dropdown_delay")
        self.scroll_up_attempts_delay = get_number(config, "DELAYS", "scroll_up_attempts_delay")
        self.perv_count_delay = get_number(config, "DELAYS", "perv_count_delay")

    def load_accounts(self):
        config = read_config_with_fallback(resource_path("accounts/accounts.ini"))
        self.accounts = []

        for name, level in config["ACCOUNTS"].items():
            image = resource_path(f"Accounts/{level}/{name}.png")

            if not os.path.exists(image):
                continue

            if self.selected_group and str(level) != str(self.selected_group):
                continue

            if self.selected_accounts and name not in self.selected_accounts:
                continue

            self.accounts.append({"name": name, "image": image})

    def activate_launcher(self):
        hwnds = []

        def enum(hwnd, _):
            if win32gui.IsWindowVisible(hwnd):
                title = win32gui.GetWindowText(hwnd)
                if "vk play" in title.lower() or "игровой центр" in title.lower():
                    hwnds.append(hwnd)

        win32gui.EnumWindows(enum, None)

        if not hwnds:
            raise Exception("Launcher not found")

        hwnd = hwnds[0]
        win32gui.ShowWindow(hwnd, win32con.SW_MAXIMIZE)
        time.sleep(0.3)
        win32gui.SetForegroundWindow(hwnd)
        time.sleep(0.5)

    def find_account(self, image):
        for attempt in range(self.search_attempts):

            if self.should_stop():
                return None

            try:
                loc = pyautogui.locateOnScreen(image, region=self.search_region, confidence=0.92)
                if loc:
                    return pyautogui.center(loc)
            except Exception as e:
                self.log(f"⚠️ {e}")

            pyautogui.moveTo(*self.scroll_position)
            pyautogui.scroll(-self.search_scroll)
            time.sleep(self.scroll_delay)

        return None

    def run(self):
        total = len(self.accounts)
        pending = self.accounts.copy()

        for round_num in range(2):
            if not pending or self.should_stop():
                break

            self.log(f"\n🔁 PASS {round_num+1}")
            next_pending = []

            for acc in pending:

                if self.should_stop():
                    break

                name = acc["name"]
                self.log(f"🔎 {name}")

                try:
                    self.activate_launcher()
                except Exception:
                    next_pending.append((acc, "launcher error"))
                    continue

                pyautogui.click(*self.dropdown)
                time.sleep(self.wait_after_dropdown_delay)

                for _ in range(self.scroll_up_attempts):
                    pyautogui.scroll(self.search_scroll)
                    time.sleep(self.scroll_up_attempts_delay)

                found = self.find_account(acc["image"])

                if not found:
                    next_pending.append((acc, "not found"))
                    continue

                pyautogui.click(found)
                time.sleep(self.account_switch_delay)

                pyautogui.click(*self.play_button)
                time.sleep(self.perv_count_delay)
                pyautogui.click(*self.open_new_client)

                self.results["launched"].append(name)
                self.log(f"🚀 {name} ({len(self.results['launched'])}/{total})")

                time.sleep(self.launch_delay)

            pending = [x[0] for x in next_pending]

        for acc in pending:
            self.results["failed"].append((acc["name"], "not launched"))

        if self.finish_callback:
            self.finish_callback(self.results)