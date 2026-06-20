🚀 GameLauncherBot
Automation tool for launching multiple game accounts via launcher UI.

📌 Overview

GameLauncherBot is a desktop application that automates:
•	Account switching in launcher
•	Launching multiple clients
•	Managing account groups
•	Editing configuration via UI

Built with:
•	Python
•	customtkinter (modern UI)
•	pyautogui (automation)

✨ Features
•	🎯 Select accounts by group
•	✅ Select All / Unselect All
•	⚙️ встроенные настройки (config.ini через UI)
•	📊 Progress bar
•	📝 Real-time logs
•	⛔ Stop execution anytime
•	🌙 Dark UI


🖥️ UI Preview
•	Group selector
•	Scrollable account list
•	Control buttons (Run / Stop)
•	Settings window
•	Log panel


Option 1 — EXE (recommended)
1.	Download compiled GameLauncherBot.exe
2.	Run — no installation required

Option 2 — Run from source
pip install -r requirements.txt
python app.py


📁 Project Structure

GameLauncherBot/
│
├── app.py
├── autologin_pw.py
│
├── configs/
│   └── config.ini
│
├── accounts/
│   ├── accounts.ini
│   └── <group>/
│       ├── acc1.png
│       └── acc2.png
│
└── Client
    ├── accounts/
    │   ├── accounts.ini
    │   └── <group>/
    │       ├── acc1.png
    │       └── acc2.png
    ├── configs/
    │   └── config.ini
    ├── _internal/
    └── GameLauncherBot.exe

⚙️ Configuration

Main config file:
configs/config.ini

Contains:
•	screen coordinates
•	delays
•	search regions

You can edit it:
•	manually
•	or via Settings UI


👤 Accounts

Defined in:
accounts/accounts.ini

Example:

[ACCOUNTS]
user1 = 29
user2 = 29


Images must exist:
accounts/29/user1.png

🚀 Usage
1.	Open game launcher
2.	Run the app
3.	Select group
4.	Select accounts
5.	Click RUN

⚠️ Requirements
•	Windows OS
•	Launcher must be running
•	Stable screen resolution
•	Do not move mouse during execution


❗ Common Issues

Launcher not found
•	Make sure launcher is open
•	Window title must match config

Account not found
•	Check account images
•	Ensure correct resolution


Wrong clicks
•	Update coordinates in config


🧠 Notes
•	Uses image recognition → sensitive to UI changes
•	Works best in fixed resolution
•	Not designed for background execution


📜 License
Private project / internal use

💬 Author
.bzk