import pyautogui
import time
import configparser
import os
import sys

import win32gui
import win32process
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
        raise Exception(f"❌ Invalid number in config: {section}.{key} = {value}")


class GameLauncher:
    def __init__(self, selected_accounts=None, selected_group=None, stop_flag=None, log_func=None, progress_func=None):
        if getattr(sys, 'frozen', False):
            self.base_dir = os.path.dirname(sys.executable)
        else:
            self.base_dir = os.path.dirname(os.path.abspath(__file__))

        self.selected_accounts = selected_accounts
        self.selected_group = selected_group

        self.stop_flag = stop_flag
        self.log = log_func if log_func else print
        self.progress = progress_func if progress_func else (lambda x: None)

        self.load_config()
        self.load_accounts()
        self.prev_count = 0

    # =========================
    # CONFIG
    # =========================
    def load_config(self):
        path = resource_path("configs/config.ini")
        config = read_config_with_fallback(path)

        self.play_button = (
            get_number(config, "COORDINATES", "play_button_x"),
            get_number(config, "COORDINATES", "play_button_y")
        )

        self.dropdown = (
            get_number(config, "COORDINATES", "dropdown_x"),
            get_number(config, "COORDINATES", "dropdown_y")
        )

        self.open_new_client = (
            get_number(config, "COORDINATES", "open_new_client_x"),
            get_number(config, "COORDINATES", "open_new_client_y")
        )

        self.scroll_position = (
            get_number(config, "COORDINATES", "scroll_x"),
            get_number(config, "COORDINATES", "scroll_y")
        )

        self.search_region = (
            get_number(config, "SEARCH", "region_x"),
            get_number(config, "SEARCH", "region_y"),
            get_number(config, "SEARCH", "region_w"),
            get_number(config, "SEARCH", "region_h")
        )

        self.scroll_up_attempts = get_number(config, "SEARCH", "scroll_up_attempts")
        self.search_attempts = get_number(config, "SEARCH", "search_attempts")
        self.search_scroll = get_number(config, "SEARCH", "scroll")

        self.click_delay = get_number(config, "DELAYS", "click_delay")
        self.launch_delay = get_number(config, "DELAYS", "launch_delay")
        self.account_switch_delay = get_number(config, "DELAYS", "account_switch_delay")
        self.scroll_delay = get_number(config, "DELAYS", "scroll_delay")
        self.wait_after_dropdown_delay = get_number(config, "DELAYS", "wait_after_dropdown_delay")
        self.scroll_up_attempts_delay = get_number(config, "DELAYS", "scroll_up_attempts_delay")
        self.perv_count_delay = get_number(config, "DELAYS", "perv_count_delay")

    # =========================
    # ACCOUNTS
    # =========================
    def load_accounts(self):
        path = resource_path("accounts/accounts.ini")
        config = read_config_with_fallback(path)

        self.accounts = []

        for name, level in config["ACCOUNTS"].items():
            image_path = resource_path(os.path.join("Accounts", str(level), f"{name}.png"))

            if not os.path.exists(image_path):
                continue

            if self.selected_group and str(level) != str(self.selected_group):
                continue

            if self.selected_accounts and name not in self.selected_accounts:
                continue

            self.accounts.append({
                "name": name,
                "level": level,
                "image": image_path
            })

    # =========================
    # ACTIVATE LAUNCHER
    # =========================
    def activate_launcher(self):
        hwnds = []

        def enum(hwnd, _):
            if not win32gui.IsWindowVisible(hwnd):
                return

            title = win32gui.GetWindowText(hwnd)

            if "vk play" in title.lower() or "игровой центр" in title.lower():
                hwnds.append(hwnd)

        win32gui.EnumWindows(enum, None)

        if not hwnds:
            raise Exception("Launcher not found")

        hwnd = hwnds[0]

        win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
        win32gui.SetForegroundWindow(hwnd)

        time.sleep(0.5)

        x1, y1, _, _ = win32gui.GetWindowRect(hwnd)
        pyautogui.click(x1 + 50, y1 + 50)

        return True

    # =========================
    # HELPERS
    # =========================
    def scroll_down(self):
        pyautogui.moveTo(*self.scroll_position)
        pyautogui.scroll(-int(self.search_scroll))
        time.sleep(self.scroll_delay)

    def find_account(self, image):
        for attempt in range(int(self.search_attempts)):
            try:
                location = pyautogui.locateOnScreen(
                    image,
                    region=self.search_region,
                    confidence=0.8
                )

                if location:
                    x, y = pyautogui.center(location)
                    self.log(f"✅ Found {image}")
                    return (x, y)

            except Exception as e:
                self.log(f"⚠️ Error finding {image}: {e}")

            self.log(f"🔄 Attempt {attempt + 1}")
            self.scroll_down()

        return None

    # =========================
    # MAIN
    # =========================
    def run(self):
        total = len(self.accounts)

        for i, acc in enumerate(self.accounts):

            if self.stop_flag and self.stop_flag():
                self.log("⛔ Stopped by user")
                break

            if total > 0:
                self.progress(int((i / total) * 100))

            name = acc["name"]
            image = acc["image"]

            self.log(f"\n🔎 {name}")

            if not self.activate_launcher():
                break

            pyautogui.moveTo(*self.dropdown)
            pyautogui.click()
            time.sleep(self.wait_after_dropdown_delay)

            for _ in range(int(self.scroll_up_attempts)):
                pyautogui.scroll(int(self.search_scroll))
                time.sleep(self.scroll_up_attempts_delay)

            found = self.find_account(image)

            if not found:
                self.log(f"❌ Not found: {name}")
                continue

            pyautogui.click(found)
            time.sleep(self.account_switch_delay)

            pyautogui.click(*self.play_button)

            if self.prev_count > 0:
                time.sleep(self.perv_count_delay)
                pyautogui.click(*self.open_new_client)

            self.log(f"▶️ Launching {name}")

            time.sleep(self.launch_delay)

            self.prev_count += 1

            self.log(f"🚀 Launched: {name}")

        self.progress(100)
        self.log("\n🎉 All accounts launched")