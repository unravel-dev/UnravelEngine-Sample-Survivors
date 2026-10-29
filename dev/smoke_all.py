"""Smoke test of the whole game: every class plays with its class weapons evolved, then with the shared weapons, starts
each map event and fights a mini-boss, under the test autopilot at a raised speed. Prints every warning and error the
editor logged, and the run report of each pass.

python smoke_all.py [SPEED] [REAL_SECONDS_PER_PASS] [classes...]
"""
import json
import sys
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto, recent_errors

SPEED = float(sys.argv[1]) if len(sys.argv) > 1 else 4.0
SECONDS = float(sys.argv[2]) if len(sys.argv) > 2 else 20.0
CLASSES = sys.argv[3:] or ["Knight", "Barbarian", "Mage", "Rogue"]
EVENTS = ["Shrine", "Robber", "Pyres", "Moonwell"]
CLASS_WEAPONS = {
    "Knight": ["aegis_orbit", "verdict", "consecration", "dawn_hammer"],
    "Barbarian": ["whirlwind", "fault_line", "war_cry", "butchers_chains"],
    "Mage": ["starfall", "rime_nova", "arc_lash", "prismatic_ray"],
    "Rogue": ["fan_of_knives", "assassins_mark", "venom_shivs", "shadow_dancers"],
}
SHARED_WEAPONS = ["spirit_darts", "ricochet_disc", "bramble_halo", "skewer_javelin", "fire_flask"]
SHARED_REST = ["storm_totem", "hunting_wisps"]
# Starts just before the Gravewarden (5:00) so each pass also fights a mini-boss.
START_SECONDS = 280.0


def run_pass(hero, weapons, label):
    stop_play()
    set_auto(True, hero, START_SECONDS)
    _, after = recent_errors()
    start_play()
    time.sleep(0.5)
    invoke("DevAutoplay", True, SPEED)
    invoke("DevInvulnerable", 100000.0)
    for weapon in weapons:
        invoke("DevAddWeapon", weapon, 8, True)
    for index, kind in enumerate(EVENTS):
        time.sleep(SECONDS / (len(EVENTS) + 1))
        invoke("DevEvent", kind)
        invoke("DevGoToEvent", 0.0 if index % 2 == 0 else 3.0)
    time.sleep(SECONDS / (len(EVENTS) + 1))
    report = json.loads(invoke("DevReport")).get("result", "")
    lines, _ = recent_errors(after)
    stop_play()
    set_auto(False)
    print(f"== {hero} {label}: {report[:300]}", flush=True)
    for line in lines:
        print("   " + line[:400], flush=True)
    return len(lines)


def main():
    problems = 0
    for hero in CLASSES:
        problems += run_pass(hero, CLASS_WEAPONS[hero] + SHARED_WEAPONS[:1], "class weapons")
        problems += run_pass(hero, SHARED_WEAPONS[1:] + SHARED_REST, "shared weapons")
    print(f"warnings and errors: {problems}")


main()
