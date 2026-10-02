import customtkinter as ctk
import configparser
import ctypes
import json
from threading import Thread, current_thread, main_thread
from queue import Empty, Queue
import os
import re
import shutil
import sys
import tkinter as tk
import tempfile
from datetime import datetime
from tkinter import PhotoImage, messagebox
from PIL import Image, ImageGrab, ImageTk
import win32con
import win32gui

from accounts_config import (
    AccountDefinition,
    load_account_definitions,
    replace_account_group,
    write_account_definitions,
)
from autologin_pw import GameLauncher
from screen_settings import (
    POINT_NAMES,
    REGION_SECTIONS,
    rectangle_from_points,
    validate_screen_entries,
)
from update_support import UPDATE_NOTICE, find_update, installed_version, start_updater
from background_memory import start_background_cleaner, stop_background_cleaner

ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")

if sys.platform == "win32":
    ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(
        "autologin_pw.GameLauncherBot"
    )

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
        base_path = os.path.dirname(os.path.abspath(__file__))

    return os.path.join(base_path, relative_path)


class App(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("Game Launcher Bot")
        self.geometry("750x850")
        self.attributes("-topmost", True)

        self.config_path = resource_path("configs/config.ini")
        self.accounts_path = resource_path("accounts/accounts.ini")
        self.icon_png_path = resource_path("configs/ico/app.png")
        self.icon_ico_path = resource_path("configs/ico/app.ico")
        self.app_version = installed_version(resource_path("version.json"))

        if os.path.exists(self.icon_png_path):
            self.app_icon = PhotoImage(file=self.icon_png_path)
            self.iconphoto(True, self.app_icon)
        if sys.platform == "win32" and os.path.exists(self.icon_ico_path):
            self.iconbitmap(self.icon_ico_path)

        ui_config = read_config_with_fallback(self.config_path)
        configured_icon_size = ui_config.getint(
            "UI", "account_icon_size", fallback=32
        )
        self.account_icon_size = max(16, min(64, configured_icon_size))
        self.memory_cleanup_enabled = ui_config.getboolean(
            "MEMORY_CLEANUP", "enabled", fallback=False
        )
        self.class_options = (
            list(ui_config["LIST_OF_CLASSES"].keys())
            if "LIST_OF_CLASSES" in ui_config
            else []
        )

        self.account_vars = {}
        self.account_buttons = {}
        self.account_images = {}
        self.debug_enabled = False
        self.stop_requested = False
        self.worker_thread = None
        self.update_check_running = False
        self.closing = False
        self.background_ready = False
        self.background_preparing = False
        self.ui_queue = Queue()
        self.log_file_error_reported = False

        logs_dir = resource_path("logs")
        os.makedirs(logs_dir, exist_ok=True)
        started_at = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
        self.log_path = os.path.join(logs_dir, f"run_{started_at}.txt")

        self.load_accounts()
        self.build_ui()
        self.protocol("WM_DELETE_WINDOW", self.close_app)
        self.after(50, self.process_ui_queue)
        self.after(0, self.prepare_background_cleaner)
        self.after(500, self.show_update_changelog)
        self.after(10000, self.cleanup_update_temp)
        self.after(20000, self.cleanup_completed_stages)

    def prepare_background_cleaner(self):
        if self.closing:
            return
        self.background_preparing = True
        self.run_button.configure(state="disabled")
        self.update_button.configure(state="disabled")

        def task():
            try:
                stop_background_cleaner(resource_path(""))
            except Exception as exc:
                self.call_on_ui(self.background_prepared, str(exc))
            else:
                self.call_on_ui(self.background_prepared, None)

        Thread(target=task, daemon=True).start()

    def background_prepared(self, error):
        self.background_preparing = False
        if error:
            self.log(f"Cannot stop background cleaner: {error}")
            self.after(5000, self.prepare_background_cleaner)
            return
        self.background_ready = True
        self.run_button.configure(state="normal")
        self.update_button.configure(state="normal")
        self.signal_update_ready()
        self.after(1500, self.auto_check_updates)

    def auto_check_updates(self):
        if self.closing or not getattr(sys, "frozen", False):
            return
        try:
            config = read_config_with_fallback(self.config_path)
            enabled = config.getboolean("UPDATES", "auto_check", fallback=True)
        except Exception as exc:
            self.log(f"Cannot read update settings: {exc}")
            return
        if enabled:
            if self.worker_thread and self.worker_thread.is_alive():
                self.after(3000, self.auto_check_updates)
                return
            self.check_for_updates(automatic=True)

    def show_update_changelog(self):
        if self.closing:
            return
        notice_path = resource_path(UPDATE_NOTICE)
        has_notice = os.path.isfile(notice_path)
        if not has_notice and not os.environ.get("GAME_LAUNCHER_UPDATE_READY_FILE"):
            return
        try:
            # Older updaters do not write notices. Bundled notes cover that first upgrade.
            with open(notice_path if has_notice else resource_path("version.json"),
                      encoding="utf-8") as notice_file:
                notice = json.load(notice_file)
            if notice.get("version") != self.app_version:
                return
            notes = notice.get("notes") or "\n".join(
                f"• {change}" for change in notice.get("changes", [])
            ) or "The release author did not provide change notes."
            win = ctk.CTkToplevel(self)
            win.title(f"What's new in {self.app_version}")
            win.geometry("560x390")
            win.transient(self)
            ctk.CTkLabel(win, text=f"Updated to {self.app_version}",
                         font=ctk.CTkFont(size=20, weight="bold")).pack(padx=20, pady=15)
            text = ctk.CTkTextbox(win, wrap="word")
            text.pack(fill="both", expand=True, padx=20)
            text.insert("1.0", notes[:12000])
            text.configure(state="disabled")
            ctk.CTkButton(win, text="Got it", command=win.destroy).pack(pady=15)
            win.lift()
            if has_notice:
                os.remove(notice_path)
        except (OSError, ValueError, TypeError) as exc:
            self.log(f"Cannot display update notes: {exc}")

    def close_app(self):
        if self.closing:
            return
        self.closing = True
        self.stop_requested = True
        self.run_button.configure(state="disabled")
        self.update_button.configure(state="disabled")
        self.finish_close()

    def finish_close(self):
        if self.worker_thread and self.worker_thread.is_alive():
            self.after(100, self.finish_close)
            return
        if self.background_preparing:
            self.after(100, self.finish_close)
            return
        if not self.background_ready:
            self.destroy()
            return
        try:
            config = read_config_with_fallback(self.config_path)
            enabled = config.getboolean("MEMORY_CLEANUP", "background_enabled", fallback=False)
        except Exception as exc:
            self.close_failed(str(exc))
            return
        if not enabled:
            self.destroy()
            return

        def task():
            try:
                start_background_cleaner(resource_path(""))
            except Exception as exc:
                self.call_on_ui(self.close_failed, str(exc))
            else:
                self.call_on_ui(self.destroy)

        Thread(target=task, daemon=True).start()

    def close_failed(self, error):
        self.log(f"Background cleaner startup failed: {error}")
        messagebox.showwarning("Memory cleaner", f"Closing without background cleanup.\n{error}", parent=self)
        self.destroy()

    def signal_update_ready(self):
        marker = os.environ.get("GAME_LAUNCHER_UPDATE_READY_FILE")
        if not marker:
            return
        temporary_dir = os.environ.get("GAME_LAUNCHER_UPDATE_TEMP")
        if not temporary_dir or os.path.realpath(marker) != os.path.realpath(
            os.path.join(temporary_dir, "ready")
        ):
            return
        try:
            with open(marker, "w", encoding="utf-8") as ready_file:
                ready_file.write(self.app_version)
        except OSError:
            pass

    def cleanup_update_temp(self, attempts=12):
        path = os.environ.get("GAME_LAUNCHER_UPDATE_TEMP")
        if not path:
            return
        resolved = os.path.realpath(path)
        temp_root = os.path.realpath(tempfile.gettempdir())
        if (
            os.path.dirname(resolved) != temp_root
            or not os.path.basename(resolved).startswith("GameLauncherBot-update-")
        ):
            return
        try:
            shutil.rmtree(resolved)
            os.environ.pop("GAME_LAUNCHER_UPDATE_TEMP", None)
            os.environ.pop("GAME_LAUNCHER_UPDATE_READY_FILE", None)
        except OSError:
            if attempts > 1:
                self.after(5000, self.cleanup_update_temp, attempts - 1)

    def cleanup_completed_stages(self):
        install_parent = os.path.dirname(resource_path("version.json"))
        install_parent = os.path.dirname(install_parent)
        try:
            with os.scandir(install_parent) as entries:
                for entry in entries:
                    if (
                        entry.name.startswith("GameLauncherBot-stage-")
                        and entry.is_dir(follow_symlinks=False)
                        and os.path.isfile(os.path.join(entry.path, "completed"))
                    ):
                        shutil.rmtree(entry.path, ignore_errors=True)
        except OSError:
            pass

    # =========================
    # LOG
    # =========================
    def call_on_ui(self, callback, *args):
        if current_thread() is main_thread():
            callback(*args)
        else:
            self.ui_queue.put((callback, args))

    def process_ui_queue(self):
        try:
            while True:
                callback, args = self.ui_queue.get_nowait()
                callback(*args)
        except Empty:
            pass
        self.after(50, self.process_ui_queue)

    def log(self, text):
        self.call_on_ui(self._append_log, text)

    def _append_log(self, text):
        ts = datetime.now().strftime("%H:%M:%S")
        line = f"[{ts}] {text}\n"
        self.log_box.insert("end", line)
        self.log_box.see("end")

        try:
            with open(self.log_path, "a", encoding="utf-8") as log_file:
                log_file.write(line)
        except OSError as exc:
            if not self.log_file_error_reported:
                self.log_file_error_reported = True
                self.log_box.insert("end", f"[{ts}] Cannot write log file: {exc}\n")

    def finish_worker(self):
        self.worker_thread = None
        if not self.closing:
            self.run_button.configure(state="normal")

    # =========================
    # ACCOUNTS
    # =========================
    def load_accounts(self):
        self.accounts_by_group = {}
        for account in load_account_definitions(self.accounts_path):
            self.accounts_by_group.setdefault(account.server, []).append(account)

    def refresh_groups(self, selected_group=None):
        self.load_accounts()
        groups = list(self.accounts_by_group.keys())
        self.group_menu.configure(values=groups or [""])
        group = selected_group if selected_group in groups else (
            groups[0] if groups else ""
        )
        self.group_var.set(group)
        self.update_accounts(group)

    # =========================
    # UI
    # =========================
    def build_ui(self):
        main = ctk.CTkFrame(self)
        main.pack(fill="both", expand=True, padx=10, pady=10)

        # GROUP
        ctk.CTkLabel(main, text="Group").pack(anchor="w")

        self.group_var = ctk.StringVar()
        self.group_menu = ctk.CTkComboBox(
            main,
            variable=self.group_var,
            values=list(self.accounts_by_group.keys())
        )
        self.group_menu.pack(fill="x")
        self.group_menu.configure(command=self.update_accounts)

        self.accounts_frame = ctk.CTkScrollableFrame(main, height=320)
        self.accounts_frame.pack(fill="both", expand=True, pady=10)
        self.accounts_frame.grid_columnconfigure(0, weight=1)

        if self.accounts_by_group:
            first = list(self.accounts_by_group.keys())[0]
            self.group_var.set(first)
            self.update_accounts(first)

        # SELECT BUTTONS
        btn_frame = ctk.CTkFrame(main)
        btn_frame.pack(pady=5)

        ctk.CTkButton(btn_frame, text="Select All", command=self.select_all).pack(side="left", padx=5)
        ctk.CTkButton(btn_frame, text="Unselect All", command=self.unselect_all).pack(side="left", padx=5)

        # ACCOUNT MANAGEMENT / SETTINGS
        manage_frame = ctk.CTkFrame(main)
        manage_frame.pack(pady=5)
        ctk.CTkButton(
            manage_frame,
            text="👥 Manage Groups",
            command=self.open_manage_groups,
        ).pack(side="left", padx=5)
        ctk.CTkButton(
            manage_frame,
            text="⚙ Settings",
            command=self.open_settings,
        ).pack(side="left", padx=5)
        self.update_button = ctk.CTkButton(
            manage_frame,
            text="Update",
            command=self.check_for_updates,
        )
        self.update_button.pack(side="left", padx=5)

        # RUN OPTIONS
        options_frame = ctk.CTkFrame(main)
        options_frame.pack(fill="x", pady=5)
        self.memory_cleanup_var = ctk.BooleanVar(value=self.memory_cleanup_enabled)
        ctk.CTkCheckBox(
            options_frame,
            text="Clean memory before each client",
            variable=self.memory_cleanup_var,
            command=self.save_memory_cleanup_setting,
        ).pack(anchor="w", padx=10, pady=8)

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

        self.run_button = ctk.CTkButton(run_frame, text="RUN", command=self.run_bot)
        self.run_button.pack(side="left", padx=5)
        ctk.CTkButton(run_frame, text="STOP", fg_color="red", command=self.stop_bot).pack(side="left", padx=5)
        self.debug_button = ctk.CTkButton(
            run_frame,
            text="DEBUG: OFF",
            width=110,
            command=self.toggle_debug,
        )
        self.debug_button_default_color = self.debug_button.cget("fg_color")
        self.debug_button.pack(side="left", padx=5)

    def toggle_debug(self):
        self.debug_enabled = not self.debug_enabled
        self.debug_button.configure(
            text="DEBUG: ON" if self.debug_enabled else "DEBUG: OFF",
            fg_color=(
                "#b45309" if self.debug_enabled
                else self.debug_button_default_color
            ),
        )
        self.log(f"Debug logging {'enabled' if self.debug_enabled else 'disabled'}")

    def check_for_updates(self, automatic=False):
        if self.closing or self.update_check_running or not self.background_ready:
            return
        if not getattr(sys, "frozen", False):
            messagebox.showinfo("Update", "Updates are available in the built application.", parent=self)
            return
        if self.worker_thread and self.worker_thread.is_alive():
            messagebox.showwarning("Update", "Stop the current run before updating.", parent=self)
            return
        self.update_button.configure(state="disabled")
        self.update_check_running = True
        self.log("Checking for updates...")

        def task():
            try:
                release = find_update(self.app_version)
                self.call_on_ui(self.show_update_result, release, automatic)
            except Exception as exc:
                self.call_on_ui(self.show_update_error, str(exc), automatic)

        Thread(target=task, daemon=True).start()

    def show_update_error(self, message, automatic=False):
        self.update_check_running = False
        if self.closing:
            return
        self.update_button.configure(state="normal")
        self.log(f"Update check failed: {message}")
        if not automatic:
            messagebox.showerror("Update", message, parent=self)

    def show_update_result(self, release, automatic=False):
        self.update_check_running = False
        if self.closing:
            return
        self.update_button.configure(state="normal")
        if not release:
            if not automatic:
                messagebox.showinfo("Update", f"Version {self.app_version} is up to date.", parent=self)
            return
        if self.worker_thread and self.worker_thread.is_alive():
            if automatic:
                self.log(f"Version {release['version']} is available. Use Update after the current run.")
                return
            messagebox.showwarning("Update", "Stop the current run before updating.", parent=self)
            return
        approved = messagebox.askyesno(
            "Update",
            f"Version {release['version']} is available. Install and restart now?",
            parent=self,
        )
        if not approved:
            return
        try:
            start_updater(os.path.dirname(sys.executable), release)
        except Exception as exc:
            self.show_update_error(str(exc))
            return
        self.destroy()

    def debug_log(self, text):
        if self.debug_enabled:
            self.log(text)

    def save_memory_cleanup_setting(self):
        enabled = self.memory_cleanup_var.get()
        try:
            cfg = read_config_with_fallback(self.config_path)
            if "MEMORY_CLEANUP" not in cfg:
                cfg.add_section("MEMORY_CLEANUP")
            cfg["MEMORY_CLEANUP"]["enabled"] = "yes" if enabled else "no"
            with open(self.config_path, "w", encoding="utf-8") as config_file:
                cfg.write(config_file)
        except Exception as exc:
            self.memory_cleanup_var.set(not enabled)
            messagebox.showerror(
                "Memory Cleanup",
                f"Cannot save memory cleanup setting:\n{exc}",
                parent=self,
            )
            return

        self.log(f"Memory cleanup {'enabled' if enabled else 'disabled'}")

    # =========================
    # ACCOUNTS UI
    # =========================
    def account_icon_path(self, character_class):
        return resource_path(f"configs/classes/{character_class}.png")

    def create_account_image(self, character_class, size=None):
        image_path = self.account_icon_path(character_class)
        if not os.path.exists(image_path):
            return None

        with Image.open(image_path) as source_image:
            pil_image = source_image.convert("RGBA")

        image_size = size or self.account_icon_size
        return ctk.CTkImage(
            light_image=pil_image,
            dark_image=pil_image,
            size=(image_size, image_size),
        )

    def update_accounts(self, selected_group=None):
        for w in self.accounts_frame.winfo_children():
            w.destroy()

        group = selected_group or self.group_var.get()
        accounts = self.accounts_by_group.get(group, [])

        self.account_vars = {}
        self.account_buttons = {}
        self.account_images = {}

        for index, account in enumerate(accounts):
            var = ctk.BooleanVar()
            card_image = self.create_account_image(account.character_class)
            if card_image:
                self.account_images[account.view_name] = card_image

            button = ctk.CTkButton(
                self.accounts_frame,
                text=account.view_name,
                image=card_image,
                compound="left",
                anchor="w",
                height=max(48, self.account_icon_size + 16),
                corner_radius=8,
                border_width=1,
                command=lambda name=account.view_name: self.toggle_account(name),
            )
            button.grid(
                row=index,
                column=0,
                padx=6,
                pady=3,
                sticky="ew",
            )
            self.account_vars[account.view_name] = var
            self.account_buttons[account.view_name] = button
            self.refresh_account_card(account.view_name)

    def toggle_account(self, account_name):
        var = self.account_vars[account_name]
        var.set(not var.get())
        self.refresh_account_card(account_name)

    def refresh_account_card(self, account_name):
        selected = self.account_vars[account_name].get()
        button = self.account_buttons[account_name]
        button.configure(
            text=f"✓  {account_name}" if selected else f"    {account_name}",
            fg_color="#1f6aa5" if selected else "transparent",
            border_width=3 if selected else 1,
            border_color="#3b8ed0" if selected else "#555555",
        )

    def select_all(self):
        for account_name, var in self.account_vars.items():
            var.set(True)
            self.refresh_account_card(account_name)

    def unselect_all(self):
        for account_name, var in self.account_vars.items():
            var.set(False)
            self.refresh_account_card(account_name)

    # =========================
    # GROUP MANAGEMENT
    # =========================
    def open_manage_groups(self):
        if self.worker_thread and self.worker_thread.is_alive():
            messagebox.showwarning(
                "Manage Groups",
                "Stop the current run before editing groups.",
                parent=self,
            )
            return

        win = ctk.CTkToplevel(self)
        win.title("Manage Account Groups")
        win.geometry("760x650")
        win.transient(self)
        win.grab_set()
        win.attributes("-topmost", True)

        state = {"original_group": None}
        rows = []

        ctk.CTkLabel(
            win,
            text="Manage account groups",
            font=("Arial", 20, "bold"),
        ).pack(pady=(18, 10))

        selector = ctk.CTkFrame(win)
        selector.pack(fill="x", padx=18, pady=5)
        ctk.CTkLabel(selector, text="Open group", width=110).pack(
            side="left", padx=(10, 5), pady=10
        )
        selected_group_var = ctk.StringVar()
        group_selector = ctk.CTkComboBox(
            selector,
            variable=selected_group_var,
            values=list(self.accounts_by_group.keys()) or [""],
        )
        group_selector.pack(side="left", fill="x", expand=True, padx=5)

        group_form = ctk.CTkFrame(win)
        group_form.pack(fill="x", padx=18, pady=5)
        ctk.CTkLabel(group_form, text="Group name", width=110).pack(
            side="left", padx=(10, 5), pady=10
        )
        group_name_entry = ctk.CTkEntry(
            group_form,
            placeholder_text="kapela",
        )
        group_name_entry.pack(side="left", fill="x", expand=True, padx=5)

        header = ctk.CTkFrame(win, fg_color="transparent")
        header.pack(fill="x", padx=28, pady=(10, 0))
        ctk.CTkLabel(header, text="Account name", anchor="w").pack(
            side="left", fill="x", expand=True
        )
        ctk.CTkLabel(header, text="Class", width=180, anchor="w").pack(
            side="left", padx=(10, 50)
        )

        rows_frame = ctk.CTkScrollableFrame(win, height=330)
        rows_frame.pack(fill="both", expand=True, padx=18, pady=5)

        def remove_row(row_data):
            row_data["frame"].destroy()
            rows.remove(row_data)

        def add_account_row(account=None):
            row_frame = ctk.CTkFrame(rows_frame)
            row_frame.pack(fill="x", pady=3)

            name_entry = ctk.CTkEntry(
                row_frame,
                placeholder_text="account_group",
            )
            name_entry.pack(side="left", fill="x", expand=True, padx=8, pady=8)
            if account:
                name_entry.insert(0, account.view_name)

            class_var = ctk.StringVar(
                value=(
                    account.character_class if account
                    else (self.class_options[0] if self.class_options else "")
                )
            )
            class_menu = ctk.CTkComboBox(
                row_frame,
                variable=class_var,
                values=self.class_options or [""],
                width=180,
            )
            class_menu.pack(side="left", padx=5, pady=8)

            row_data = {
                "frame": row_frame,
                "name": name_entry,
                "class": class_var,
            }
            rows.append(row_data)
            ctk.CTkButton(
                row_frame,
                text="✕",
                width=36,
                fg_color="#a33a3a",
                command=lambda: remove_row(row_data),
            ).pack(side="left", padx=(3, 8), pady=8)

        def clear_rows():
            for row_data in rows[:]:
                row_data["frame"].destroy()
            rows.clear()

        def load_group(group_name):
            if not group_name or group_name not in self.accounts_by_group:
                return
            state["original_group"] = group_name
            selected_group_var.set(group_name)
            group_name_entry.delete(0, "end")
            group_name_entry.insert(0, group_name)
            clear_rows()
            for account in self.accounts_by_group[group_name]:
                add_account_row(account)
            delete_button.configure(state="normal")

        def new_group():
            state["original_group"] = None
            selected_group_var.set("")
            group_name_entry.delete(0, "end")
            clear_rows()
            add_account_row()
            delete_button.configure(state="disabled")
            group_name_entry.focus_set()

        def collect_group_accounts(group_name):
            if not re.fullmatch(r"[\w-]+", group_name):
                raise ValueError(
                    "Group name may contain only letters, numbers, _ and -."
                )

            accounts = []
            for row_number, row_data in enumerate(rows, start=1):
                view_name = row_data["name"].get().strip()
                if not view_name:
                    continue
                character_class = row_data["class"].get().strip().casefold()
                if not re.fullmatch(r"[\w-]+", view_name):
                    raise ValueError(
                        f"Invalid account name in row {row_number}: {view_name}"
                    )
                if character_class not in self.class_options:
                    raise ValueError(
                        f"Select a valid class in row {row_number}."
                    )
                if not os.path.exists(self.account_icon_path(character_class)):
                    raise ValueError(
                        f"Image not found for class '{character_class}'."
                    )
                accounts.append(
                    AccountDefinition(view_name, group_name, character_class)
                )

            if not accounts:
                raise ValueError("Add at least one account to the group.")
            return accounts

        def save_group():
            existing_groups = list(self.accounts_by_group.keys())
            original_group = state["original_group"]
            group_name = group_name_entry.get().strip()

            group_collision = any(
                existing.casefold() == group_name.casefold()
                and (
                    not original_group
                    or existing.casefold() != original_group.casefold()
                )
                for existing in existing_groups
            )
            if group_collision:
                messagebox.showerror(
                    "Manage Groups",
                    f"Group '{group_name}' already exists.",
                    parent=win,
                )
                return

            try:
                replacements = collect_group_accounts(group_name)
                all_accounts = [
                    account
                    for group_accounts in self.accounts_by_group.values()
                    for account in group_accounts
                ]
                updated = replace_account_group(
                    all_accounts,
                    original_group,
                    group_name,
                    replacements,
                )
                write_account_definitions(self.accounts_path, updated)
            except (OSError, ValueError) as exc:
                messagebox.showerror(
                    "Manage Groups", str(exc), parent=win
                )
                return

            self.refresh_groups(group_name)
            state["original_group"] = group_name
            groups = list(self.accounts_by_group.keys())
            group_selector.configure(values=groups or [""])
            selected_group_var.set(group_name)
            delete_button.configure(state="normal")
            self.log(f"✅ Group saved: {group_name} ({len(replacements)} accounts)")

        def delete_group():
            original_group = state["original_group"]
            if not original_group:
                return
            if not messagebox.askyesno(
                "Delete Group",
                f"Delete group '{original_group}' and all its accounts?",
                parent=win,
            ):
                return

            all_accounts = [
                account
                for group_accounts in self.accounts_by_group.values()
                for account in group_accounts
            ]
            try:
                updated = replace_account_group(
                    all_accounts, original_group, "", []
                )
                write_account_definitions(self.accounts_path, updated)
            except (OSError, ValueError) as exc:
                messagebox.showerror(
                    "Delete Group", str(exc), parent=win
                )
                return

            self.refresh_groups()
            groups = list(self.accounts_by_group.keys())
            group_selector.configure(values=groups or [""])
            self.log(f"🗑 Group deleted: {original_group}")
            if groups:
                load_group(groups[0])
            else:
                new_group()

        group_selector.configure(command=load_group)
        ctk.CTkButton(
            selector,
            text="＋ New Group",
            command=new_group,
        ).pack(side="left", padx=8)

        actions = ctk.CTkFrame(win)
        actions.pack(fill="x", padx=18, pady=(5, 15))
        ctk.CTkButton(
            actions,
            text="＋ Add Account Row",
            command=add_account_row,
        ).pack(side="left", padx=6, pady=8)
        ctk.CTkButton(
            actions,
            text="Save Group",
            command=save_group,
        ).pack(side="right", padx=6, pady=8)
        delete_button = ctk.CTkButton(
            actions,
            text="Delete Group",
            fg_color="#a33a3a",
            command=delete_group,
        )
        delete_button.pack(side="right", padx=6, pady=8)

        groups = list(self.accounts_by_group.keys())
        if groups:
            selected_group = self.group_var.get()
            load_group(selected_group if selected_group in groups else groups[0])
        else:
            new_group()

    # Legacy single-account dialog retained for compatibility.
    def open_add_account(self):
        win = ctk.CTkToplevel(self)
        win.title("Add Account")
        win.geometry("420x470")
        win.transient(self)
        win.grab_set()
        win.attributes("-topmost", True)

        ctk.CTkLabel(win, text="Add account", font=("Arial", 20, "bold")).pack(
            pady=(20, 12)
        )

        form = ctk.CTkFrame(win)
        form.pack(fill="x", padx=20, pady=5)

        ctk.CTkLabel(form, text="View name").pack(anchor="w", padx=12, pady=(12, 2))
        view_name_entry = ctk.CTkEntry(form, placeholder_text="luk_kapela")
        view_name_entry.pack(fill="x", padx=12, pady=(0, 10))

        ctk.CTkLabel(form, text="Server").pack(anchor="w", padx=12, pady=(2, 2))
        server_entry = ctk.CTkEntry(form, placeholder_text="kapela")
        server_entry.pack(fill="x", padx=12, pady=(0, 10))

        ctk.CTkLabel(form, text="Class").pack(anchor="w", padx=12, pady=(2, 2))
        class_var = ctk.StringVar(
            value=self.class_options[0] if self.class_options else ""
        )
        class_menu = ctk.CTkComboBox(
            form,
            variable=class_var,
            values=self.class_options or [""],
        )
        class_menu.pack(fill="x", padx=12, pady=(0, 12))

        preview = ctk.CTkLabel(win, text="")
        preview.pack(pady=10)

        def update_preview(selected_class):
            preview_image = self.create_account_image(selected_class, size=48)
            win.preview_image = preview_image
            preview.configure(
                image=preview_image,
                text="" if preview_image else "Image not found",
            )

        class_menu.configure(command=update_preview)
        if class_var.get():
            update_preview(class_var.get())

        def save_account():
            view_name = view_name_entry.get().strip()
            server = server_entry.get().strip()
            character_class = class_var.get().strip().casefold()

            if not view_name or not server or not character_class:
                messagebox.showerror(
                    "Add Account",
                    "View name, server, and class are required.",
                    parent=win,
                )
                return

            if not re.fullmatch(r"[\w-]+", view_name) or not re.fullmatch(
                r"[\w-]+", server
            ):
                messagebox.showerror(
                    "Add Account",
                    "View name and server may contain only letters, numbers, _ and -.",
                    parent=win,
                )
                return

            existing_names = {
                account.view_name.casefold()
                for accounts in self.accounts_by_group.values()
                for account in accounts
            }
            if view_name.casefold() in existing_names:
                messagebox.showerror(
                    "Add Account",
                    f"Account '{view_name}' already exists.",
                    parent=win,
                )
                return

            if character_class not in self.class_options:
                messagebox.showerror(
                    "Add Account",
                    "Select a class from LIST_OF_CLASSES.",
                    parent=win,
                )
                return

            if not os.path.exists(self.account_icon_path(character_class)):
                messagebox.showerror(
                    "Add Account",
                    f"Image not found for class '{character_class}'.",
                    parent=win,
                )
                return

            config = read_config_with_fallback(self.accounts_path)
            section = f"ACCOUNT:{view_name}"
            if config.has_section(section):
                messagebox.showerror(
                    "Add Account",
                    f"Section [{section}] already exists.",
                    parent=win,
                )
                return
            config.add_section(section)
            config[section]["view_name"] = view_name
            config[section]["server"] = server
            config[section]["class"] = character_class

            with open(self.accounts_path, "w", encoding="utf-8") as config_file:
                config.write(config_file)

            self.load_accounts()
            groups = list(self.accounts_by_group.keys())
            self.group_menu.configure(values=groups)
            self.group_var.set(server)
            self.update_accounts(server)
            self.log(f"✅ Account added: {view_name}")
            win.destroy()

        ctk.CTkButton(win, text="Save Account", command=save_account).pack(pady=10)

    # =========================
    # SETTINGS
    # =========================
    def screen_bounds(self):
        metrics = ctypes.windll.user32.GetSystemMetrics
        return 0, 0, metrics(0), metrics(1)

    def pick_screen_target(self, settings_window, entries, section, name=None):
        try:
            settings_window.grab_release()
        except tk.TclError:
            pass

        settings_window.withdraw()
        self.withdraw()

        def restore_windows():
            self.deiconify()
            settings_window.deiconify()
            settings_window.lift()
            settings_window.focus_force()
            settings_window.grab_set()

        def show_picker():
            try:
                left, top, screen_w, screen_h = self.screen_bounds()
                screenshot = ImageGrab.grab()
                if screenshot.size != (screen_w, screen_h):
                    raise RuntimeError("Screen capture size does not match display coordinates")
            except Exception as exc:
                restore_windows()
                messagebox.showerror("Select on screen", str(exc), parent=settings_window)
                return

            try:
                picker = tk.Toplevel(self)
                picker.overrideredirect(True)
                picker.geometry(f"{screen_w}x{screen_h}+0+0")
                picker.update_idletasks()
                win32gui.SetWindowPos(
                    picker.winfo_id(), win32con.HWND_TOPMOST,
                    left, top, screen_w, screen_h, win32con.SWP_SHOWWINDOW,
                )
                picker.screen_image = ImageTk.PhotoImage(screenshot)
            except Exception as exc:
                if "picker" in locals():
                    picker.destroy()
                restore_windows()
                messagebox.showerror("Select on screen", str(exc), parent=settings_window)
                return
            canvas = tk.Canvas(
                picker,
                cursor="crosshair",
                highlightthickness=0,
            )
            canvas.pack(fill="both", expand=True)
            canvas.create_image(0, 0, image=picker.screen_image, anchor="nw")
            state = {"start": None, "rect": None}

            def set_entry_value(key, value):
                entry = entries[(section, key)]
                entry.delete(0, "end")
                entry.insert(0, str(int(value)))

            def close_picker():
                picker.destroy()
                restore_windows()

            def on_press(event):
                state["start"] = (event.x, event.y)
                if name is None:
                    state["rect"] = canvas.create_rectangle(
                        event.x, event.y, event.x, event.y,
                        outline="#00ff88", width=3,
                    )

            def on_drag(event):
                if state["rect"] is None:
                    return
                start_x, start_y = state["start"]
                canvas.coords(
                    state["rect"],
                    start_x,
                    start_y,
                    event.x,
                    event.y,
                )

            def on_release(event):
                if state["start"] is None:
                    return
                end = (max(0, min(screen_w - 1, event.x)), max(0, min(screen_h - 1, event.y)))
                if name is not None:
                    set_entry_value(f"{name}_x", left + end[0])
                    set_entry_value(f"{name}_y", top + end[1])
                else:
                    x, y, width, height = rectangle_from_points(state["start"], end)
                    if width >= 5 and height >= 5:
                        set_entry_value("region_x", left + x)
                        set_entry_value("region_y", top + y)
                        set_entry_value("region_w", width)
                        set_entry_value("region_h", height)

                close_picker()

            picker.bind("<Escape>", lambda _event: close_picker())
            picker.protocol("WM_DELETE_WINDOW", close_picker)
            canvas.bind("<ButtonPress-1>", on_press)
            canvas.bind("<B1-Motion>", on_drag)
            canvas.bind("<ButtonRelease-1>", on_release)
            picker.grab_set()
            picker.focus_force()

        self.after(250, show_picker)

    def open_settings(self):
        win = ctk.CTkToplevel(self)
        win.title("Settings")
        win.geometry("600x600")

        win.transient(self)
        win.grab_set()

        win.after(10, lambda: win.lift())
        win.after(10, lambda: win.focus_force())
        win.after(10, lambda: win.attributes("-topmost", True))

        cfg = read_config_with_fallback(self.config_path)
        boolean_settings = {
            ("UPDATES", "auto_check"): ("Check for updates at startup", True),
            ("MEMORY_CLEANUP", "background_enabled"): (
                "Clean memory after closing the app (every 3 min)", False),
            ("MEMORY_CLEANUP", "enabled"): ("Clean memory before each client", False),
            ("MEMORY_CLEANUP", "trim_processes"): ("Trim process working sets", True),
            ("MEMORY_CLEANUP", "trim_file_cache"): ("Trim system file cache", True),
            ("MEMORY_CLEANUP", "purge_standby_list"): ("Purge standby memory", True),
        }
        for (section, key), (_, default) in boolean_settings.items():
            if not cfg.has_section(section):
                cfg.add_section(section)
            if not cfg.has_option(section, key):
                cfg[section][key] = "yes" if default else "no"
        entries = {}

        container = ctk.CTkScrollableFrame(win)
        container.pack(fill="both", expand=True, padx=10, pady=10)

        for section in cfg.sections():
            ctk.CTkLabel(container, text=f"[{section}]").pack(anchor="w", pady=(10, 0))

            point_names = {}
            if section == "COORDINATES":
                point_names = {
                    f"{name}_x": name for name in POINT_NAMES
                    if f"{name}_x" in cfg[section] and f"{name}_y" in cfg[section]
                }
            region_keys = ("region_x", "region_y", "region_w", "region_h")
            has_region = section in REGION_SECTIONS and all(
                key in cfg[section] for key in region_keys
            )

            def add_coordinate_entry(row, key, label):
                ctk.CTkLabel(row, text=label, width=18).pack(side="left")
                entry = ctk.CTkEntry(row, width=58)
                entry.insert(0, cfg[section][key])
                entry.pack(side="left", padx=(0, 6))
                entries[(section, key)] = entry

            for key, val in cfg[section].items():
                if key in {f"{name}_y" for name in point_names.values()}:
                    continue
                if has_region and key in region_keys[1:]:
                    continue

                row = ctk.CTkFrame(container)
                row.pack(fill="x", pady=2)

                if (section, key) in boolean_settings:
                    label, default = boolean_settings[(section, key)]
                    checkbox = ctk.CTkCheckBox(row, text=label, onvalue="yes", offvalue="no")
                    if cfg.getboolean(section, key, fallback=default):
                        checkbox.select()
                    checkbox.pack(anchor="w", padx=10, pady=8)
                    entries[(section, key)] = checkbox
                    continue

                if key in point_names:
                    name = point_names[key]
                    ctk.CTkLabel(
                        row, text=name.replace("_", " "), width=150, anchor="w",
                    ).pack(side="left", padx=(6, 0))
                    add_coordinate_entry(row, key, "X")
                    add_coordinate_entry(row, f"{name}_y", "Y")
                    ctk.CTkButton(
                        row, text="Select point", width=100,
                        command=lambda point_name=name, selected_section=section: self.pick_screen_target(
                            win, entries, selected_section, point_name,
                        ),
                    ).pack(side="left", padx=(2, 6))
                    continue

                if has_region and key == "region_x":
                    ctk.CTkLabel(row, text="Area", width=60, anchor="w").pack(
                        side="left", padx=(6, 0),
                    )
                    for region_key, label in zip(region_keys, ("X", "Y", "W", "H")):
                        add_coordinate_entry(row, region_key, label)
                    ctk.CTkButton(
                        row, text="Select area", width=100,
                        command=lambda selected_section=section: self.pick_screen_target(
                            win, entries, selected_section,
                        ),
                    ).pack(side="left", padx=(2, 6))
                    continue

                ctk.CTkLabel(row, text=key, width=200).pack(side="left")

                entry = ctk.CTkEntry(row)
                entry.insert(0, val)
                entry.pack(side="right", fill="x", expand=True)

                entries[(section, key)] = entry

        def save():
            values = {(section, key): entry.get() for (section, key), entry in entries.items()}
            temporary_path = None
            try:
                validate_screen_entries(values, self.screen_bounds())
                for (section, key), value in values.items():
                    cfg[section][key] = value
                with tempfile.NamedTemporaryFile(
                    mode="w", encoding="utf-8", dir=os.path.dirname(self.config_path),
                    prefix="config-", suffix=".tmp", delete=False,
                ) as temporary_file:
                    temporary_path = temporary_file.name
                    cfg.write(temporary_file)
                os.replace(temporary_path, self.config_path)
            except (OSError, ValueError) as exc:
                messagebox.showerror("Settings", str(exc), parent=win)
                return
            finally:
                if temporary_path and os.path.exists(temporary_path):
                    try:
                        os.remove(temporary_path)
                    except OSError:
                        pass

            self.log("✅ Config saved")
            self.memory_cleanup_enabled = cfg.getboolean("MEMORY_CLEANUP", "enabled", fallback=False)
            self.memory_cleanup_var.set(self.memory_cleanup_enabled)
            win.destroy()

        ctk.CTkButton(win, text="Save", command=save).pack(pady=10)

    # =========================
    # RUN
    # =========================
    def run_bot(self):
        if self.closing or not self.background_ready:
            return
        if self.worker_thread and self.worker_thread.is_alive():
            self.log("⚠️ Bot is already running")
            return

        self.stop_requested = False

        selected_accounts = [k for k, v in self.account_vars.items() if v.get()]
        group = self.group_var.get()

        if not selected_accounts:
            self.log("❌ No accounts selected")
            return

        self.log(f"🚀 Start | group={group} accounts={selected_accounts}")
        self.run_button.configure(state="disabled")

        def task():
            try:
                bot = GameLauncher(
                    selected_accounts=selected_accounts,
                    selected_group=group,
                    stop_flag=lambda: self.stop_requested,
                    log_func=self.log,
                    debug_log_func=self.debug_log,
                    progress_func=self.update_progress
                )

                bot.finish_callback = lambda results: self.call_on_ui(
                    self.show_summary, results, group
                )

                bot.run()

            except Exception as e:
                self.log(f"❌ ERROR: {e}")
            finally:
                self.call_on_ui(self.finish_worker)

        self.worker_thread = Thread(target=task, daemon=True)
        self.worker_thread.start()

    # =========================
    # SUMMARY + RETRY
    # =========================
    def show_summary(self, results, group):
        if self.closing:
            return
        launched = len(results["launched"])
        failed = results["failed"]
        total = launched + len(failed)

        win = ctk.CTkToplevel(self)
        win.title("Result")
        win.geometry("400x250")

        win.attributes("-topmost", True)

        win.transient(self)
        win.grab_set()

        win.update_idletasks()
        x = self.winfo_x() + (self.winfo_width() // 2 - 200)
        y = self.winfo_y() + (self.winfo_height() // 2 - 125)
        win.geometry(f"+{x}+{y}")

        # =========================
        # CONTENT
        # =========================
        text = f"✅ Done {launched}/{total}"

        if failed:
            text += f"\n\n❌ Failed: {len(failed)}"

        ctk.CTkLabel(win, text=text, font=("Arial", 16)).pack(pady=20)

        # =========================
        # BUTTONS
        # =========================
        btn_frame = ctk.CTkFrame(win)
        btn_frame.pack(pady=10)

        if failed:
            def retry():
                win.destroy()
                self.retry_failed([x[0] for x in failed], group)

            ctk.CTkButton(btn_frame, text="Retry", command=retry).pack(side="left", padx=10)

        def close():
            win.destroy()

        ctk.CTkButton(btn_frame, text="Close", command=close).pack(side="left", padx=10)

    def retry_failed(self, accounts, group):
        if self.worker_thread and self.worker_thread.is_alive():
            self.log("⚠️ Bot is already running")
            return

        self.stop_requested = False
        self.log(f"🔁 Retrying failed: {accounts}")
        self.run_button.configure(state="disabled")

        def task():
            try:
                bot = GameLauncher(
                    selected_accounts=accounts,
                    selected_group=group,
                    stop_flag=lambda: self.stop_requested,
                    log_func=self.log,
                    debug_log_func=self.debug_log,
                    progress_func=self.update_progress
                )

                bot.finish_callback = lambda results: self.call_on_ui(
                    self.show_summary, results, group
                )

                bot.run()

            except Exception as e:
                self.log(f"❌ ERROR (retry): {e}")
            finally:
                self.call_on_ui(self.finish_worker)

        self.worker_thread = Thread(target=task, daemon=True)
        self.worker_thread.start()

    def stop_bot(self):
        self.stop_requested = True
        self.log("⛔ Stop requested")

    def update_progress(self, val):
        self.call_on_ui(self._set_progress, val)

    def _set_progress(self, val):
        self.progress.set(val / 100)


if __name__ == "__main__":
    app = App()
    app.mainloop()
