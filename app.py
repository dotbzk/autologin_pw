import customtkinter as ctk
import configparser
from threading import Thread
import os
import sys
from datetime import datetime

from autologin_pw import GameLauncher

ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")


def read_config_with_fallback(path):
    encodings = ["utf-8", "utf-8-sig", "cp1251"]

    if not os.path.exists(path):
        raise Exception(f"❌ not found: {path}")

    for enc in encodings:
        try:
            config = configparser.ConfigParser()
            config.read(path, encoding=enc)
            return config
        except:
            continue

    raise Exception(f"Cannot read {path}")


def resource_path(relative_path):
    if getattr(sys, 'frozen', False):
        base_path = os.path.dirname(sys.executable)
    else:
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)


class App(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("Game Launcher Bot")
        self.geometry("750x850")

        self.config_path = resource_path("configs/config.ini")
        self.accounts_path = resource_path("accounts/accounts.ini")

        self.account_vars = {}
        self.stop_requested = False

        self.load_accounts()
        self.build_ui()

    # =========================
    # LOG
    # =========================
    def log(self, text):
        ts = datetime.now().strftime("%H:%M:%S")
        self.log_box.insert("end", f"[{ts}] {text}\n")
        self.log_box.see("end")

    # =========================
    # ACCOUNTS
    # =========================
    def load_accounts(self):
        config = read_config_with_fallback(self.accounts_path)
        self.accounts_by_group = {}

        if "ACCOUNTS" not in config:
            return

        for name, level in config["ACCOUNTS"].items():
            self.accounts_by_group.setdefault(level, []).append(name)

    # =========================
    # UI
    # =========================
    def build_ui(self):
        main = ctk.CTkFrame(self)
        main.pack(fill="both", expand=True, padx=10, pady=10)

        # GROUP
        ctk.CTkLabel(main, text="Group").pack(anchor="w")

        self.group_var = ctk.StringVar()
        self.accounts_frame = ctk.CTkScrollableFrame(main, height=200)
        self.accounts_frame.pack(fill="both", expand=True, pady=10)

        # GROUP
        self.group_var = ctk.StringVar()

        self.group_menu = ctk.CTkComboBox(
            main,
            variable=self.group_var,
            values=list(self.accounts_by_group.keys())
        )
        self.group_menu.pack(fill="x")

        self.group_menu.configure(command=self.update_accounts)

        if self.accounts_by_group:
            first = list(self.accounts_by_group.keys())[0]
            self.group_var.set(first)
            self.update_accounts(first)

        # SELECT BUTTONS
        btn_frame = ctk.CTkFrame(main)
        btn_frame.pack(pady=5)

        ctk.CTkButton(btn_frame, text="Select All", command=self.select_all).pack(side="left", padx=5)
        ctk.CTkButton(btn_frame, text="Unselect All", command=self.unselect_all).pack(side="left", padx=5)

        # SETTINGS
        ctk.CTkButton(main, text="⚙ Settings", command=self.open_settings).pack(pady=5)

        # LOG
        ctk.CTkLabel(main, text="LOG").pack(anchor="w")

        self.log_box = ctk.CTkTextbox(main, height=200)
        self.log_box.pack(fill="both", expand=True)

        # PROGRESS
        self.progress = ctk.CTkProgressBar(main)
        self.progress.pack(fill="x", pady=10)
        self.progress.set(0)

        # RUN/STOP
        run_frame = ctk.CTkFrame(main)
        run_frame.pack(pady=5)

        ctk.CTkButton(run_frame, text="RUN", command=self.run_bot).pack(side="left", padx=5)
        ctk.CTkButton(run_frame, text="STOP", fg_color="red", command=self.stop_bot).pack(side="left", padx=5)

    # =========================
    # ACCOUNTS UI
    # =========================
    def update_accounts(self, selected_group=None):
        for w in self.accounts_frame.winfo_children():
            w.destroy()

        group = selected_group or self.group_var.get()
        accounts = self.accounts_by_group.get(group, [])

        self.account_vars = {}

        for acc in accounts:
            var = ctk.BooleanVar()
            cb = ctk.CTkCheckBox(self.accounts_frame, text=acc, variable=var)
            cb.pack(anchor="w", pady=2)
            self.account_vars[acc] = var

    def select_all(self):
        for v in self.account_vars.values():
            v.set(True)

    def unselect_all(self):
        for v in self.account_vars.values():
            v.set(False)

    # =========================
    # SETTINGS
    # =========================
    def open_settings(self):
        win = ctk.CTkToplevel(self)
        win.title("Settings")
        win.geometry("600x600")

        cfg = read_config_with_fallback(self.config_path)
        entries = {}

        container = ctk.CTkScrollableFrame(win)
        container.pack(fill="both", expand=True, padx=10, pady=10)

        for section in cfg.sections():
            ctk.CTkLabel(container, text=f"[{section}]").pack(anchor="w", pady=(10, 0))

            for key, val in cfg[section].items():
                row = ctk.CTkFrame(container)
                row.pack(fill="x", pady=2)

                ctk.CTkLabel(row, text=key, width=200).pack(side="left")

                entry = ctk.CTkEntry(row)
                entry.insert(0, val)
                entry.pack(side="right", fill="x", expand=True)

                entries[(section, key)] = entry

        def save():
            for (section, key), entry in entries.items():
                cfg[section][key] = entry.get()

            with open(self.config_path, "w", encoding="utf-8") as f:
                cfg.write(f)

            self.log("✅ Config saved")
            win.destroy()

        ctk.CTkButton(win, text="Save", command=save).pack(pady=10)

    # =========================
    # RUN
    # =========================
    def run_bot(self):
        self.stop_requested = False

        selected_accounts = [k for k, v in self.account_vars.items() if v.get()]
        group = self.group_var.get()

        if not selected_accounts:
            self.log("❌ No accounts selected")
            return

        self.log(f"🚀 Start | group={group} accounts={selected_accounts}")

        def task():
            try:
                bot = GameLauncher(
                    selected_accounts=selected_accounts,
                    selected_group=group,
                    stop_flag=lambda: self.stop_requested,
                    log_func=self.log,
                    progress_func=self.update_progress
                )
                bot.run()
                self.log("✅ DONE")
                self.update_progress(1)

            except Exception as e:
                self.log(f"❌ ERROR: {e}")

        Thread(target=task, daemon=True).start()

    def stop_bot(self):
        self.stop_requested = True
        self.log("⛔ Stop requested")

    def update_progress(self, val):
        self.progress.set(val / 100)


if __name__ == "__main__":
    app = App()
    app.mainloop()