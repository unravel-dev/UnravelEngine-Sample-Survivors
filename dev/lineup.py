"""Enemy review: auto-start, freeze the opening enemies away, spawn a lineup of every archetype x tint, capture close."""
import json
import os
import time

from playutil import call, start_play, stop_play, capture_game
from smoke import set_auto

HERE = os.path.dirname(os.path.abspath(__file__))


def invoke(method, *arguments):
    ids = json.load(open(os.path.join(HERE, "game_ids.json")))
    return call("scripts_invoke", {"entity_id": ids["Run"], "type_name": "Survivors.Run", "method": method, "arguments": list(arguments)})


def main():
    set_auto(True)
    print("play", start_play())
    time.sleep(0.5)
    print(invoke("DevInvulnerable", 120.0)[:120])
    print(invoke("DevSpawnLineup", 60.0)[:120])
    ids = json.load(open(os.path.join(HERE, "game_ids.json")))
    time.sleep(1.0)
    print(invoke("DevCamera", 32.0, 13.0, [0.0, 0.0, 10.0])[:120])
    time.sleep(1.5)
    print(capture_game("lineup", 1600, 900))
    stop_play()
    set_auto(False)


if __name__ == "__main__":
    main()
