"""Map event looks without the horde: python event_look.py KIND [SECONDS_BEFORE_CAPTURE] (shots/event_<kind>_look.png)."""
import json
import shutil
import sys
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto

KIND = sys.argv[1]
WAIT = float(sys.argv[2]) if len(sys.argv) > 2 else 1.5


def main():
    stop_play()
    set_auto(True, "Knight", 60.0)
    start_play()
    time.sleep(0.5)
    invoke("DevSpawns", False)
    invoke("DevInvulnerable", 1000.0)
    invoke("DevEvent", KIND)
    time.sleep(WAIT)
    result = call("viewport_capture_game", {"width": 1600, "height": 900})
    path = [l.split("image saved: ", 1)[1].strip() for l in result.splitlines() if "image saved:" in l][-1]
    shutil.copyfile(path, f"shots/event_{KIND.lower()}_look.png")
    print(json.loads(invoke("DevReport")).get("result", "")[:200])
    stop_play()
    set_auto(False)


main()
