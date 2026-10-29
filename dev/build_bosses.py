"""Boss controllers and prefabs: KayKit skeletons with props on the hand slots, scaled, with a controller each."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

# Full-body states every boss controller has (state, clip, loop). Bosses script these by name.
STATES = [
    ("Attack", "1H_Melee_Attack_Chop", False),
    ("AttackSlice", "1H_Melee_Attack_Slice_Horizontal", False),
    ("HeavyChop", "2H_Melee_Attack_Chop", False),
    ("HeavySlice", "2H_Melee_Attack_Slice", False),
    ("Spin", "2H_Melee_Attack_Spinning", True),
    ("JumpChop", "1H_Melee_Attack_Jump_Chop", False),
    ("Cast", "Spellcast_Shoot", False),
    ("CastRaise", "Spellcast_Raise", False),
    ("CastLong", "Spellcast_Long", False),
    ("Summon", "Spellcast_Summon", False),
    ("Taunt", "Taunt", False),
    ("Block", "Blocking", True),
    ("Hit", "Hit_A", False),
    ("Kneel", "Hit_B", False),
    ("Death", "Death_A", False),
    ("Rise", "Skeletons_Awaken_Floor_Long", False),
]

# KayKit hand-slot offsets (the Adventurers hold the same props this way): one-handed blades and axes turned 180
# degrees about Y and lifted a little, staffs turned, shields pushed out of the forearm.
HAND_OFFSETS = {
    "Skeleton_Blade": ([0, 0.0333, 0], [0, 180, 0]),
    "Skeleton_Axe": ([0, 0.0333, 0], [0, 180, 0]),
    "Skeleton_Staff": ([0, 0, 0], [0, 180, 0]),
    "Skeleton_Shield_Large_A": ([0, 0.017, 0.1559], [0, 0, 0]),
}

BOSSES = {
    "Gravewarden": {"model": "Skeleton_Warrior", "scale": 2.6, "right": "Skeleton_Blade", "left": "Skeleton_Shield_Large_A"},
    "HollowArchmage": {"model": "Skeleton_Mage", "scale": 2.6, "right": "Skeleton_Staff", "left": None},
    "HollowKing": {"model": "Skeleton_Warrior", "scale": 4.5, "right": "Skeleton_Axe", "left": None},
}


def clip(model, name):
    return f"app:/data/Characters/Bosses/{model}/{model}_{name}.anim"


def controller(model):
    states = [{"name": "Locomotion", "motion": "blend_1d", "parameter_x": "Speed", "loop": True,
               "points": [{"clip": clip(model, "Idle_Combat"), "position": [0, 0]},
                          {"clip": clip(model, "Walking_D_Skeletons"), "position": [1.5, 0]},
                          {"clip": clip(model, "Running_C"), "position": [4.0, 0]}],
               "editor_position": [0, 0]}]
    for i, (state, name, loop) in enumerate(STATES):
        states.append({"name": state, "motion": "clip", "clip": clip(model, name), "loop": loop, "editor_position": [300, i * 70]})
    return {"parameters": [{"name": "Speed", "type": "float_value", "default_float": 0}],
            "layers": [{"name": "Base", "weight": 1, "blend_mode": "override_pose", "default_state": "Locomotion", "mask": [],
                        "states": states, "transitions": []}],
            "preview_model": f"app:/data/Characters/Bosses/{model}/{model}.glb"}


def build_controllers():
    models = sorted({b["model"] for b in BOSSES.values()})
    items = [{"type": "animation_controller", "folder": "Animation", "name": f"Boss_{m}"} for m in models]
    print(call("assets_create_batch", {"items": items, "wait_ms": 10000})[:300])
    for model in models:
        key = f"app:/data/Animation/Boss_{model}.actrl"
        print(call("assets_set_properties_batch", {"items": [{"asset": key, "properties": controller(model)}]})[:200])
        print(call("animation_controller_validate", {"asset": key})[:300])


def find_child(root, name):
    tree = json.loads(call("scene_get_hierarchy_batch", {"parent_id": root, "max_depth": 30, "limit": 600}))
    found = []

    def walk(node):
        if node["name"] == name:
            found.append(node["id"])
        for child in node.get("children", []):
            walk(child)
    for node in tree["entities"]:
        walk(node)
    return found[0] if found else None


def build_prefabs():
    for name, spec in BOSSES.items():
        model = spec["model"]
        root = json.loads(call("scene_create_batch", {"items": [{"kind": "mesh", "asset": f"app:/data/Characters/Bosses/{model}/{model}.glb",
                                                                  "name": f"Boss_{name}", "position": [0, 0, 0]}]}))["created"][0]["id"]
        props = []
        for side, slot in (("right", "handslot.r"), ("left", "handslot.l")):
            if not spec[side]:
                continue
            parent = find_child(root, slot)
            position, rotation = HAND_OFFSETS.get(spec[side], ([0, 0, 0], [0, 0, 0]))
            props.append({"kind": "mesh", "asset": f"app:/data/Props/Skeletons/gltf/{spec[side]}.gltf", "name": spec[side],
                          "parent_id": parent, "space": "local", "position": position, "rotation_euler": rotation})
        if props:
            print(call("scene_create_batch", {"items": props})[:200])
        print(call("scene_set_component_properties_batch", {"items": [
            {"entity_id": root, "component": "Animation", "properties": {"controller": f"app:/data/Animation/Boss_{model}.actrl",
                                                                         "auto_play": True, "apply_root_motion": False}}]})[:200])
        print(call("scene_set_transforms_batch", {"items": [{"entity_id": root, "scale": [spec["scale"]] * 3}]})[:120])
        print(call("prefabs_create_from_entities_batch", {"items": [{"entity_id": root, "folder": "app:/data/Prefabs/Bosses",
                                                                      "name": f"Boss_{name}"}], "wait_ms": 15000})[:200])
        print(call("scene_delete_entities_batch", {"entity_ids": [root]})[:120])


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what in ("all", "controllers"):
        build_controllers()
    if what in ("all", "prefabs"):
        build_prefabs()
