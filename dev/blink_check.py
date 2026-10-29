"""Blink while walking: the hero walks (autopilot), Blink fires mid-step; the position must jump about 7 m."""
import json
import time

from playutil import call, start_play, stop_play
from lineup import invoke
from smoke import set_auto


def hero_position(entity):
    return json.loads(call("scene_get_entities_batch", {"entity_ids": [entity]}))["entities"][0]["position"]


def main():
    set_auto(True, "Mage", 20.0)
    start_play()
    time.sleep(0.5)
    invoke("DevInvulnerable", 1000.0)
    invoke("DevAutoplay", True, 1.0)
    time.sleep(1.5)
    hero = json.loads(call("scene_find_entities_batch", {"name_exact": "Hero_Mage", "limit": 1}))["entities"][0]["id"]
    results = []
    for attempt in range(3):
        before = hero_position(hero)
        print(invoke("DevActivate")[:40])
        time.sleep(0.15)
        after = hero_position(hero)
        jump = ((after[0] - before[0]) ** 2 + (after[2] - before[2]) ** 2) ** 0.5
        results.append(jump)
        print("attempt %d: moved %.2f m" % (attempt, jump))
        time.sleep(1.2)
    stop_play()
    set_auto(False)
    print("PASS" if all(j > 4.0 for j in results) else "FAIL", results)


if __name__ == "__main__":
    main()
