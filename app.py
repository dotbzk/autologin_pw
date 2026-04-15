import customtkinter as ctk
from threading import Thread
import os
import sys
from datetime import datetime
from tkinter import messagebox

from autologin_pw import GameLauncher

ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")


def resource_path(relative_path):
    base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)


class App(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("Game Launcher Bot")
        self.geometry("750x850")

        self.account_vars = {}
        self.stop_requested = False
        self.is_running = False

        self.build_ui()

        self.bind("<Escape>", lambda e: self.stop_bot())

    def log(self, text):
        ts = datetime.now().strftime("%H:%M:%S")
        self.log_box.insert("end", f"[{ts}] {text}\n")
        self.log_box.see("end")

    def build_ui(self):
        main = ctk.CTkFrame(self)
        main.pack(fill="both", expand=True)

        self.log_box = ctk.CTkTextbox(main)
        self.log_box.pack(fill="both", expand=True)

        self.progress = ctk.CTkProgressBar(main)
        self.progress.pack(fill="x")
        self.progress.set(0)

        self.run_button = ctk.CTkButton(main, text="RUN", command=self.run_bot)
        self.run_button.pack()

        ctk.CTkButton(main, text="STOP", fg_color="red", command=self.stop_bot).pack()

    def run_bot(self):
        if self.is_running:
            return

        self.is_running = True
        self.run_button.configure(state="disabled")

        def task():
            bot = GameLauncher(
                log_func=self.log,
                progress_func=self.update_progress,
                stop_flag=lambda: self.stop_requested
            )

            bot.finish_callback = lambda results: self.after(0, lambda: self.show_summary(results))

            bot.run()

            self.is_running = False
            self.run_button.configure(state="normal")

        Thread(target=task, daemon=True).start()

    def show_summary(self, results):
        launched = len(results["launched"])
        failed = results["failed"]
        total = launched + len(failed)

        if not failed:
            messagebox.showinfo("Done", f"✅ Done {launched}/{total}")
            return

        retry = messagebox.askyesno(
            "Failed accounts",
            f"Done {launched}/{total}\nRetry failed?"
        )

        if retry:
            self.retry_failed([x[0] for x in failed])

    def retry_failed(self, accounts):
        def task():
            bot = GameLauncher(
                selected_accounts=accounts,
                log_func=self.log,
                stop_flag=lambda: self.stop_requested
            )

            bot.finish_callback = lambda results: self.after(0, lambda: self.show_summary(results))

            bot.run()

        Thread(target=task, daemon=True).start()

    def stop_bot(self):
        self.stop_requested = True
        self.log("⛔ Stop requested")

    def update_progress(self, val):
        self.progress.set(val / 100)


if __name__ == "__main__":
    app = App()
    app.mainloop()