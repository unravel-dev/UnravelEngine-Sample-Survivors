"""Map event review: starts one event ahead of the hero and captures it waiting, under way and at its end.

python event_preview.py KIND [CLASS] [START_SECONDS] [W H]
KIND: Shrine, Robber, Pyres, Moonwell. Captures go to shots/event_<kind>_<moment>.png; the tracker's state and the
run report are printed at each moment.
"""
import json
import shutil
import sys
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto

KIND = sys.argv[1]
HERO = sys.argv[2] if len(sys.argv) > 2 else "Knight"
START = float(sys.argv[3]) if len(sys.argv) > 3 else 300.0
WIDTH = int(sys.argv[4]) if len(sys.argv) > 4 else 1600
HEIGHT = int(sys.argv[5]) if len(sys.argv) > 5 else 900


def capture(moment):
    result = call("viewport_capture_game", {"width": WIDTH, "height": HEIGHT})
    path = [l.split("image saved: ", 1)[1].strip() for l in result.splitlines() if "image saved:" in l][-1]
    shutil.copyfile(path, f"shots/event_{KIND.lower()}_{moment}.png")
    report = json.loads(invoke("DevReport")).get("result", "")
    print(moment, report[:220], flush=True)


def chase(seconds):
    """Keeps the hero 3 m behind the robber (a player giving chase)."""
    end = time.time() + seconds
    while time.time() < end:
        invoke("DevGoToEvent", 3.0)
        time.sleep(0.5)


def set_auto_pick(enabled):
    ids = json.load(open("game_ids.json"))
    call("scene_set_component_properties_batch", {"items": [{"entity_id": ids["Run"], "component": "Script", "type_name": "Survivors.Run",
                                                             "properties": {"devAutoPick": enabled}}]})


def main():
    stop_play()
    set_auto(True, HERO, START)
    set_auto_pick(True)
    start_play()
    time.sleep(1.5)
    if KIND == "Moonwell":
        invoke("DevHurt", 0.6)
    invoke("DevInvulnerable", 1000.0)
    invoke("DevAddWeapon", "oathblade_arc", 4, False)
    invoke("DevAddWeapon", "spirit_darts", 4, False)
    invoke("DevAddWeapon", "aegis_orbit", 3, False)
    invoke("DevEvent", KIND)
    time.sleep(1.2)
    capture("placed")
    if KIND in ("Shrine", "Moonwell"):
        invoke("DevGoToEvent", 0.0)
    elif KIND == "Pyres":
        invoke("DevGoToEvent", 3.0)
    if KIND == "Robber":
        chase(2.5)
    else:
        time.sleep(2.5)
    capture("early")
    if KIND == "Robber":
        chase(6.0)
    time.sleep(4.0)
    capture("mid")
    time.sleep(8.0)
    capture("late")
    time.sleep(6.0)
    capture("end")
    stop_play()
    set_auto(False)
    set_auto_pick(False)


main()
