"""Chest reveal captures (spinning, just landed, settled) at the current preset: python chest_preview.py W H tag"""
import shutil
import sys
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto

WIDTH = int(sys.argv[1])
HEIGHT = int(sys.argv[2])
TAG = sys.argv[3]


def capture(name):
    result = call("viewport_capture_game", {"width": WIDTH, "height": HEIGHT})
    path = [l.split("image saved: ", 1)[1].strip() for l in result.splitlines() if "image saved:" in l][-1]
    shutil.copyfile(path, f"shots/chest_{TAG}_{name}.png")


def main():
    stop_play()
    set_auto(True, "Knight", 300.0)
    start_play()
    time.sleep(2.5)
    invoke("DevInvulnerable", 1000.0)
    invoke("DevAddWeapon", "spirit_darts", 3, False)
    invoke("DevAddWeapon", "fire_flask", 6, False)
    invoke("DevScreen", "chest")
    time.sleep(0.8)
    capture("spin")
    time.sleep(3.2)
    capture("landed")
    time.sleep(1.5)
    capture("settled")
    stop_play()
    set_auto(False)


main()
