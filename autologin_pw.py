import pyautogui
import time
import pygetwindow as gw

# === НАСТРОЙКИ ===

PLAY_BUTTON = (1770, 183)
DROPDOWN = (2173, 71)
OPEN_NEW_CLIENT = (1087, 563)

SEARCH_REGION = (1800, 100, 600, 800)

ACCOUNTS = [
    "luk.png",
    "strazh.png",
    "prist.png"
]

# === ФУНКЦИИ ===

def activate_launcher():
    windows = gw.getWindowsWithTitle("Игровой центр")

    if not windows:
        print("❌ Лаунчер не найден")
        return False

    windows[0].activate()
    time.sleep(1)
    return True


def find_account(image):
    try:
        location = pyautogui.locateOnScreen(
            image,
            region=SEARCH_REGION,
            confidence=0.8
        )

        if location:
            x, y = pyautogui.center(location)
            return (x, y)

    except Exception as e:
        print("ImageSearch error:", e)

    return None


def scroll_down():
    pyautogui.moveTo(2000, 400)
    pyautogui.scroll(-400)
    time.sleep(0.5)

# === ОСНОВНОЙ ЦИКЛ ===

prev_count = 0

for img in ACCOUNTS:
    pyautogui.hotkey("win", "1")
    time.sleep(2)

    print(f"\n🔎 Ищем: {img}")

    if not activate_launcher():
        break

    # открыть dropdown
    pyautogui.click(DROPDOWN)
    time.sleep(1)

    # скролл вверх (сброс)
    for _ in range(3):
        pyautogui.scroll(500)
        time.sleep(0.3)

    found = None

    # ищем аккаунт
    for _ in range(7):
        found = find_account(img)

        if found:
            break

        scroll_down()

    if not found:
        print(f"❌ Не найден: {img}")
        continue

    pyautogui.click(found)
    time.sleep(10)

    # Play
    pyautogui.click(PLAY_BUTTON)
    if prev_count > 0:
        pyautogui.click(OPEN_NEW_CLIENT)
    time.sleep(20)
    prev_count =+1

    print(f"🚀 Запущен: {img}")


print("\n🎉 Все аккаунты запущены")