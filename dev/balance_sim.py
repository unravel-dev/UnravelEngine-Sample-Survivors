"""Autopilot balance run of one class: python balance_sim.py CLASS GAME_SECONDS SPEED TAG [inv]
(inv: invulnerable, to compare damage output)
Prints a sample every 60 s of game time (level, hp, damage taken, healing by source) and a final line; appends the final
line to shots/balance.log."""
import json
import os
import re
import sys
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto

HERO = sys.argv[1]
GAME_SECONDS = float(sys.argv[2])
SPEED = float(sys.argv[3])
TAG = sys.argv[4] if len(sys.argv) > 4 else "sim"
INVULNERABLE = len(sys.argv) > 5 and sys.argv[5] == "inv"
HERE = os.path.dirname(os.path.abspath(__file__))


def report():
    return json.loads(invoke("DevReport")).get("result", "")


def field(text, name):
    m = re.search(name + r"=([^ ]+)", text)
    return m.group(1) if m else "?"


def main():
    stop_play()
    set_auto(True, HERO, 0.0)
    start_play()
    time.sleep(0.5)
    invoke("DevAutoplay", True, SPEED)
    if INVULNERABLE:
        invoke("DevInvulnerable", 100000.0)
    next_sample = 60.0
    lowest = 1.0
    last = ""
    deadline = time.time() + GAME_SECONDS / SPEED + 120.0
    while time.time() < deadline:
        time.sleep(1.0)
        last = report()
        phase = field(last, "phase")
        t = float(field(last, "t") or 0)
        hp = field(last, "hp")
        if "/" in hp:
            cur, mx = hp.split("/")
            lowest = min(lowest, float(cur) / max(1.0, float(mx)))
        if t >= next_sample:
            print(f"{HERO} t={t:5.0f} lvl={field(last, 'level')} hp={hp} kills={field(last, 'kills')} taken={field(last, 'taken')} "
                  f"healed={field(last, 'healed')} {last[last.find('['):last.find(']') + 1]}", flush=True)
            next_sample += 60.0
        if phase in ("Ended", "Results") or t >= GAME_SECONDS:
            break
    final = (f"{TAG} {HERO} end t={field(last, 't')} phase={field(last, 'phase')} lvl={field(last, 'level')} kills={field(last, 'kills')} "
             f"lowest={lowest:.0%} taken={field(last, 'taken')} healed={field(last, 'healed')} {last[last.find('['):last.find(']') + 1]} "
             f"weapons={last[last.find('weapons=') + 8:]}")
    print(final)
    with open(os.path.join(HERE, "shots", "balance.log"), "a") as f:
        f.write(final + "\n")
    stop_play()
    set_auto(False)


main()
