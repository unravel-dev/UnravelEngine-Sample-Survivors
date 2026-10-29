"""Creates and writes the four hero animation controllers through the editor MCP."""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from mcp import call  # noqa: E402

HEROES = {"Knight": "Knight", "Barbarian": "Barbarian", "Mage": "Mage", "Rogue": "Rogue_Hooded"}

# Upper-body states (weapons play these while the legs keep walking).
UPPER = [
    ("Slash", "1H_Melee_Attack_Slice_Horizontal"),
    ("SlashDiagonal", "1H_Melee_Attack_Slice_Diagonal"),
    ("Chop", "1H_Melee_Attack_Chop"),
    ("Stab", "1H_Melee_Attack_Stab"),
    ("HeavyChop", "2H_Melee_Attack_Chop"),
    ("HeavySlice", "2H_Melee_Attack_Slice"),
    ("DualChop", "Dualwield_Melee_Attack_Chop"),
    ("DualSlice", "Dualwield_Melee_Attack_Slice"),
    ("Shoot", "1H_Ranged_Shoot"),
    ("Throw", "Throw"),
    ("CastShoot", "Spellcast_Shoot"),
    ("CastRaise", "Spellcast_Raise"),
    ("CastLong", "Spellcast_Long"),
    ("Block", "Block_Hit"),
    ("Reload", "1H_Ranged_Reload"),
    ("UpperSpin", "2H_Melee_Attack_Spin"),
]

# Base-layer full-body states besides Locomotion: (state, clip, loop).
FULL = [
    ("Dodge", "Dodge_Forward", False),
    ("Spin", "2H_Melee_Attack_Spinning", True),
    ("Leap", "Jump_Full_Short", False),
    ("Cheer", "Cheer", True),
    ("Death", "Death_A", False),
    ("Hit", "Hit_A", False),
]


def clip(stem, name):
    return f"app:/data/Characters/Heroes/{HEROES[stem]}/{HEROES[stem]}_{name}.anim"


def build(hero):
    stem = hero
    base_states = [{
        "name": "Locomotion", "motion": "blend_1d", "parameter_x": "Speed", "loop": True,
        "points": [{"clip": clip(stem, "Idle"), "position": [0, 0]},
                   {"clip": clip(stem, "Walking_A"), "position": [2.0, 0]},
                   {"clip": clip(stem, "Running_A"), "position": [5.0, 0]}],
        "editor_position": [0, 0]}]
    for i, (state, name, loop) in enumerate(FULL):
        base_states.append({"name": state, "motion": "clip", "clip": clip(stem, name), "loop": loop,
                            "editor_position": [300, i * 70]})
    upper_states = [{"name": "UpperIdle", "motion": "clip", "clip": clip(stem, "Idle"), "loop": True,
                     "editor_position": [0, 0]}]
    for i, (state, name) in enumerate(UPPER):
        upper_states.append({"name": state, "motion": "clip", "clip": clip(stem, name), "loop": False,
                             "tag": "Attack", "editor_position": [300, i * 70]})
    return {
        "parameters": [{"name": "Speed", "type": "float_value", "default_float": 0}],
        "layers": [
            {"name": "Base", "weight": 1, "blend_mode": "override_pose", "default_state": "Locomotion", "mask": [],
             "states": base_states, "transitions": []},
            {"name": "Upper", "weight": 0, "blend_mode": "override_pose", "default_state": "UpperIdle",
             "mask": [{"bone": "spine", "weight": 1, "include_children": True}],
             "states": upper_states, "transitions": []},
        ],
        "preview_model": f"app:/data/Characters/Heroes/{HEROES[stem]}/{HEROES[stem]}.glb",
    }


def main():
    if "--create" in sys.argv:
        items = [{"type": "animation_controller", "folder": "Animation", "name": f"Hero_{hero}"} for hero in HEROES]
        print(call("assets_create_batch", {"items": items, "wait_ms": 10000})[:800])
    for hero in HEROES:
        key = f"app:/data/Animation/Hero_{hero}.actrl"
        result = call("assets_set_properties_batch", {"items": [{"asset": key, "properties": build(hero)}]})
        print(hero, result[:300])
        print(call("animation_controller_validate", {"asset": key})[:1200])


if __name__ == "__main__":
    main()
