import pyautogui
import time
import pygetwindow as gw
import configparser
import os
import sys

def read_config_with_fallback(path):
    encodings = ["utf-8", "utf-8-sig", "cp1251"]

    for enc in encodings:
        try:
            config = configparser.ConfigParser()
            config.read(path, encoding=enc)
            self.log(f"✅ Loaded {path} with {enc}")
            return config
        except Exception:
            continue

    raise Exception(f"❌ Cannot read config: {path}")

def resource_path(relative_path):
    if getattr(sys, 'frozen', False):
        # exe
        base_path = os.path.dirname(sys.executable)
    else:
        # dev
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)

class GameLauncher:
    def __init__(self, selected_accounts=None, selected_group=None, stop_flag=None, log_func=None, progress_func=None):
        self.base_dir = os.path.dirname(os.path.abspath(__file__))

        self.selected_accounts = selected_accounts
        self.selected_group = selected_group

        self.stop_flag = stop_flag
        self.log = log_func if log_func else print
        self.progress = progress_func if progress_func else (lambda x: None)

        self.load_config()
        self.load_accounts()
        self.prev_count = 0

    def load_config(self):
        path = resource_path("configs/config.ini")
        config = read_config_with_fallback(path)

        self.play_button = (
            int(config["COORDINATES"]["play_button_x"]),
            int(config["COORDINATES"]["play_button_y"])
        )

        self.dropdown = (
            int(config["COORDINATES"]["dropdown_x"]),
            int(config["COORDINATES"]["dropdown_y"])
        )

        self.open_new_client = (
            int(config["COORDINATES"]["open_new_client_x"]),
            int(config["COORDINATES"]["open_new_client_y"])
        )

        self.scroll_position = (
            int(config["COORDINATES"]["scroll_x"]),
            int(config["COORDINATES"]["scroll_y"])
        )

        self.search_region = (
            int(config["SEARCH"]["region_x"]),
            int(config["SEARCH"]["region_y"]),
            int(config["SEARCH"]["region_w"]),
            int(config["SEARCH"]["region_h"])
        )

        self.scroll_up_attempts = int(config["SEARCH"]["scroll_up_attempts"])
        self.search_attempts = int(config["SEARCH"]["search_attempts"])
        self.search_scroll = int(config["SEARCH"]["scroll"])

        self.click_delay = int(config["DELAYS"]["click_delay"])
        self.launch_delay = int(config["DELAYS"]["launch_delay"])
        self.account_switch_delay = int(config["DELAYS"]["account_switch_delay"])
        self.scroll_delay = int(config["DELAYS"]["scroll_delay"])
        self.win_1_delay = int(config["DELAYS"]["win1_delay"])
        self.wait_after_dropdown_delay = int(config["DELAYS"]["wait_after_dropdown_delay"])
        self.scroll_up_attempts_delay = int(config["DELAYS"]["scroll_up_attempts_delay"])
        self.perv_count_delay = int(config["DELAYS"]["perv_count_delay"])

        self.launcher_title = config["GENERAL"]["launcher_title"]

    def load_accounts(self):
        path = resource_path("accounts/accounts.ini")
        config = read_config_with_fallback(path)

        self.accounts = []

        for name, level in config["ACCOUNTS"].items():
            image_path = os.path.join(self.base_dir, "Accounts", str(level), f"{name}.png")

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

    def activate_launcher(self):
        windows = gw.getWindowsWithTitle(self.launcher_title)

        if not windows:
            self.log("❌ Launcher not found")
            return False

        windows[0].activate()
        time.sleep(self.click_delay)
        return True

    def scroll_down(self):
        pyautogui.moveTo(*self.scroll_position)
        pyautogui.scroll(-400)
        time.sleep(self.scroll_delay)

    def find_account(self, image):
        for attempt in range(self.search_attempts):
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

    def run(self):
        total = len(self.accounts)

        for i, acc in enumerate(self.accounts):

            # === STOP ===
            if self.stop_flag and self.stop_flag():
                self.log("⛔ Stopped by user")
                break

            # === PROGRESS ===
            if total > 0:
                progress_value = int((i / total) * 100)
                self.progress(progress_value)

            name = acc["name"]
            image = acc["image"]

            pyautogui.hotkey("win", "1")
            time.sleep(self.win_1_delay)

            self.log(f"\n🔎 {name}")

            if not self.activate_launcher():
                break

            pyautogui.click(self.dropdown)
            time.sleep(self.wait_after_dropdown_delay)

            for _ in range(self.scroll_up_attempts):
                if self.stop_flag and self.stop_flag():
                    self.log("⛔ Stopped during scroll")
                    return

                pyautogui.scroll(self.search_scroll)
                time.sleep(self.scroll_up_attempts_delay)

            found = self.find_account(image)

            if not found:
                self.log(f"❌ Not found: {name}")
                continue

            pyautogui.click(found)
            time.sleep(self.account_switch_delay)

            pyautogui.click(self.play_button)

            if self.prev_count > 0:
                time.sleep(self.perv_count_delay)
                pyautogui.click(self.open_new_client)

            self.log(f"▶️ Launching {name}")

            for _ in range(int(self.launch_delay)):
                if self.stop_flag and self.stop_flag():
                    self.log("⛔ Stopped during launch wait")
                    return
                time.sleep(1)

            self.prev_count += 1

            self.log(f"🚀 Launched: {name}")

        # === FINISH ===
        self.progress(100)
        self.log("\n🎉 All accounts launched")