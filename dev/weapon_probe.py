"""Damage of one weapon alone against a held crowd, at rank 8 and evolved: python weapon_probe.py HERO SECONDS id...

Each id is measured twice (rank 8, then evolved) with nothing else firing, so an evolution that deals less than its
rank-8 form stands out. Prints the damage per second of the weapon's slot.
"""
import json
import re
import sys
import time

from playutil import start_play, stop_play
from lineup import invoke
from smoke import set_auto

HERO = sys.argv[1]
SECONDS = float(sys.argv[2])
IDS = sys.argv[3:]
CROWD = 160
CROWD_INNER = 1.5
CROWD_OUTER = 10.0


def weapon_damage():
    report = json.loads(invoke("DevReport")).get("result", "")
    match = re.search(r"weapons=(\S+?)@([\d.]+)k", report)
    return (match.group(1), float(match.group(2)) * 1000.0) if match else ("?", 0.0)


def measure(weapon, evolve):
    invoke("DevWeapon", weapon, 8, evolve)
    invoke("DevCrowd", CROWD, CROWD_INNER, CROWD_OUTER, SECONDS + 5.0)
    _, before = weapon_damage()
    time.sleep(SECONDS)
    name, after = weapon_damage()
    return name, (after - before) / SECONDS


def main():
    stop_play()
    set_auto(True, HERO, 0.0)
    start_play()
    time.sleep(0.5)
    invoke("DevInvulnerable", 100000.0)
    for weapon in IDS:
        base_name, base = measure(weapon, False)
        evolved_name, evolved = measure(weapon, True)
        print(f"{weapon:18s} rank8 {base_name:22s} {base:8.0f}/s   evolved {evolved_name:22s} {evolved:8.0f}/s", flush=True)
    stop_play()
    set_auto(False)


main()
