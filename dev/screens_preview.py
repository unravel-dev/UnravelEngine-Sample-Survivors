"""Walk every screen at a resolution and save half-size captures: python screens_preview.py WIDTH HEIGHT tag"""
import shutil
import sys
import time

from PIL import Image

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto

WIDTH = int(sys.argv[1]) if len(sys.argv) > 1 else 2340
HEIGHT = int(sys.argv[2]) if len(sys.argv) > 2 else 1080
TAG = sys.argv[3] if len(sys.argv) > 3 else "phone"
MENU = ["title", "heroselect", "armory", "settings"]
RUN = ["hud", "details", "levelup", "chest", "pause", "results"]


def capture(name):
    result = call("viewport_capture_game", {"width": WIDTH, "height": HEIGHT})
    path = [l.split("image saved: ", 1)[1].strip() for l in result.splitlines() if "image saved:" in l][-1]
    target = f"shots/ui_{TAG}_{name}.png"
    shutil.copyfile(path, target)
    return Image.open(target).resize((WIDTH // 2, HEIGHT // 2))


def main():
    tiles = []
    stop_play()
    set_auto(False)
    start_play()
    time.sleep(2.0)
    for screen in MENU:
        if screen != "title":
            invoke("DevScreen", screen)
            time.sleep(0.6)
        tiles.append((screen, capture(screen)))
        if screen != "title":
            stop_play()
            start_play()
            time.sleep(1.5)
    stop_play()
    set_auto(True, "Knight", 300.0)
    start_play()
    time.sleep(2.5)
    invoke("DevInvulnerable", 1000.0)
    invoke("DevAddWeapon", "spirit_darts", 3, False)
    invoke("DevAddWeapon", "fire_flask", 8, False)
    for screen in RUN:
        if screen != "hud":
            invoke("DevScreen", screen)
            time.sleep(0.7)
        tiles.append((screen, capture(screen)))
        if screen in ("details", "levelup", "chest", "pause"):
            stop_play()
            start_play()
            time.sleep(2.0)
            invoke("DevInvulnerable", 1000.0)
            invoke("DevAddWeapon", "spirit_darts", 3, False)
            invoke("DevAddWeapon", "fire_flask", 8, False)
    stop_play()
    set_auto(False)
    for name, image in tiles:
        image.save(f"shots/ui_{TAG}_{name}_small.png")
    print(" ".join(name for name, _ in tiles))


if __name__ == "__main__":
    main()
