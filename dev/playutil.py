"""Play-mode helpers over the editor MCP: start play past the splash, capture to a named file, stop."""
import glob
import json
import os
import shutil
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

SHOTS = os.path.join(HERE, "shots")


def status():
    return json.loads(call("editor_get_status", {}))


def start_play(timeout=60.0):
    """Enters play and waits until the simulation runs and the splash is gone."""
    call("play_set_active", {"active": True})
    deadline = time.time() + timeout
    while time.time() < deadline:
        play = status()["play"]
        if play.get("is_simulation_running") and not play.get("is_splash"):
            return True
        time.sleep(0.25)
    return False


def stop_play():
    call("play_set_active", {"active": False})


def capture_game(name, width=1280, height=720):
    """Captures the game view into shots/<name>.png and returns the path."""
    os.makedirs(SHOTS, exist_ok=True)
    before = set(glob.glob(os.path.join(HERE, "..", "capture_viewport_capture_game_*.png")))
    result = call("viewport_capture_game", {"width": width, "height": height})
    paths = [line.split("image saved: ", 1)[1].strip() for line in result.splitlines() if "image saved:" in line]
    if not paths:
        print(result[:400])
        return None
    target = os.path.join(SHOTS, f"{name}.png")
    shutil.copyfile(paths[-1], target)
    return target
