"""Smoke test: auto-start a run, let it play, capture frames and collect errors from the log.

python smoke.py [class] [start_seconds] [play_seconds] [tag]
"""
import json
import os
import sys
import time

from playutil import call, start_play, stop_play, capture_game

HERE = os.path.dirname(os.path.abspath(__file__))


def set_auto(enabled, hero="Knight", start=0.0):
    ids = json.load(open(os.path.join(HERE, "game_ids.json")))
    print(call("scene_set_component_properties_batch", {"items": [{"entity_id": ids["Run"], "component": "Script", "type_name": "Survivors.Run",
                                                                  "properties": {"autoStart": enabled, "autoStartClass": hero, "autoStartTime": start}}]})[:200])


def recent_errors(after_id=0):
    result = json.loads(call("logs_get_recent", {"min_level": "warning", "max_count": 200, "after_id": after_id}))
    lines = []
    for entry in result.get("entries", []):
        text = entry["text"].strip().replace("\r", "")
        lines.append(f"{entry['level']}: {text[:300]}")
    return lines, result.get("next_after_id", after_id)


def main():
    hero = sys.argv[1] if len(sys.argv) > 1 else "Knight"
    start = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
    seconds = float(sys.argv[3]) if len(sys.argv) > 3 else 8.0
    tag = sys.argv[4] if len(sys.argv) > 4 else "smoke"
    set_auto(True, hero, start)
    _, after = recent_errors()
    print("play", start_play())
    shots = 3
    for i in range(shots):
        time.sleep(seconds / shots)
        print(capture_game(f"{tag}_{i}", 1600, 900))
    stats = call("stats_get", {})
    print(stats[:600])
    lines, _ = recent_errors(after)
    stop_play()
    set_auto(False)
    print("\n".join(lines[-40:]))


if __name__ == "__main__":
    main()
