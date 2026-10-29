"""Builds the Barrowlight arena in the open scene through the editor MCP (procedural, seeded layout).

python build_arena.py plan     -> prints counts and writes arena_plan.json
python build_arena.py build    -> creates everything in the open scene (expects a fresh preset scene)
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

SEED = 1428
ARENA_RADIUS = 80.0
GROUND_SIZE = 240.0
HALLOWEEN = "app:/data/Environment/Halloween/gltf/{}.gltf"
DUNGEON = "app:/data/Environment/dungeon/{}.obj"

# name -> (size x, size y, size z) from assets_get_mesh_info; used for colliders and spacing.
SIZES = {
    "crypt": (6, 8, 8), "tree_dead_large": (2.3, 5.1, 1.4), "tree_dead_medium": (1.8, 3.8, 1.2),
    "tree_dead_small": (1.2, 2.6, 1.0), "tree_dead_large_decorated": (2.3, 5.1, 1.4),
    "tree_pine_orange_large": (4.8, 7.5, 4.2), "tree_pine_orange_medium": (3.8, 5.8, 3.4),
    "tree_pine_orange_small": (2.8, 4.2, 2.5), "tree_pine_yellow_large": (4.8, 7.5, 4.2),
    "tree_pine_yellow_medium": (3.8, 5.8, 3.4), "tree_pine_yellow_small": (2.8, 4.2, 2.5),
    "grave_A": (2, 2.1, 1.0), "grave_A_destroyed": (2, 1.5, 1.0), "grave_B": (2, 2.1, 1.0),
    "gravestone": (1.4, 1.6, 0.4), "gravemarker_A": (1.0, 1.6, 0.3), "gravemarker_B": (1.0, 1.6, 0.3),
    "fence": (4, 2.2, 0.5), "fence_broken": (4, 2.2, 0.5), "fence_pillar": (0.7, 2.6, 0.7),
    "arch_gate": (4.2, 4.4, 0.8), "shrine_candles": (1.1, 1.8, 1.1), "shrine": (1.1, 1.8, 1.1),
    "coffin": (2, 1.3, 3.0), "coffin_decorated": (2, 1.3, 3.0), "post_lantern": (0.64, 3.3, 1.6),
    "lantern_standing": (0.64, 0.93, 0.64), "bench": (2.4, 1.0, 0.8), "bench_decorated": (2.4, 1.0, 0.8),
    "pillar": (1.5, 4, 1.5), "pillar_decorated": (2.2, 4, 1.7), "column": (0.7, 1.4, 0.7),
    "rubble_large": (8.1, 3.5, 3.2), "rubble_half": (4.0, 1.8, 2.0), "plaque_candles": (1.6, 1.2, 0.6),
}


class Plan:
    def __init__(self):
        self.groups = []      # (name, parent)
        self.props = []       # dict(group, model, pos, yaw, scale, collider, obstacle)
        self.lights = []      # dict(group, pos, color, intensity, range)
        self.walls = []       # dict(pos, yaw, size)
        self.occupied = []    # (x, z, radius)

    def group(self, name, parent=None):
        self.groups.append((name, parent))
        return name

    def free(self, x, z, radius):
        for ox, oz, orad in self.occupied:
            if (x - ox) ** 2 + (z - oz) ** 2 < (radius + orad) ** 2:
                return False
        return True

    def add(self, group, model, x, z, yaw=0.0, scale=1.0, collider=None, obstacle=0.0, reserve=None, y=0.0):
        self.props.append({"group": group, "model": model, "pos": [round(x, 3), y, round(z, 3)],
                           "yaw": round(yaw, 2), "scale": scale, "collider": collider, "obstacle": obstacle})
        radius = reserve if reserve is not None else max(obstacle, 0.4)
        self.occupied.append((x, z, radius))

    def light(self, group, x, y, z, color=(1.0, 0.62, 0.3), intensity=90.0, radius=7.0):
        self.lights.append({"group": group, "pos": [round(x, 3), y, round(z, 3)], "color": list(color) + [1.0],
                            "intensity": intensity, "range": radius})


def key(model):
    return DUNGEON.format(model) if model in ("pillar", "pillar_decorated", "column", "rubble_large", "rubble_half",
                                              "barrel_large", "barrel_small", "torch_lit", "torch_mounted",
                                              "sword_shield_broken", "wall_broken", "wall_half",
                                              "banner_patternA_red", "banner_thin_red") else HALLOWEEN.format(model)


def box_collider(model, scale=1.0, shrink=0.85):
    sx, sy, sz = SIZES.get(model, (1, 1, 1))
    return {"type": "box", "center": [0, sy * 0.5, 0], "extends": [sx * shrink, sy, sz * shrink]}


def trunk_collider(radius, height):
    return {"type": "cylinder", "center": [0, height * 0.5, 0], "radius": radius, "length": height}


def build_plan():
    rng = random.Random(SEED)
    plan = Plan()
    level = plan.group("LEVEL")
    plan.group("Ground", level)
    sanctum = plan.group("Sanctum", level)
    yards = plan.group("Graveyards", level)
    paths = plan.group("Paths", level)
    scatter = plan.group("Moor", level)
    edge = plan.group("Edge", level)
    lights = plan.group("Lights", level)

    # Sanctum: a broken ring of pillars around the spawn, a shrine to the north.
    plan.occupied.append((0.0, 0.0, 5.0))
    plan.add(sanctum, "shrine_candles", 0.0, 8.0, 180.0, 1.4, box_collider("shrine_candles"), 1.0)
    plan.add(sanctum, "plaque_candles", -2.6, 8.4, 160.0, 1.0, None, 0.0, reserve=0.8)
    plan.add(sanctum, "plaque_candles", 2.6, 8.4, 200.0, 1.0, None, 0.0, reserve=0.8)
    plan.light(sanctum, 0.0, 2.2, 7.2, (1.0, 0.7, 0.4), 140.0, 9.0)
    for i in range(8):
        angle = i * math.tau / 8 + math.pi / 8
        x, z = math.sin(angle) * 11.0, math.cos(angle) * 11.0
        if i in (2, 5):
            plan.add(sanctum, "rubble_half", x, z, math.degrees(angle) + 90, 0.9, box_collider("rubble_half", shrink=0.7), 1.4)
            continue
        model = "pillar_decorated" if i % 2 == 0 else "pillar"
        plan.add(sanctum, model, x, z, math.degrees(angle) + 180, 0.8, trunk_collider(0.6, 3.2), 0.85)
    for i in range(6):
        angle = i * math.tau / 6
        plan.add(sanctum, "candle_triple", math.sin(angle) * 6.5, math.cos(angle) * 6.5, rng.uniform(0, 360), 1.2, None, 0.0, reserve=0.3)
    # Graveyards at the four diagonals.
    for index, angle_deg in enumerate((45, 135, 225, 315)):
        angle = math.radians(angle_deg)
        cx, cz = math.sin(angle) * 34.0, math.cos(angle) * 34.0
        build_graveyard(plan, rng, yards, lights, index, cx, cz, angle)
        build_path(plan, rng, paths, angle)
    build_moor(plan, rng, scatter, lights)
    build_edge(plan, rng, edge)
    return plan


def build_graveyard(plan, rng, group, lights_group, index, cx, cz, facing):
    name = f"Graveyard_{'NE SE SW NW'.split()[index]}"
    plan.groups.append((name, group))
    half = 7.0
    toward_center = facing + math.pi
    # Fence square with a gate facing the sanctum.
    for side in range(4):
        side_angle = toward_center + side * math.pi / 2
        nx, nz = math.sin(side_angle), math.cos(side_angle)
        tx, tz = -nz, nx
        for k in (-1, 0, 1):
            x = cx + nx * half + tx * k * 4.2
            z = cz + nz * half + tz * k * 4.2
            yaw = math.degrees(side_angle)
            if side == 0 and k == 0:
                plan.add(name, "arch_gate", x, z, yaw, 1.0, None, 0.0, reserve=1.0)
                continue
            model = "fence_broken" if rng.random() < 0.3 else "fence"
            if rng.random() < 0.15:
                continue
            plan.add(name, model, x, z, yaw, 1.0, box_collider(model, shrink=0.95), 0.0, reserve=0.6)
        corner_angle = side_angle + math.pi / 4
        px = cx + math.sin(corner_angle) * half * math.sqrt(2)
        pz = cz + math.cos(corner_angle) * half * math.sqrt(2)
        plan.add(name, "post_lantern", px, pz, math.degrees(side_angle), 1.0, trunk_collider(0.3, 3.0), 0.5)
        plan.light(lights_group, px, 2.8, pz, (1.0, 0.6, 0.28), 70.0, 7.0)
    # Crypt in the NW yard; rows of graves elsewhere.
    if index == 3:
        plan.add(name, "crypt", cx, cz, math.degrees(toward_center), 0.75, box_collider("crypt", 0.75, 0.75), 3.4)
        for k in range(2):
            ox = math.sin(toward_center + (k - 0.5) * 1.2) * 4.0
            oz = math.cos(toward_center + (k - 0.5) * 1.2) * 4.0
            plan.add(name, "lantern_standing", cx + ox, cz + oz, 0.0, 1.4, None, 0.0, reserve=0.4)
            plan.light(lights_group, cx + ox, 1.0, cz + oz, (0.55, 1.0, 0.6), 60.0, 6.0)
        return
    tx, tz = math.cos(toward_center), -math.sin(toward_center)
    nx, nz = math.sin(toward_center), math.cos(toward_center)
    for row in range(3):
        for column in range(4):
            if rng.random() < 0.18:
                continue
            u = (column - 1.5) * 2.9 + rng.uniform(-0.2, 0.2)
            v = (row - 1.0) * 3.2 + rng.uniform(-0.2, 0.2)
            x, z = cx + tx * u + nx * v, cz + tz * u + nz * v
            model = rng.choice(["grave_A", "grave_B", "gravestone", "gravemarker_A", "gravemarker_B", "grave_A_destroyed"])
            yaw = math.degrees(toward_center) + rng.uniform(-8, 8)
            plan.add(name, model, x, z, yaw, 1.0, box_collider(model), 0.75)
    plan.add(name, "tree_dead_large_decorated", cx - nx * 4.5 + tx * 4.5, cz - nz * 4.5 + tz * 4.5, rng.uniform(0, 360), 1.1,
             trunk_collider(0.45, 4.0), 0.7)
    plan.add(name, "coffin_decorated" if index % 2 else "coffin", cx + tx * -4.0 - nx * 4.0, cz + tz * -4.0 - nz * 4.0,
             math.degrees(toward_center) + 90, 1.0, box_collider("coffin"), 1.1)


def build_path(plan, rng, group, angle):
    for step in range(13):
        distance = 6.0 + step * 2.0
        x = math.sin(angle) * distance + rng.uniform(-0.35, 0.35)
        z = math.cos(angle) * distance + rng.uniform(-0.35, 0.35)
        model = rng.choice(["path_A", "path_B", "path_C", "path_D"])
        plan.props.append({"group": group, "model": model, "pos": [round(x, 3), 0.0, round(z, 3)],
                           "yaw": round(math.degrees(angle) + rng.choice((0, 90, 180, 270)) + rng.uniform(-10, 10), 2),
                           "scale": 1.0, "collider": None, "obstacle": 0.0})
        plan.occupied.append((x, z, 1.4))


def scatter_point(plan, rng, inner, outer, radius, tries=60):
    for _ in range(tries):
        angle = rng.uniform(0, math.tau)
        distance = math.sqrt(rng.uniform(inner * inner, outer * outer))
        x, z = math.sin(angle) * distance, math.cos(angle) * distance
        if plan.free(x, z, radius):
            return x, z
    return None


def build_moor(plan, rng, group, lights_group):
    table = [
        ("tree_dead_large", 10, 1.0, 0.45, 4.6, 2.6),
        ("tree_dead_medium", 12, 1.0, 0.35, 3.4, 2.2),
        ("tree_dead_small", 10, 1.0, 0.25, 2.4, 1.6),
        ("tree_pine_orange_large", 6, 1.0, 0.55, 6.0, 3.2),
        ("tree_pine_yellow_large", 6, 1.0, 0.55, 6.0, 3.2),
        ("tree_pine_orange_medium", 8, 1.0, 0.45, 4.8, 2.6),
        ("tree_pine_yellow_medium", 8, 1.0, 0.45, 4.8, 2.6),
        ("rubble_large", 3, 0.7, None, None, 3.4),
        ("pillar", 6, 0.8, 0.55, 3.2, 1.6),
        ("column", 8, 1.2, 0.35, 1.6, 1.2),
    ]
    for model, count, scale, trunk, height, reserve in table:
        for _ in range(count):
            spot = scatter_point(plan, rng, 16.0, ARENA_RADIUS - 6.0, reserve)
            if spot is None:
                continue
            x, z = spot
            if model == "rubble_large":
                collider, obstacle = box_collider(model, scale, 0.6), 2.4
            else:
                collider, obstacle = trunk_collider(trunk, height), trunk + 0.3
            plan.add(group, model, x, z, rng.uniform(0, 360), scale * rng.uniform(0.9, 1.1), collider, obstacle, reserve=reserve)
    decor = [
        ("pumpkin_orange", 8), ("pumpkin_yellow", 6), ("pumpkin_orange_small", 10), ("pumpkin_yellow_small", 8),
        ("bone_A", 14), ("bone_B", 14), ("bone_C", 10), ("ribcage", 8), ("skull", 10), ("skull_candle", 6),
        ("candle_melted", 8), ("candle_thin", 8), ("floor_dirt_small", 14), ("floor_dirt", 10),
    ]
    for model, count in decor:
        for _ in range(count):
            spot = scatter_point(plan, rng, 7.0, ARENA_RADIUS - 4.0, 0.6)
            if spot is None:
                continue
            x, z = spot
            y = -0.02 if model.startswith("floor_dirt") else 0.0
            plan.add(group, model, x, z, rng.uniform(0, 360), rng.uniform(0.85, 1.2), None, 0.0, reserve=0.6, y=y)
    for _ in range(10):
        spot = scatter_point(plan, rng, 18.0, ARENA_RADIUS - 8.0, 1.2)
        if spot is None:
            continue
        x, z = spot
        plan.add(group, "pumpkin_orange_jackolantern", x, z, rng.uniform(0, 360), 1.0, trunk_collider(0.6, 1.2), 0.75, reserve=1.2)
        plan.light(lights_group, x, 0.9, z, (1.0, 0.5, 0.15), 45.0, 5.0)
    for _ in range(6):
        spot = scatter_point(plan, rng, 14.0, ARENA_RADIUS - 10.0, 1.5)
        if spot is None:
            continue
        x, z = spot
        plan.add(group, "bench_decorated", x, z, rng.uniform(0, 360), 1.0, box_collider("bench_decorated"), 0.9, reserve=1.5)


def build_edge(plan, rng, group):
    """A dense tree line just outside the playable circle, and invisible walls on it."""
    count = 110
    for i in range(count):
        angle = i * math.tau / count + rng.uniform(-0.02, 0.02)
        for layer, distance in enumerate((ARENA_RADIUS + 3.0, ARENA_RADIUS + 9.0)):
            if layer == 1 and i % 2:
                continue
            d = distance + rng.uniform(-1.5, 1.5)
            x, z = math.sin(angle) * d, math.cos(angle) * d
            model = rng.choice(["tree_pine_orange_large", "tree_pine_yellow_large", "tree_pine_orange_medium",
                                "tree_dead_large", "tree_pine_yellow_medium"])
            plan.props.append({"group": group, "model": model, "pos": [round(x, 3), 0.0, round(z, 3)],
                               "yaw": round(rng.uniform(0, 360), 2), "scale": round(rng.uniform(1.0, 1.4), 2),
                               "collider": None, "obstacle": 0.0})
    segments = 40
    length = math.tau * (ARENA_RADIUS + 0.5) / segments * 1.15
    for i in range(segments):
        angle = i * math.tau / segments
        x, z = math.sin(angle) * (ARENA_RADIUS + 0.5), math.cos(angle) * (ARENA_RADIUS + 0.5)
        plan.walls.append({"pos": [round(x, 3), 3.0, round(z, 3)], "yaw": round(math.degrees(angle), 2),
                           "size": [round(length, 2), 6.0, 1.0]})


def chunks(items, size):
    for start in range(0, len(items), size):
        yield items[start:start + size]


def create(items):
    result = json.loads(call("scene_create_batch", {"items": items}))
    if "created" not in result:
        raise RuntimeError(json.dumps(result)[:800])
    return [entry["id"] for entry in result["created"]]


def build(plan):
    ids = {}
    for name, parent in plan.groups:
        item = {"kind": "empty", "name": name}
        if parent:
            item["parent_id"] = ids[parent]
        ids[name] = create([item])[0]
    # Ground: one big plane with the moor material and a static box under it.
    ground = create([{"kind": "primitive", "primitive": "plane", "name": "MoorGround", "parent_id": ids["Ground"],
                      "position": [0, 0, 0], "scale": [GROUND_SIZE, 1, GROUND_SIZE], "material": "app:/data/Materials/Ground.mat"}])[0]
    call("scene_add_components_batch", {"items": [{"entity_id": ground, "component": "Physics"}]})
    print(call("scene_set_component_properties_batch", {"items": [{"entity_id": ground, "component": "Physics", "properties": {
        "body_type": "static", "shapes": [{"type": "box", "center": [0, -0.5, 0], "extends": [1, 1, 1]}]}}]})[:200])
    # Props.
    created = []
    for batch in chunks(plan.props, 80):
        items = [{"kind": "mesh", "asset": key(p["model"]), "name": p["model"], "parent_id": ids[p["group"]],
                  "position": p["pos"], "rotation_euler": [0, p["yaw"], 0], "scale": [p["scale"]] * 3} for p in batch]
        created += list(zip(create(items), batch))
        print("props", len(created))
    solid = [(entity, prop) for entity, prop in created if prop["collider"] or prop["obstacle"] > 0.0]
    adds, sets = [], []
    for entity, prop in solid:
        if prop["collider"]:
            adds.append({"entity_id": entity, "component": "Physics"})
            sets.append({"entity_id": entity, "component": "Physics", "properties": {"body_type": "static", "shapes": [prop["collider"]]}})
        if prop["obstacle"] > 0.0:
            adds.append({"entity_id": entity, "component": "Script", "type_name": "Survivors.ArenaObstacle"})
            sets.append({"entity_id": entity, "component": "Script", "type_name": "Survivors.ArenaObstacle",
                         "properties": {"radius": round(prop["obstacle"] * prop["scale"], 3)}})
    for batch in chunks(adds, 120):
        print(call("scene_add_components_batch", {"items": batch})[:160])
    for batch in chunks(sets, 120):
        print(call("scene_set_component_properties_batch", {"items": batch})[:160])
    # Walls.
    wall_ids = create([{"kind": "empty", "name": f"Wall_{i:02d}", "parent_id": ids["Edge"], "position": w["pos"],
                        "rotation_euler": [0, w["yaw"], 0]} for i, w in enumerate(plan.walls)])
    call("scene_add_components_batch", {"items": [{"entity_id": e, "component": "Physics"} for e in wall_ids]})
    print(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": e, "component": "Physics", "properties": {"body_type": "static",
                                                                "shapes": [{"type": "box", "center": [0, 0, 0], "extends": [w["size"][0], w["size"][1], w["size"][2]]}]}}
        for e, w in zip(wall_ids, plan.walls)]})[:200])
    # Lights.
    light_ids = []
    for light in plan.lights:
        result = json.loads(call("scene_create_batch", {"items": [{"kind": "light", "light_type": "point", "name": "LanternLight",
                                                                    "parent_id": ids["Lights"], "position": light["pos"]}]}))
        light_ids.append((result["created"][0]["id"], light))
    print(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": e, "component": "Light", "properties": {"light": {"color": l["color"], "intensity": l["intensity"], "casts_shadows": False,
                                                                        "point_data": {"range": l["range"]}}}}
        for e, l in light_ids]})[:300])
    with open(os.path.join(HERE, "arena_ids.json"), "w") as file:
        json.dump({"groups": ids, "ground": ground}, file, indent=1)


if __name__ == "__main__":
    plan = build_plan()
    print("groups", len(plan.groups), "props", len(plan.props), "lights", len(plan.lights), "walls", len(plan.walls),
          "solid", sum(1 for p in plan.props if p["collider"]))
    with open(os.path.join(HERE, "arena_plan.json"), "w") as file:
        json.dump({"props": plan.props, "lights": plan.lights, "walls": plan.walls}, file)
    if len(sys.argv) > 1 and sys.argv[1] == "build":
        build(plan)
