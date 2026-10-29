"""Armory + inspect panel captures at one preset: python armory_preview.py "PRESET" WIDTH HEIGHT tag"""
import shutil
import sys
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto

PRESET = sys.argv[1]
WIDTH = int(sys.argv[2])
HEIGHT = int(sys.argv[3])
TAG = sys.argv[4]


def capture(name):
    result = call("viewport_capture_game", {"width": WIDTH, "height": HEIGHT})
    path = [l.split("image saved: ", 1)[1].strip() for l in result.splitlines() if "image saved:" in l][-1]
    shutil.copyfile(path, f"shots/arm_{TAG}_{name}.png")


def restart():
    stop_play()
    start_play()
    time.sleep(1.5)


def main():
    print(call("settings_set", {"properties": {"resolution.current": PRESET}})[:120])
    stop_play()
    set_auto(False)
    start_play()
    time.sleep(2.0)
    invoke("DevScreen", "armory")
    time.sleep(0.6)
    capture("grid")
    invoke("DevArmory", 1, 0, 0)
    time.sleep(0.6)
    capture("inspect")
    invoke("DevArmory", 0, 0, 0)
    invoke("DevArmory", 11, 0, 1)
    time.sleep(0.6)
    capture("poor")
    invoke("DevArmory", 1, 2000, 2)
    time.sleep(0.12)
    capture("flash")
    time.sleep(0.8)
    capture("bought")
    invoke("DevArmory", 7, 0, 3)
    time.sleep(0.6)
    capture("maxed")
    invoke("DevScreen", "armory")
    time.sleep(0.6)
    capture("grid_after")
    stop_play()


main()
