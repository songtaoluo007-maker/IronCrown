"""
Phase1-closeout G1: 截取 UI 截图
修复: 不 maximize, 直接用编辑器窗口区域, 禁 failsafe
"""
import pyautogui
import pygetwindow as gw
import subprocess
import time
import os
import sys

UNITY = r"C:\Program Files\Unity\Hub\Editor\6000.0.76f1\Editor\Unity.exe"
PROJECT = r"E:\IronCrown"
OUT = r"E:\IronCrown\Design\screenshots"
os.makedirs(OUT, exist_ok=True)
pyautogui.FAILSAFE = False  # 禁用 failsafe，手动控制安全

def find_win(substr, timeout=180):
    t0 = time.time()
    while time.time() - t0 < timeout:
        for w in gw.getAllWindows():
            if substr.lower() in w.title.lower() and w.width > 400:
                return w
        time.sleep(3)
    return None

def snap(name, region=None):
    p = os.path.join(OUT, f"{name}.png")
    pyautogui.screenshot(region=region).save(p)
    sz = os.path.getsize(p)
    print(f"  [{name}] {sz:,} bytes")

def main():
    print("Phase1 Screenshot Capture")
    print("=" * 50)

    # 1. 启动 Unity
    print("[1] Launching Unity...")
    subprocess.Popen([UNITY, "-projectPath", PROJECT])

    # 2. 等窗口
    print("[2] Waiting for editor...")
    win = find_win("IronCrown", timeout=180)
    if not win:
        print("ERROR: No Unity window"); sys.exit(1)
    win.activate()
    time.sleep(2)
    # 不 maximize — 直接用当前窗口位置
    left, top = win.left, win.top
    w, h = win.width, win.height
    region = (left, top, w, h)
    print(f"  Window: {w}x{h} at ({left},{top})")

    # 3. 等 asset import
    print("[3] Waiting for assets (40s)...")
    time.sleep(40)

    # 4. Play
    print("[4] Play mode (Ctrl+P)...")
    pyautogui.hotkey('ctrl', 'p')
    time.sleep(15)

    # 5. 截图
    print("[5] Capturing...")

    # 5a. 主 HUD
    # 点击窗口中央确保焦点
    cx, cy = left + w // 2, top + h // 2
    pyautogui.click(cx, cy)
    time.sleep(2)
    snap("phase1-rank-names", region)

    # 5b-5e: 尝试用 Tab 遍历 UI 按钮
    # UI Toolkit 按钮可通过 Tab/Enter 激活
    for name in ["phase1-gacha-panel", "phase1-collection", "phase1-shop", "phase1-saveload"]:
        pyautogui.press('tab')
        time.sleep(0.5)
        pyautogui.press('enter')
        time.sleep(2)
        snap(name, region)

    # 6. 退出
    print("[6] Stop Play...")
    pyautogui.hotkey('ctrl', 'p')
    time.sleep(3)

    print("\nDone!")
    for f in sorted(os.listdir(OUT)):
        if f.startswith("phase1") and f.endswith(".png"):
            print(f"  {f}")

if __name__ == "__main__":
    main()
