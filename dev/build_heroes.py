"""Turns the four test hero entities into game heroes (loadout, controller, script) and saves them as prefabs."""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from mcp import call  # noqa: E402

HEROES = {
    "Knight": ("444ada43-0f3f-1a44-be10-f523e5819cb6", "Knight",
               ["c1c0ac59-086c-ad4f-990e-fd6deb6c7567", "abdc1acd-efc8-c849-b19a-0ba59b3b22d2",
                "a107c43f-b929-6e4c-ad2d-f0f41933a0bf", "68112bf0-e184-c74b-a871-f330d328c59e",
                "99635e28-51bd-e447-a422-807e7a90ecb4"]),
    "Barbarian": ("49af8417-a75a-a649-8d74-bd8eee78b42c", "Barbarian",
                  ["67c133b5-0ba9-314d-8e5b-39a3c1499964", "15ed9e79-70ca-f947-a973-f73aa1601c4d",
                   "faa3ec6c-b242-3140-8b2b-4e5007f56dfd"]),
    "Mage": ("4806e2d4-83d6-e84c-9bb3-9b3da4e3c53c", "Mage",
             ["f3b681d9-b038-5940-9c9d-f21930b1a005", "d5c3b855-ba1b-da40-bd38-0494ec9064d7"]),
    "Rogue": ("e9dcb9cd-bafc-4d43-8ccd-ee6471f35f23", "Rogue",
              ["8193a16a-06df-5a4d-b8c9-1ba4259487b3", "14afe03f-83ce-5b40-b10d-6a130fd4c344",
               "aca773e1-9cb9-c747-874d-689ad1fb78aa"]),
}

CAPSULE_HEIGHT = 2.0
CAPSULE_RADIUS = 0.45


def check(text):
    print(text[:600])
    data = json.loads(text)
    return data


def main():
    hidden = [{"entity_id": prop, "active": False} for _, _, props in HEROES.values() for prop in props]
    check(call("scene_set_active_batch", {"items": hidden}))
    adds = []
    for name, (entity, hero_class, _) in HEROES.items():
        adds.append({"entity_id": entity, "component": "Character Controller"})
        adds.append({"entity_id": entity, "component": "Script", "type_name": "Survivors.HeroController"})
    check(call("scene_add_components_batch", {"items": adds}))
    sets = []
    for name, (entity, hero_class, _) in HEROES.items():
        sets.append({"entity_id": entity, "component": "Animation", "properties": {
            "controller": f"app:/data/Animation/Hero_{name}.actrl", "auto_play": True, "apply_root_motion": False}})
        sets.append({"entity_id": entity, "component": "Character Controller", "properties": {
            "radius": CAPSULE_RADIUS, "height": CAPSULE_HEIGHT, "center": [0, CAPSULE_HEIGHT * 0.5, 0]}})
        sets.append({"entity_id": entity, "component": "Script", "type_name": "Survivors.HeroController",
                     "properties": {"heroClass": hero_class, "bodyRadius": CAPSULE_RADIUS}})
    check(call("scene_set_component_properties_batch", {"items": sets}))
    names = [{"entity_id": entity, "name": f"Hero_{name}"} for name, (entity, _, _) in HEROES.items()]
    check(call("scene_set_names_batch", {"items": names}))
    prefabs = [{"entity_id": entity, "folder": "app:/data/Prefabs/Heroes", "name": f"Hero_{name}", "attach": True}
               for name, (entity, _, _) in HEROES.items()]
    check(call("prefabs_create_from_entities_batch", {"items": prefabs, "wait_ms": 15000}))


if __name__ == "__main__":
    main()
