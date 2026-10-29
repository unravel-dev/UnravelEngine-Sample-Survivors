"""How hard a run is at a Dread level: python difficulty_sim.py CLASS DREAD GAME_SECONDS SPEED TAG [inv]

Plays with the test autopilot (inv: invulnerable) and prints each minute: level, health, enemies alive, kills, damage
taken. Enemies alive is the measure of pressure: a build that clears everything keeps it near the director's floor.
Appends the final line to shots/difficulty.log.
"""
import json
import os
import re
import sys
import time

from playutil import start_play, stop_play
from lineup import invoke
from smoke import set_auto

HERO, DREAD, GAME_SECONDS, SPEED, TAG = sys.argv[1], int(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), sys.argv[5]
INVULNERABLE = len(sys.argv) > 6 and sys.argv[6] == "inv"
HERE = os.path.dirname(os.path.abspath(__file__))


def field(text, name):
    match = re.search(name + r"=([^ ]+)", text)
    return match.group(1) if match else "?"


def main():
    stop_play()
    set_auto(True, HERO, 0.0)
    start_play()
    time.sleep(0.4)
    invoke("DevDread", DREAD)
    invoke("DevAutoplay", True, SPEED)
    if INVULNERABLE:
        invoke("DevInvulnerable", 100000.0)
    alive_samples = []
    next_minute = 60.0
    report = ""
    deadline = time.time() + GAME_SECONDS / SPEED + 120.0
    while time.time() < deadline:
        time.sleep(0.5)
        report = json.loads(invoke("DevReport")).get("result", "")
        t = float(field(report, "t") or 0)
        alive_samples.append(int(field(report, "alive")))
        if t >= next_minute:
            print(f"{HERO} d{DREAD} t={t:4.0f} lvl={field(report, 'level'):>3} hp={field(report, 'hp'):>8} "
                  f"alive={field(report, 'alive'):>5} kills={field(report, 'kills'):>6} taken={field(report, 'taken')}", flush=True)
            next_minute += 60.0
        if field(report, "phase") in ("Ended", "Results") or t >= GAME_SECONDS:
            break
    mean_alive = sum(alive_samples) / max(1, len(alive_samples))
    final = (f"{TAG} {HERO} d{DREAD} t={field(report, 't')} phase={field(report, 'phase')} lvl={field(report, 'level')} "
             f"kills={field(report, 'kills')} meanAlive={mean_alive:.0f} taken={field(report, 'taken')} "
             f"weapons={report[report.find('weapons=') + 8:]}")
    print(final)
    with open(os.path.join(HERE, "shots", "difficulty.log"), "a") as log:
        log.write(final + "\n")
    stop_play()
    set_auto(False)


main()
