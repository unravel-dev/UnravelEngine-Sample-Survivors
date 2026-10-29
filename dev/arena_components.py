"""Adds colliders and ArenaObstacle markers to the arena's solid props (matched to arena_plan.json by name and position)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402


def chunks(items, size):
    for start in range(0, len(items), size):
        yield items[start:start + size]


def main():
    plan = json.load(open(os.path.join(HERE, "arena_plan.json")))
    groups = json.load(open(os.path.join(HERE, "arena_ids.json")))["groups"]
    entities = []
    for name in ("Sanctum", "Graveyards", "Moor"):
        tree = json.loads(call("scene_get_hierarchy_batch", {"parent_id": groups[name], "max_depth": 3, "limit": 2000}))

        def walk(node):
            entities.append((node["id"], node["name"]))
            for child in node.get("children", []):
                walk(child)
        for root in tree["entities"]:
            walk(root)
    transforms = {}
    for batch in chunks([e for e, _ in entities], 200):
        result = json.loads(call("scene_get_entities_batch", {"entity_ids": batch}))
        for row in result["entities"]:
            transforms[row["id"]] = row
    by_name = {}
    for entity, name in entities:
        if entity in transforms:
            by_name.setdefault(name, []).append((entity, transforms[entity]))
    adds, sets = [], []
    matched = 0
    for prop in plan["props"]:
        if not prop["collider"] and prop["obstacle"] <= 0.0:
            continue
        candidates = by_name.get(prop["model"], [])
        best = None
        for entity, transform in candidates:
            position = transform.get("position")
            if position is None:
                continue
            if abs(position[0] - prop["pos"][0]) < 0.05 and abs(position[2] - prop["pos"][2]) < 0.05:
                best = entity
                break
        if best is None:
            continue
        matched += 1
        if prop["collider"]:
            adds.append({"entity_id": best, "component": "Physics"})
            sets.append({"entity_id": best, "component": "Physics", "properties": {"body_type": "static", "shapes": [prop["collider"]]}})
        if prop["obstacle"] > 0.0:
            adds.append({"entity_id": best, "component": "Script", "type_name": "Survivors.ArenaObstacle"})
            sets.append({"entity_id": best, "component": "Script", "type_name": "Survivors.ArenaObstacle",
                         "properties": {"radius": round(prop["obstacle"] * prop["scale"], 3)}})
    print("matched", matched)
    for batch in chunks(adds, 120):
        print(call("scene_add_components_batch", {"items": batch})[:200])
    for batch in chunks(sets, 120):
        print(call("scene_set_component_properties_batch", {"items": batch})[:200])


if __name__ == "__main__":
    main()
