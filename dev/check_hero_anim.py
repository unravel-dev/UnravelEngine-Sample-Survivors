"""Plays the sandbox, runs every hero with an upper-body attack and captures the game view."""
import json
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from mcp import call  # noqa: E402

HEROES = {
    "444ada43-0f3f-1a44-be10-f523e5819cb6": "Slash",
    "49af8417-a75a-a649-8d74-bd8eee78b42c": "DualChop",
    "4806e2d4-83d6-e84c-9bb3-9b3da4e3c53c": "CastShoot",
    "e9dcb9cd-bafc-4d43-8ccd-ee6471f35f23": "Shoot",
}
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "shots")


def main():
    os.makedirs(OUT, exist_ok=True)
    camera = json.loads(call("scene_find_entities_batch", {"name_contains": "Main Camera"}))
    print(json.dumps(camera)[:300])
    print(call("play_set_active", {"active": True})[:200])
    print(call("editor_wait_for", {"play_phase": "running", "timeout_ms": 30000})[:200])
    time.sleep(1.5)
    print(call("play_set_paused", {"paused": True})[:200])
    for entity, attack in HEROES.items():
        call("animation_set_parameters", {"entity_id": entity, "values": {"Speed": 5.0}})
        state = call("animation_set_parameters", {"entity_id": entity, "state": attack, "layer": 1, "duration": 0.05})
        print(entity[:8], state[:400])
    call("play_skip_frame", {"count": 12})
    shot = call("viewport_capture_game", {"width": 1280, "height": 720})
    print(shot[-200:])
    os.replace(os.path.join(os.path.dirname(OUT), "..", "capture_viewport_capture_game_0.png") if False else
               os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "capture_viewport_capture_game_0.png"),
               os.path.join(OUT, "hero_anim.png"))
    print(call("play_set_active", {"active": False})[:200])


if __name__ == "__main__":
    main()
