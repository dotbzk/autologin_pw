import tkinter as tk
from tkinter import ttk
import configparser
from threading import Thread
import os
import sys

from autologin_pw import GameLauncher


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
        # exe
        base_path = os.path.dirname(sys.executable)
    else:
        # dev
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)


class App:
    def __init__(self, root):
        self.root = root
        self.root.title("Game Launcher Bot")
        self.root.geometry("700x800")
        self.root.configure(bg="#2b2b2b")

        try:
            self.root.iconbitmap("configs/ico/app.ico")
        except:
            print("⚠️ icon not loaded")

        self.config_path = resource_path("configs/config.ini")
        self.accounts_path = resource_path("accounts/accounts.ini")

        self.config = read_config_with_fallback(self.config_path)

        self.entries = {}
        self.account_vars = {}
        self.stop_requested = False

        self.load_accounts()
        self.build_ui()

    # =========================
    # ACCOUNTS LOAD
    # =========================
    def load_accounts(self):
        config = read_config_with_fallback(self.accounts_path)

        self.accounts_by_group = {}

        if "ACCOUNTS" not in config:
            self.log("⚠️ ACCOUNTS section missing")
            return

        for name, level in config["ACCOUNTS"].items():
            self.accounts_by_group.setdefault(level, []).append(name)

    # =========================
    # UI
    # =========================
    def build_ui(self):
        row = 0

        # GROUP
        tk.Label(self.root, text="Group", bg="#2b2b2b", fg="white").grid(row=row, column=0)

        self.group_var = tk.StringVar()
        self.group_menu = ttk.Combobox(
            self.root,
            textvariable=self.group_var,
            values=list(self.accounts_by_group.keys())
        )
        self.group_menu.grid(row=row, column=1)
        self.group_menu.bind("<<ComboboxSelected>>", self.update_accounts)

        row += 1

        # ACCOUNTS CHECKBOXES
        self.accounts_frame = tk.Frame(self.root, bg="#2b2b2b")
        self.accounts_frame.grid(row=row, column=0, columnspan=2)

        row += 1

        # =========================
        # ACCOUNT MANAGER
        # =========================
        tk.Label(self.root, text="Accounts Manager", bg="#2b2b2b", fg="white").grid(row=row, column=0)
        row += 1

        self.new_name = tk.Entry(self.root)
        self.new_name.grid(row=row, column=0)

        self.new_group = tk.Entry(self.root)
        self.new_group.grid(row=row, column=1)

        row += 1

        tk.Button(self.root, text="➕ Add", command=self.add_account, bg="blue", fg="white").grid(row=row, column=0)
        tk.Button(self.root, text="❌ Delete", command=self.delete_account, bg="red", fg="white").grid(row=row, column=1)

        row += 1

        # =========================
        # LOG
        # =========================
        tk.Label(self.root, text="LOG", bg="#2b2b2b", fg="white").grid(row=row, column=0)
        row += 1

        self.log_box = tk.Text(self.root, height=15, width=70, bg="#1e1e1e", fg="white")
        self.log_box.grid(row=row, column=0, columnspan=2)

        row += 1

        # PROGRESS
        self.progress = ttk.Progressbar(self.root, length=400)
        self.progress.grid(row=row, column=0, columnspan=2, pady=10)

        row += 1

        # BUTTONS
        tk.Button(self.root, text="RUN", bg="green", fg="white", command=self.run_bot).grid(row=row, column=0)
        tk.Button(self.root, text="STOP", bg="red", fg="white", command=self.stop_bot).grid(row=row, column=1)

    # =========================
    # LOG / HELPERS
    # =========================
    def log(self, text):
        self.log_box.insert(tk.END, text + "\n")
        self.log_box.see(tk.END)

    def update_progress(self, value):
        self.progress["value"] = value
        self.root.update_idletasks()

    def is_stopped(self):
        return self.stop_requested

    def refresh_groups(self):
        self.group_menu["values"] = list(self.accounts_by_group.keys())

        if self.group_var.get():
            self.update_accounts()

    # =========================
    # ACCOUNT MANAGEMENT
    # =========================
    def add_account(self):
        name = self.new_name.get().strip()
        group = self.new_group.get().strip()

        if not name or not group:
            self.log("❌ Enter name and group")
            return

        config = read_config_with_fallback(self.accounts_path)

        if "ACCOUNTS" not in config:
            config["ACCOUNTS"] = {}

        config["ACCOUNTS"][name] = group

        with open(self.accounts_path, "w", encoding="utf-8") as f:
            config.write(f)

        self.log(f"✅ Added: {name} -> {group}")

        self.load_accounts()
        self.refresh_groups()

    def delete_account(self):
        selected = [k for k, v in self.account_vars.items() if v.get()]

        if not selected:
            self.log("❌ Select account to delete")
            return

        config = read_config_with_fallback(self.accounts_path)

        for acc in selected:
            if acc in config["ACCOUNTS"]:
                del config["ACCOUNTS"][acc]
                self.log(f"🗑 Deleted: {acc}")

        with open(self.accounts_path, "w", encoding="utf-8") as f:
            config.write(f)

        self.load_accounts()
        self.update_accounts()

    # =========================
    # ACCOUNT UI
    # =========================
    def update_accounts(self, event=None):
        for w in self.accounts_frame.winfo_children():
            w.destroy()

        group = self.group_var.get()
        accounts = self.accounts_by_group.get(group, [])

        self.account_vars = {}

        for i, acc in enumerate(accounts):
            var = tk.BooleanVar()
            cb = tk.Checkbutton(
                self.accounts_frame,
                text=acc,
                variable=var,
                bg="#2b2b2b",
                fg="white",
                selectcolor="#444"
            )
            cb.grid(row=i, column=0, sticky="w")

            self.account_vars[acc] = var

    # =========================
    # RUN / STOP
    # =========================
    def run_bot(self):
        self.stop_requested = False

        selected_accounts = [k for k, v in self.account_vars.items() if v.get()]
        selected_group = self.group_var.get()

        self.log(f"🚀 Start | group={selected_group} accounts={selected_accounts}")

        def task():
            try:
                bot = GameLauncher(
                    selected_accounts=selected_accounts,
                    selected_group=selected_group,
                    stop_flag=self.is_stopped,
                    log_func=self.log,
                    progress_func=self.update_progress
                )
                bot.run()

                self.log("✅ DONE")
                self.update_progress(100)

            except Exception as e:
                self.log(f"❌ ERROR: {e}")

        Thread(target=task).start()

    def stop_bot(self):
        self.stop_requested = True
        self.log("⛔ Stop requested")


if __name__ == "__main__":
    root = tk.Tk()
    app = App(root)
    root.mainloop()