"""Builds the title / hero select stage: four display heroes before the sanctum shrine and a spotlight, wired to Run."""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from mcp import call  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
IDS = json.load(open(os.path.join(HERE, "game_ids.json")))

STAGE_POSITION = [0.0, 0.0, 4.6]
# (class, local x, yaw): the models face +Z at yaw 0; turned around to face the menu camera, a little inward.
HEROES = [("Knight", -3.6, 168.0), ("Barbarian", -1.2, 176.0), ("Mage", 1.2, 184.0), ("Rogue", 3.6, 192.0)]
SPOT = {"color": [1.0, 0.82, 0.58, 1.0], "intensity": 500.0, "range": 10.0}
# A dim cold fill in front of the line so the heroes outside the spotlight still read against the shrine glow.
FILL = {"color": [0.62, 0.7, 1.0, 1.0], "intensity": 30.0, "range": 9.0, "position": [0.0, 2.6, -2.4]}


def created(text):
    data = json.loads(text)
    if "created" not in data:
        raise RuntimeError(text[:800])
    return [entry["id"] for entry in data["created"]]


def check(text):
    print(text[:500])
    return json.loads(text)


def main():
    old = json.loads(call("scene_find_entities_batch", {"name_exact": "MenuStage", "limit": 4}))["entities"]
    if old:
        check(call("scene_delete_entities_batch", {"entity_ids": [e["id"] for e in old]}))
    stage = created(call("scene_create_batch", {"items": [{"kind": "empty", "name": "MenuStage", "position": STAGE_POSITION}]}))[0]
    items = [{"kind": "prefab", "asset": f"app:/data/Prefabs/Heroes/Hero_{name}.pfb", "name": name, "parent_id": stage,
              "space": "local", "position": [x, 0.0, 0.0], "rotation_euler": [0.0, yaw, 0.0]} for name, x, yaw in HEROES]
    items.append({"kind": "light", "light_type": "spot", "name": "MenuSpotlight", "parent_id": stage, "space": "local",
                  "position": [HEROES[0][1], 4.5, -2.5], "rotation_euler": [54.0, 0.0, 0.0]})
    items.append({"kind": "light", "light_type": "point", "name": "MenuFill", "parent_id": stage, "space": "local",
                  "position": FILL["position"]})
    ids = created(call("scene_create_batch", {"items": items}))
    heroes, spot, fill = ids[:4], ids[4], ids[5]
    removes = [{"entity_id": e, "component": "Character Controller"} for e in heroes]
    removes += [{"entity_id": e, "component": "Script", "type_name": "Survivors.HeroController"} for e in heroes]
    check(call("scene_remove_components_batch", {"items": removes}))
    print(call("scene_get_component_properties_batch", {"items": [{"entity_id": spot, "component": "Light"}]})[:1500])
    check(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": spot, "component": "Light", "properties": {"light": {
            "color": SPOT["color"], "intensity": SPOT["intensity"], "casts_shadows": True,
            "spot_data": {"range": SPOT["range"], "inner_angle": 22, "outer_angle": 40}}}},
        {"entity_id": fill, "component": "Light", "properties": {"light": {
            "color": FILL["color"], "intensity": FILL["intensity"], "casts_shadows": False,
            "point_data": {"range": FILL["range"]}}}},
        {"entity_id": IDS["Run"], "component": "Script", "type_name": "Survivors.Run",
         "properties": {"menuStage": stage, "menuSpotlight": spot}},
    ]}))
    IDS["MenuStage"] = stage
    IDS["MenuSpotlight"] = spot
    print(call("scene_save", {})[:200])
    json.dump(IDS, open(os.path.join(HERE, "game_ids.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
