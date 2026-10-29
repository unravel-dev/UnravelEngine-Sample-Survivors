"""Performance with full evolved loadouts at normal speed: python perf_vfx.py [start_seconds]"""
import json
import sys
import time

from playutil import call, start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto

START = float(sys.argv[1]) if len(sys.argv) > 1 else 600.0
LOADOUTS = {
    "Mage": ["ember_orb", "starfall", "rime_nova", "fire_flask", "storm_totem", "hunting_wisps"],
    "Knight": ["oathblade_arc", "aegis_orbit", "verdict", "spirit_darts", "ricochet_disc", "bramble_halo"],
    "Barbarian": ["ravager_chop", "whirlwind", "fault_line", "skewer_javelin", "fire_flask", "storm_totem"],
    "Rogue": ["repeater_crossbow", "fan_of_knives", "assassins_mark", "spirit_darts", "hunting_wisps", "ricochet_disc"],
}


def main():
    for hero, weapons in LOADOUTS.items():
        set_auto(True, hero, START)
        start_play()
        time.sleep(0.4)
        invoke("DevInvulnerable", 1000.0)
        invoke("DevAutoplay", True, 1.0)
        invoke("DevWeapon", weapons[0], 8, True)
        invoke("DevFillSignature")
        call("scripts_invoke", {"entity_id": json.load(open("game_ids.json"))["Run"], "type_name": "Survivors.Run", "method": "DevSetTime", "arguments": [START]})
        for weapon in weapons[1:]:
            invoke("DevAddWeapon", weapon, 8, True)
        invoke("DevSpawns", True)
        invoke("DevSpawnMany", 900, 8.0, 40.0)
        time.sleep(3.0)
        samples = []
        for _ in range(20):
            time.sleep(1.0)
            st = json.loads(call("stats_get", {}))
            samples.append((st["frame"]["frame_ms"], st["frame"]["gpu_ms"], st["game"]["particles"]["drawn"], st["draw"]["draw_calls"]))
        capture_game(f"perf_{hero}", 1280, 720)
        avg = lambda k: sum(s[k] for s in samples) / len(samples)
        print("%-9s frame avg %5.2f max %5.2f | gpu avg %5.2f max %5.2f | particles avg %5d max %5d | draws max %d" % (
            hero, avg(0), max(s[0] for s in samples), avg(1), max(s[1] for s in samples), avg(2), max(s[2] for s in samples),
            max(s[3] for s in samples)))
        print("          ", json.loads(invoke("DevReport")).get("result", "")[:200])
        stop_play()
        set_auto(False)


if __name__ == "__main__":
    main()
