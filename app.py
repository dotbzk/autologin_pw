import tkinter as tk
from tkinter import ttk
import configparser
from threading import Thread

from autologin_pw import GameLauncher


class App:
    def __init__(self, root):
        self.root = root
        self.root.title("Game Launcher Bot")

        self.config = configparser.ConfigParser()
        self.config.read("config.ini")

        self.entries = {}

        self.build_ui()

    def build_ui(self):
        row = 0

        # === GENERAL ===
        tk.Label(self.root, text="GENERAL", font=("Arial", 12, "bold")).grid(row=row, column=0)
        row += 1

        row = self.create_field("launcher_title", "GENERAL", row)

        # === DELAYS ===
        tk.Label(self.root, text="DELAYS", font=("Arial", 12, "bold")).grid(row=row, column=0)
        row += 1

        for key in self.config["DELAYS"]:
            row = self.create_field(key, "DELAYS", row)

        # === SEARCH ===
        tk.Label(self.root, text="SEARCH", font=("Arial", 12, "bold")).grid(row=row, column=0)
        row += 1

        for key in self.config["SEARCH"]:
            row = self.create_field(key, "SEARCH", row)

        # === BUTTON ===
        tk.Button(self.root, text="RUN", command=self.run_bot, bg="green", fg="white").grid(row=row, column=0, columnspan=2, pady=10)

    def create_field(self, key, section, row):
        tk.Label(self.root, text=key).grid(row=row, column=0, sticky="w")

        entry = tk.Entry(self.root, width=20)
        entry.insert(0, self.config[section][key])
        entry.grid(row=row, column=1)

        self.entries[(section, key)] = entry

        return row + 1

    def save_config(self):
        for (section, key), entry in self.entries.items():
            self.config[section][key] = entry.get()

        with open("config.ini", "w") as f:
            self.config.write(f)

    def run_bot(self):
        self.save_config()

        def task():
            bot = GameLauncher()
            bot.run()

        Thread(target=task).start()


# === ENTRY ===
if __name__ == "__main__":
    root = tk.Tk()
    app = App(root)
    root.mainloop()