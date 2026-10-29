"""Class-update core review: chest beacons in a crowd, traits, boons on the pause sheet and level-up cards.
python core_preview.py TAG [W H]"""
import json
import sys
import time

from playutil import start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto

TAG = sys.argv[1] if len(sys.argv) > 1 else "core"
W = int(sys.argv[2]) if len(sys.argv) > 2 else 1600
H = int(sys.argv[3]) if len(sys.argv) > 3 else 900


def main():
    stop_play()
    set_auto(True, "Knight", 300.0)
    start_play()
    time.sleep(2.0)
    invoke("DevInvulnerable", 10000.0)
    invoke("DevSpawns", False)
    invoke("DevCrowd", 160, 4.0, 13.0, 60.0, 360.0)
    time.sleep(0.8)
    invoke("DevDropChest", 1, 6.0)
    invoke("DevDropChest", 5, -6.0)
    time.sleep(1.6)
    print(capture_game(f"{TAG}_beacon", W, H))
    invoke("DevAutoplay", True, 1.0)
    for passive in ("iron_heart", "plate_pauldron", "wide_sigil", "tome_of_insight", "keen_eye", "windstride_boots"):
        invoke("DevPassive", passive, 5)
    invoke("DevBoon", "boon_edge", 6)
    invoke("DevBoon", "boon_fleetfoot", 3)
    invoke("DevBoon", "boon_precision", 2)
    invoke("DevSpawns", True)
    time.sleep(12.0)
    print(capture_game(f"{TAG}_traits", W, H))
    print(json.loads(invoke("DevReport")).get("result"))
    invoke("DevAutoplay", False, 1.0)
    time.sleep(0.5)
    invoke("DevScreen", "pause")
    time.sleep(0.8)
    print(capture_game(f"{TAG}_pause", W, H))
    stop_play()
    for i in range(3):
        start_play()
        time.sleep(1.2)
        invoke("DevBoon", "boon_edge", 4)
        invoke("DevScreen", "levelup")
        time.sleep(0.9)
        print(capture_game(f"{TAG}_levelup{i}", W, H))
        stop_play()
    set_auto(False)


main()
