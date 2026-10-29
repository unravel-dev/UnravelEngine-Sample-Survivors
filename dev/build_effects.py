"""Barrowlight particle effects through the editor MCP: one registry of named, script-fired emitters under the
scene's "Effects" root (Effects.cs finds them by entity name).

    python build_effects.py build [group]     create missing effects and (re)apply every effect's settings
    python build_effects.py tune NAME...      re-apply the settings of some effects
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

P = "app:/data/Textures/Particles"
V = "app:/data/Textures/VFX"
T = "app:/data/Textures/telegraphs"

WHITE = [1.0, 1.0, 1.0]
BONE = [0.95, 0.9, 0.78]
GOLD = [1.0, 0.8, 0.38]
FIRE = [1.0, 0.5, 0.15]
GREEN = [0.45, 1.0, 0.55]
VIOLET = [0.7, 0.4, 1.0]
CYAN = [0.5, 0.9, 1.0]
DANGER = [1.0, 0.29, 0.16]


def keys(*points):
    return [{"progress": p, "color": c} for p, c in points]


def span(start, end=None):
    end = end or start
    return [{"progress": 0.0, "min": start[0], "max": start[1]}, {"progress": 1.0, "min": end[0], "max": end[1]}]


FROM_ZERO_PROGRESS = 0.02


def from_zero(curve):
    """A scripted size curve that grows from s0 > 0 (where 1.0 = the script's size), rewritten to start at 0 so the
    engine fits its PEAK to the size: the first key moves to 2% of the life, an extra key 0 sits at progress 0."""
    first = curve[0]
    if first["progress"] > 0.0 or first["max"] <= 0.0:
        return curve
    moved = dict(first, progress=min(FROM_ZERO_PROGRESS, curve[1]["progress"] * 0.5))
    return [{"progress": 0.0, "min": 0.0, "max": 0.0}, moved] + curve[1:]


def punch(start, end, at=0.25, reach=0.75):
    middle = [a + (b - a) * reach for a, b in zip(start, end)]
    return [{"progress": 0.0, "min": start[0], "max": start[1]}, {"progress": at, "min": middle[0], "max": middle[1]},
            {"progress": 1.0, "min": end[0], "max": end[1]}]


def burst(**props):
    """A burst-only emitter: scripts fire it with Emit(count, params); it keeps simulating off screen."""
    base = {"emission_rate": 0.0, "loop": True, "auto_play": True, "simulation_space": "world",
            "culling_mode": "always_simulate", "shape": "sphere", "direction": "outward", "spawn_location": "inside",
            "emission_shape_scale": [0.01, 0.01, 0.01], "velocity_gradient": span((0.0, 0.0))}
    base.update(props)
    return base


SPARK = {"texture": f"{P}/spark.png", "align_to_direction": True, "render_mode": "billboard", "blend_mode": "additive"}

# name -> (group, emitter properties, sort priority)
EFFECTS = {}


RENAMED = {"emission_shape_scale": "shape_scale", "rotation": "start_rotation", "rotation_speed": "spin_speed",
           "random_rotation_direction": "random_spin_direction", "scale_gradient": "size_over_life",
           "initial_scale_3d": "size_axes", "bounce": "restitution"}


def normalize(props):
    """Writes the reflected property names the editor takes today (speed as start_speed + speed_over_life)."""
    out = {}
    for key, value in props.items():
        if key == "velocity_gradient":
            first, last = value[0], value[-1]
            out["start_speed"] = [first["min"], first["max"]]
            start = (first["min"] + first["max"]) * 0.5
            end = (last["min"] + last["max"]) * 0.5
            out["speed_over_life"] = [{"progress": 0.0, "value": 1.0},
                                      {"progress": 1.0, "value": (end / start) if start > 1e-6 else 1.0}]
            continue
        if key == "lifetime" and not isinstance(value, list):
            value = [value, value]
        if key == "collision" and value == "planes":
            value = "bounce"
        if key == "spawn_location":
            continue
        out[RENAMED.get(key, key)] = value
    return out


POSITIONS = {}


def effect(group, name, props, sort=0, position=None):
    props = normalize(props)
    props["sort_priority"] = sort
    EFFECTS[name] = (group, props)
    if position is not None:
        POSITIONS[name] = position


# ------------------------------------------------------------------------------------------------ telegraphs
# Sized and timed by the script: EmitParams size = diameter (m), lifetime = wind-up; the size curves are normalised.
effect("Telegraphs", "TelegraphRing", burst(
    max_particles=256, lifetime=1.0, texture=f"{T}/ring_thin.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=2.2, scale_gradient=span((1.0, 1.0)),
    color_gradient=keys((0.0, WHITE + [0.0]), (0.08, WHITE + [0.9]), (0.92, WHITE + [1.0]), (1.0, WHITE + [0.0]))), -12)
effect("Telegraphs", "TelegraphFill", burst(
    max_particles=256, lifetime=1.0, texture=f"{T}/disc_fill.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=0.9, scale_gradient=span((0.0, 0.0), (1.0, 1.0)),
    color_gradient=keys((0.0, WHITE + [0.25]), (0.9, WHITE + [0.5]), (0.97, WHITE + [1.0]), (1.0, WHITE + [0.0]))), -13)
effect("Telegraphs", "TelegraphCone", burst(
    max_particles=128, lifetime=1.0, texture=f"{T}/cone_120.png", render_mode="horizontal_billboard",
    align_to_direction=True, blend_mode="additive", color_intensity=1.1, velocity_gradient=span((1.0, 1.0)),
    scale_gradient=from_zero(span((0.35, 0.35), (1.0, 1.0))),
    color_gradient=keys((0.0, DANGER + [0.2]), (0.9, DANGER + [0.55]), (0.97, [1, 0.9, 0.8, 1.0]), (1.0, DANGER + [0.0]))), -13)
effect("Telegraphs", "AimLine", burst(
    max_particles=256, lifetime=1.0, texture=f"{T}/aim_line.png", render_mode="horizontal_billboard",
    align_to_direction=True, blend_mode="additive", color_intensity=3.0, velocity_gradient=span((1.0, 1.0)),
    scale_gradient=span((2.0, 2.0)),
    color_gradient=keys((0.0, DANGER + [0.0]), (0.15, DANGER + [0.7]), (0.75, DANGER + [0.9]), (0.8, [1, 0.85, 0.7, 1.0]),
                        (1.0, DANGER + [0.0]))), -11)

# ------------------------------------------------------------------------------------------------ hits and deaths
effect("Combat", "HitSpark", burst(**dict(SPARK,
    max_particles=1024, lifetime=0.22, color_intensity=6.0, initial_scale_3d=[1, 3, 1],
    velocity_gradient=span((3.5, 7.0), (0.5, 1.0)), scale_gradient=span((0.12, 0.18), (0.05, 0.08)),
    color_gradient=keys((0.0, [1.0, 0.95, 0.85, 1.0]), (0.6, BONE + [0.8]), (1.0, BONE + [0.0])))), 2)
effect("Combat", "CritSpark", burst(**dict(SPARK,
    max_particles=512, lifetime=0.32, color_intensity=12.0, initial_scale_3d=[1, 4, 1],
    velocity_gradient=span((6.0, 11.0), (0.8, 1.4)), scale_gradient=span((0.2, 0.28), (0.06, 0.1)),
    color_gradient=keys((0.0, [1.0, 1.0, 0.8, 1.0]), (0.5, [1.0, 0.85, 0.3, 0.9]), (1.0, [1.0, 0.6, 0.1, 0.0])))), 3)
effect("Combat", "BoneBurst", burst(
    max_particles=2048, lifetime=0.8, texture=f"{P}/shard.png", render_mode="billboard", blend_mode="normal",
    gravity_scale=2.4, rotation=[0, 360], rotation_speed=[360, 720], random_rotation_direction=True,
    lighting="surface",
    velocity_gradient=span((2.5, 5.5), (2.0, 4.0)), scale_gradient=span((0.12, 0.2), (0.1, 0.16)),
    collision="planes", collision_planes=[[0.0, 1.0, 0.0, 0.0]], bounce=0.3, max_bounces=2,
    color_gradient=keys((0.0, BONE + [1.0]), (0.75, BONE + [1.0]), (1.0, BONE + [0.0]))), 1)
effect("Combat", "EnemySwing", burst(
    max_particles=256, lifetime=0.18, texture=f"{V}/arc_8x1.png", texture_sheet_tiles=[8, 1], texture_sheet_randomize=True,
    render_mode="horizontal_billboard", align_to_direction=True, rotation=[90, 90], blend_mode="additive",
    color_intensity=1.4, velocity_gradient=span((1.0, 1.0)), scale_gradient=from_zero(span((0.8, 0.8), (1.0, 1.0))),
    color_gradient=keys((0.0, [1.0, 0.6, 0.5, 0.0]), (0.2, [1.0, 0.55, 0.45, 0.6]), (1.0, [1.0, 0.4, 0.3, 0.0]))), 1)

# ------------------------------------------------------------------------------------------------ enemy abilities
effect("Enemies", "GraveBurst", burst(
    max_particles=128, lifetime=0.5, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=4.0, rotation=[0, 360], scale_gradient=from_zero(punch((0.4, 0.4), (1.0, 1.0))),
    color_gradient=keys((0.0, WHITE + [1.0]), (0.5, WHITE + [0.6]), (1.0, WHITE + [0.0]))), -9)
effect("Enemies", "CinderBlast", burst(
    max_particles=256, lifetime=0.55, texture=f"{V}/explosion_fire_8x8.png", texture_sheet_tiles=[8, 8],
    texture_sheet_cycles=1, render_mode="billboard", blend_mode="additive", color_intensity=2.2, rotation=[0, 360],
    soft_fade_distance=0.4, scale_gradient=span((0.9, 1.1), (1.1, 1.3)),
    color_gradient=keys((0.0, WHITE + [1.0]), (0.8, WHITE + [0.9]), (1.0, WHITE + [0.0]))), 2)
effect("Enemies", "SummonMotes", burst(
    shape="circle", direction="up", spawn_location="inside", emission_shape_scale=[1.6, 0.02, 1.6],
    max_particles=1024, lifetime=1.4, texture=f"{P}/sparkle.png", render_mode="billboard", blend_mode="additive",
    color_intensity=6.0, velocity_gradient=span((0.6, 1.4)), scale_gradient=span((0.12, 0.2), (0.02, 0.04)),
    color_gradient=keys((0.0, GREEN + [0.0]), (0.2, GREEN + [1.0]), (1.0, [0.2, 0.8, 0.3, 0.0]))), 2)
effect("Enemies", "WardRing", burst(
    max_particles=64, lifetime=1.0, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=3.0, rotation=[0, 360], scale_gradient=from_zero(punch((0.2, 0.2), (1.0, 1.0), 0.35, 0.8)),
    color_gradient=keys((0.0, WHITE + [0.0]), (0.15, WHITE + [1.0]), (1.0, WHITE + [0.0]))), -9)
effect("Enemies", "LungeFlare", burst(
    max_particles=128, lifetime=0.25, texture=f"{P}/glow.png", render_mode="billboard", blend_mode="additive",
    color_intensity=6.0, scale_gradient=span((0.5, 0.6), (0.15, 0.2)),
    color_gradient=keys((0.0, CYAN + [1.0]), (1.0, CYAN + [0.0]))), 3)
effect("Enemies", "EliteBeam", burst(
    max_particles=16, lifetime=0.8, texture=f"{P}/light_shaft.png", render_mode="vertical_billboard",
    pivot=[0.5, 1.0], initial_scale_3d=[1, 6, 1], blend_mode="additive", color_intensity=4.0,
    scale_gradient=span((1.4, 1.4), (0.6, 0.6)),
    color_gradient=keys((0.0, GOLD + [0.0]), (0.1, GOLD + [1.0]), (1.0, GOLD + [0.0]))), 2)
effect("Enemies", "EliteDeathBurst", burst(
    max_particles=32, lifetime=0.6, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=6.0, rotation=[0, 360], scale_gradient=punch((1.0, 1.0), (6.0, 6.0)),
    color_gradient=keys((0.0, GOLD + [1.0]), (0.5, GOLD + [0.6]), (1.0, GOLD + [0.0]))), -9)

# ------------------------------------------------------------------------------------------------ hero and pickups
effect("Hero", "LevelUpRing", burst(
    max_particles=16, lifetime=0.7, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=8.0, rotation=[0, 360], scale_gradient=punch((1.0, 1.0), (9.0, 9.0)),
    color_gradient=keys((0.0, GOLD + [1.0]), (0.5, GOLD + [0.7]), (1.0, GOLD + [0.0]))), -9)
effect("Hero", "ReviveBurst", burst(
    max_particles=16, lifetime=0.9, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=10.0, rotation=[0, 360], scale_gradient=punch((1.0, 1.0), (13.0, 13.0)),
    color_gradient=keys((0.0, [1, 1, 0.9, 1.0]), (0.5, GOLD + [0.7]), (1.0, GOLD + [0.0]))), -9)
effect("Hero", "HealBurst", burst(
    shape="circle", direction="up", spawn_location="inside", emission_shape_scale=[0.9, 0.02, 0.9],
    max_particles=128, lifetime=0.9, texture=f"{V}/heal_plus.png", render_mode="billboard", blend_mode="additive",
    color_intensity=4.0, velocity_gradient=span((1.4, 2.4), (0.4, 0.6)), scale_gradient=span((0.3, 0.45), (0.1, 0.15)),
    color_gradient=keys((0.0, GREEN + [0.0]), (0.15, GREEN + [1.0]), (1.0, GREEN + [0.0]))), 3)
effect("Hero", "BellFlash", burst(
    max_particles=8, lifetime=0.8, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=12.0, rotation=[0, 360], scale_gradient=punch((2.0, 2.0), (46.0, 46.0), 0.35, 0.85),
    color_gradient=keys((0.0, [1, 1, 0.95, 1.0]), (0.4, GOLD + [0.8]), (1.0, GOLD + [0.0]))), -9)
effect("Hero", "FrostWave", burst(
    max_particles=8, lifetime=0.9, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=8.0, rotation=[0, 360], scale_gradient=punch((2.0, 2.0), (46.0, 46.0), 0.35, 0.85),
    color_gradient=keys((0.0, [0.9, 1, 1, 1.0]), (0.4, CYAN + [0.8]), (1.0, CYAN + [0.0]))), -9)
effect("Hero", "LanternShatter", burst(**dict(SPARK,
    max_particles=256, lifetime=0.5, color_intensity=8.0, gravity_scale=1.5, initial_scale_3d=[1, 2.5, 1],
    velocity_gradient=span((3.0, 7.0), (1.0, 2.0)), scale_gradient=span((0.12, 0.2), (0.05, 0.08)),
    color_gradient=keys((0.0, [1.0, 0.85, 0.5, 1.0]), (1.0, FIRE + [0.0])))), 2)


# ------------------------------------------------------------------------------------------------ ambient
def ambient(**props):
    """A looping emitter that is already full when the scene starts (prewarm)."""
    base = {"loop": True, "auto_play": True, "prewarm": True, "simulation_space": "world", "culling_mode": "renderer_based"}
    base.update(props)
    return base


MIST = [0.62, 0.7, 0.78]
effect("Follow", "MoorFog", ambient(
    shape="box", shape_scale=[64.0, 1.2, 48.0], shape_position=[0.0, 0.6, 4.0], max_particles=160, emission_rate=8.0,
    lifetime=[12.0, 16.0], texture=f"{V}/smoke_billow_4x4.png", texture_sheet_tiles=[4, 4], texture_sheet_randomize=True,
    render_mode="billboard", blend_mode="normal", lighting="volumetric", soft_fade_distance=1.5,
    start_rotation=[0, 360], spin_speed=[2, 6], random_spin_direction=True, start_speed=[0.1, 0.3],
    acceleration=[0.12, 0.0, 0.05], size_over_life=span((6.0, 9.0), (8.0, 12.0)),
    color_gradient=keys((0.0, MIST + [0.0]), (0.25, MIST + [0.14]), (0.75, MIST + [0.12]), (1.0, MIST + [0.0]))), -6, [0, 0, 0])
effect("Follow", "Fireflies", ambient(
    shape="box", shape_scale=[54.0, 2.5, 40.0], shape_position=[0.0, 1.6, 4.0], max_particles=140, emission_rate=12.0,
    lifetime=[5.0, 8.0], texture=f"{P}/glow.png", render_mode="billboard", blend_mode="additive", color_intensity=5.0,
    start_speed=[0.05, 0.2], turbulence_strength=0.8, turbulence_frequency=0.35, turbulence_speed=0.4,
    size_over_life=span((0.12, 0.2)),
    color_gradient=keys((0.0, [0.7, 1.0, 0.45, 0.0]), (0.1, [0.75, 1.0, 0.5, 1.0]), (0.3, [0.75, 1.0, 0.5, 0.25]),
                        (0.5, [0.8, 1.0, 0.5, 1.0]), (0.72, [0.75, 1.0, 0.5, 0.3]), (1.0, [0.7, 1.0, 0.45, 0.0]))), 1, [0, 0, 0])
import math as _math
for _index, _angle in enumerate((45, 135, 225, 315)):
    _x = round(_math.sin(_math.radians(_angle)) * 34.0, 2)
    _z = round(_math.cos(_math.radians(_angle)) * 34.0, 2)
    effect("Ambient", f"GraveMist_{_index}", ambient(
        shape="circle", shape_scale=[9.0, 0.1, 9.0], shape_position=[0.0, 0.4, 0.0], max_particles=60, emission_rate=4.0,
        lifetime=[9.0, 13.0], texture=f"{V}/smoke_puffs_4x4.png", texture_sheet_tiles=[4, 4], texture_sheet_randomize=True,
        render_mode="billboard", blend_mode="normal", lighting="volumetric", soft_fade_distance=1.0,
        start_rotation=[0, 360], spin_speed=[3, 8], random_spin_direction=True, start_speed=[0.05, 0.2],
        size_over_life=span((3.0, 4.5), (4.5, 6.5)),
        color_gradient=keys((0.0, MIST + [0.0]), (0.3, MIST + [0.22]), (0.7, MIST + [0.18]), (1.0, MIST + [0.0]))), -6, [_x, 0.0, _z])
effect("Ambient", "CryptSouls", ambient(
    shape="circle", shape_scale=[3.0, 0.1, 3.0], shape_position=[0.0, 0.3, 0.0], max_particles=60, emission_rate=6.0,
    lifetime=[3.0, 4.5], texture=f"{V}/wisp_2x2.png", texture_sheet_tiles=[2, 2], texture_sheet_randomize=True,
    render_mode="billboard", blend_mode="additive", color_intensity=4.0, start_speed=[0.4, 0.9],
    turbulence_strength=0.5, turbulence_frequency=0.5, turbulence_speed=0.6, size_over_life=span((0.5, 0.8), (0.2, 0.3)),
    color_gradient=keys((0.0, GREEN + [0.0]), (0.2, GREEN + [0.9]), (1.0, [0.3, 0.9, 0.6, 0.0]))), 1, [-24.04, 0.0, 24.04])
effect("Ambient", "SanctumEmbers", ambient(
    shape="circle", shape_scale=[1.0, 0.1, 1.0], shape_position=[0.0, 1.4, 0.0], max_particles=60, emission_rate=10.0,
    lifetime=[1.6, 2.6], texture=f"{P}/ember.png", render_mode="billboard", blend_mode="additive", color_intensity=12.0,
    start_speed=[0.8, 1.6], acceleration=[0.1, 0.3, 0.05], turbulence_strength=0.3, size_over_life=span((0.04, 0.06), (0.01, 0.02)),
    color_gradient=keys((0.0, [1.0, 0.9, 0.6, 0.0]), (0.1, [1.0, 0.8, 0.45, 1.0]), (0.6, [1.0, 0.5, 0.14, 0.9]),
                        (1.0, [0.75, 0.18, 0.04, 0.0]))), 2, [0.0, 0.0, 8.0])


# ================================================================================================ build

def find_root():
    found = json.loads(call("scene_find_entities_batch", {"name_contains": "Effects"}))["entities"]
    for entity in found:
        if entity["name"] == "Effects" and entity["parent_id"] is None:
            return entity["id"]
    return json.loads(call("scene_create_batch", {"items": [{"kind": "empty", "name": "Effects", "position": [0, 0, 0]}]}))["created"][0]["id"]


def existing_children(root):
    tree = json.loads(call("scene_get_hierarchy_batch", {"parent_id": root, "max_depth": 3, "limit": 2000}))
    names = {}

    def walk(node):
        names[node["name"]] = node["id"]
        for child in node.get("children", []):
            walk(child)
    for node in tree["entities"]:
        walk(node)
    return names


def apply(names_to_build):
    root = find_root()
    present = existing_children(root)
    for group in sorted({EFFECTS[n][0] for n in names_to_build}):
        if group not in present:
            present[group] = json.loads(call("scene_create_batch", {"items": [{"kind": "empty", "name": group, "parent_id": root,
                                                                                "space": "local", "position": [0, 0, 0]}]}))["created"][0]["id"]
    missing = [n for n in names_to_build if n not in present]
    if missing:
        items = [{"kind": "particle_emitter", "name": n, "parent_id": present[EFFECTS[n][0]], "space": "world",
                  "position": POSITIONS.get(n, [0, 0, 0])} for n in missing]
        for entry in json.loads(call("scene_create_batch", {"items": items}))["created"]:
            present[entry["name"]] = entry["id"]
    for name in names_to_build:
        result = json.loads(call("scene_set_component_properties_batch", {"items": [
            {"entity_id": present[name], "component": "Particle Emitter", "properties": EFFECTS[name][1]}]}))
        if not result.get("ok", True) or "error" in json.dumps(result).lower():
            print(name, json.dumps(result)[:600])
    print("effects applied:", len(names_to_build))


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else "build"
    if command == "build":
        groups = sys.argv[2:]
        apply([n for n, (g, _) in EFFECTS.items() if not groups or g in groups])
    elif command == "tune":
        apply(sys.argv[2:])
