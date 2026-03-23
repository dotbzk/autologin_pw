import tkinter as tk
import configparser
from threading import Thread
import os

from autologin_pw import GameLauncher


def read_config_with_fallback(path):
    encodings = ["utf-8", "utf-8-sig", "cp1251"]

    for enc in encodings:
        try:
            config = configparser.ConfigParser()
            config.read(path, encoding=enc)
            return config
        except:
            continue

    raise Exception(f"Cannot read {path}")


class App:
    def __init__(self, root):
        self.root = root
        self.root.title("Game Launcher Bot")

        self.base_dir = os.path.dirname(os.path.abspath(__file__))
        self.config_path = os.path.join(self.base_dir, "config.ini")

        self.config = read_config_with_fallback(self.config_path)

        self.entries = {}
        self.build_ui()

    def build_ui(self):
        row = 0

        for section in self.config.sections():
            tk.Label(self.root, text=section, font=("Arial", 12, "bold")).grid(row=row, column=0)
            row += 1

            for key in self.config[section]:
                row = self.create_field(key, section, row)

        tk.Button(self.root, text="RUN", command=self.run_bot, bg="green").grid(row=row, column=0, columnspan=2)

    def create_field(self, key, section, row):
        tk.Label(self.root, text=key).grid(row=row, column=0)

        entry = tk.Entry(self.root)
        entry.insert(0, self.config[section][key])
        entry.grid(row=row, column=1)

        self.entries[(section, key)] = entry
        return row + 1

    def save_config(self):
        for (section, key), entry in self.entries.items():
            self.config[section][key] = entry.get()

        with open(self.config_path, "w", encoding="utf-8") as f:
            self.config.write(f)

    def run_bot(self):
        self.save_config()

        def task():
            bot = GameLauncher()
            bot.run()

        Thread(target=task).start()


if __name__ == "__main__":
    root = tk.Tk()
    app = App(root)
    root.mainloop()