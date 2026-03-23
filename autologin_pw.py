import pyautogui
import time
import pygetwindow as gw
import configparser
import os


class GameLauncher:
    def __init__(self):
        self.load_config()
        self.load_accounts()
        self.prev_count = 0

    # === CONFIG ===
    def load_config(self):
        config = configparser.ConfigParser()
        config.read("config.ini")

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

        self.click_delay = int(config["DELAYS"]["click_delay"])
        self.launch_delay = int(config["DELAYS"]["launch_delay"])
        self.account_switch_delay = int(config["DELAYS"]["account_switch_delay"])

        self.launcher_title = config["GENERAL"]["launcher_title"]

    # === ACCOUNTS ===
    def load_accounts(self):
        config = configparser.ConfigParser()
        config.read("accounts.ini")

        self.accounts = []

        for name, level in config["ACCOUNTS"].items():
            image_path = os.path.join("Accounts", str(level), f"{name}.png")

            if not os.path.exists(image_path):
                print(f"❌ Missing file: {image_path}")
                continue

            self.accounts.append({
                "name": name,
                "level": level,
                "image": image_path
            })

    # === ACTIONS ===

    def activate_launcher(self):
        windows = gw.getWindowsWithTitle(self.launcher_title)

        if not windows:
            print("❌ Launcher not found")
            return False

        windows[0].activate()
        time.sleep(self.click_delay)
        return True

    def scroll_down(self):
        pyautogui.moveTo(*self.scroll_position)
        pyautogui.scroll(-400)
        time.sleep(0.5)

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
                    print(f"✅ Found {image}")
                    return (x, y)

            except Exception as e:
                print(f"⚠️ Error finding {image}: {e}")

            print(f"🔄 Attempt {attempt + 1}")
            self.scroll_down()

        return None
    # === MAIN LOGIC ===

    def run(self):
        for acc in self.accounts:

            name = acc["name"]
            image = acc["image"]
            level = acc["level"]

            pyautogui.hotkey("win", "1")
            time.sleep(10)

            print(f"\n🔎 {name} (lvl {level})")

            if not self.activate_launcher():
                break

            pyautogui.click(self.dropdown)
            time.sleep(1)

            for _ in range(self.scroll_up_attempts):
                pyautogui.scroll(500)
                time.sleep(0.3)

            found = self.find_account(image)

            if not found:
                print(f"❌ Not found: {name}")
                continue

            pyautogui.click(found)
            time.sleep(self.account_switch_delay)

            pyautogui.click(self.play_button)

            if self.prev_count > 0:
                time.sleep(1)
                pyautogui.click(self.open_new_client)

            print(f"▶️ Launching {name}")

            time.sleep(self.launch_delay)

            self.prev_count += 1

            print(f"🚀 Launched: {name}")

        print("\n🎉 All accounts launched")


if __name__ == "__main__":
    bot = GameLauncher()
    bot.run()