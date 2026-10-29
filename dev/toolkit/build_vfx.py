"""Spell Arena effects, second generation: deletes the old Ambient and Spells hierarchies and rebuilds them
through the editor MCP server, then attaches their scripts.

    python build_vfx.py            rebuild everything
    python build_vfx.py tune NAME  re-apply the settings of one effect entity (fast iteration)
"""
import json
import math
import os
import sys

from mcp import call

P = "app:/data/Textures/Particles"
V = "app:/data/Textures/VFX"
NS = "ParticlesSample."
AUDIO = "app:/data/Audio"

PILLAR_RADIUS = 11.8
BRAZIER_ANGLES = [22.5, 112.5, 202.5, 292.5]

BLUE = [0.62, 0.76, 1.0]
BLUE_WHITE = [0.86, 0.92, 1.0]
ORANGE = [1.0, 0.56, 0.22]
CYAN = [0.55, 0.9, 1.0]
VIOLET = [0.74, 0.46, 1.0]
PINK = [1.0, 0.55, 0.95]
GOLD = [1.0, 0.82, 0.42]


# ============================================================================================ helpers

def polar(radius, degrees, y=0.0):
    a = math.radians(degrees)
    return [round(radius * math.sin(a), 4), y, round(radius * math.cos(a), 4)]


def keys(*points):
    return [{"progress": p, "color": c} for p, c in points]


def fade(color, a_in=0.1, a_out=0.3, peak=1.0, hold=None):
    hold = peak if hold is None else hold
    return keys((0.0, color + [0.0]), (a_in, color + [peak]), (1.0 - a_out, color + [hold]), (1.0, color + [0.0]))


def strobe(color):
    """Lightning flicker: on, nearly off, on, dim, on, out."""
    return keys((0.0, color + [1.0]), (0.12, color + [0.25]), (0.24, color + [1.0]), (0.45, color + [0.35]),
                (0.62, color + [0.85]), (1.0, color + [0.0]))


def twinkle(color):
    return keys((0.0, color + [0.0]), (0.1, color + [1.0]), (0.3, color + [0.35]), (0.5, color + [1.0]),
                (0.72, color + [0.4]), (1.0, color + [0.0]))


def span(start, end=None):
    end = end or start
    return [{"progress": 0.0, "min": start[0], "max": start[1]}, {"progress": 1.0, "min": end[0], "max": end[1]}]


def pop_in(start, end=None, seconds=0.15, life=1.0, overshoot=1.1):
    """A size that pops in: 0 at birth, a slight overshoot after `seconds`, settled on the start size at twice
    that, then on to the end size. Summoned things (runes, glyphs) arrive instead of fading in at full size."""
    end = end or start
    grow = seconds / life
    return [{"progress": 0.0, "min": 0.0, "max": 0.0},
            {"progress": grow, "min": start[0] * overshoot, "max": start[1] * overshoot},
            {"progress": 2.0 * grow, "min": start[0], "max": start[1]},
            {"progress": 1.0, "min": end[0], "max": end[1]}]


def punch(start, end, at=0.25, reach=0.75):
    """A size that bursts out and slows down: `reach` of the growth is done by `at` of the life, as a
    shockwave does."""
    middle = [a + (b - a) * reach for a, b in zip(start, end)]
    return [{"progress": 0.0, "min": start[0], "max": start[1]},
            {"progress": at, "min": middle[0], "max": middle[1]},
            {"progress": 1.0, "min": end[0], "max": end[1]}]


def spread(size, seconds, life, start=0.35):
    """A decal that spreads fast from `start` of its size over `seconds`, then holds."""
    at = seconds / life
    return [{"progress": 0.0, "min": size[0] * start, "max": size[1] * start},
            {"progress": at / 3.0, "min": size[0] * 0.8, "max": size[1] * 0.8},
            {"progress": at, "min": size[0], "max": size[1]},
            {"progress": 1.0, "min": size[0], "max": size[1]}]


def burst(**props):
    """A burst-only emitter: scripts fire it with Emit(count); it keeps simulating off screen."""
    base = {"emission_rate": 0.0, "loop": True, "simulation_space": "world", "culling_mode": "always_simulate",
            "shape": "sphere", "direction": "outward", "spawn_location": "inside",
            "emission_shape_scale": [0.01, 0.01, 0.01], "velocity_gradient": span((0.0, 0.0))}
    base.update(props)
    return base


def stream(**props):
    """A short non-looping emitter: Play() runs its emission lifetime once. It does not auto play,
    so it waits for the script that triggers it."""
    base = {"loop": False, "auto_play": False, "simulation_space": "world", "culling_mode": "always_simulate"}
    base.update(props)
    return base


def ambient(**props):
    """A looping emitter that is already full when the scene starts (prewarm)."""
    base = {"loop": True, "prewarm": True, "simulation_space": "world", "culling_mode": "renderer_based"}
    base.update(props)
    return base


def light(color, intensity, radius, enabled=True):
    return {"type": "point", "color": color + [1.0], "intensity": intensity, "range": radius, "casts_shadows": False,
            "enabled": enabled}


# Draw order layers (sort_priority): lower draws first. Ground decals and floor rings lie under everything,
# smoke covers the fire inside it, sparks and flashes composite on top. The rest stay at 0 and draw back
# to front by distance.
SORT_PRIORITY = {
    "Ground Mist": -5,
    "Lightning Scorch": -10, "Lightning Scorch Glow": -9, "Lightning Ground Flash": -9, "Lightning Shock Ring": -9,
    "Lightning Charge Ring": -9, "Lightning Bolt Glow": 1, "Lightning Bolt": 2, "Lightning Sparks": 2,
    "Lightning Ground Arcs": 2, "Lightning Chain Arcs": 2, "Lightning Embers": 2, "Lightning Charge Sparks": 2,
    "Lightning Flash": 3, "Lightning Impact Arcs": 2,
    "Fireball Launch Sparks": 2, "Fireball Launch Flash": 3,
    "Explosion Scorch": -10, "Explosion Scorch Glow": -9, "Explosion Dust Ring": -8, "Explosion Shock Ring": -9,
    "Explosion Smoke Billow": 1, "Explosion Column": 1, "Explosion Debris": 2, "Explosion Chunks": 2,
    "Explosion Embers": 2, "Explosion": 3,
    "Frost Patch": -10, "Frost Ring": -9, "Frost Spikes Glow": -9, "Frost Shatter Dust": 1, "Frost Mist": -1, "Frost Shards": 2, "Frost Shatter": 2, "Frost Snow": 2,
    "Frost Sparkles": 2, "Frost Flash": 3,
    "Vortex Runes Outer": -10, "Vortex Runes Inner": -10, "Vortex Ring": -9, "Vortex Smoke": 1, "Vortex Burst": 2,
    "Vortex Burst Wisps": 2, "Vortex Burst Glyphs": 2, "Vortex Flash": 3,
    "Holy Runes": -10, "Holy Glow": -10, "Holy Rings": -9, "Holy Descent": 2, "Holy Rise": 2, "Holy Heal": 2,
}
for k in range(1, 4):
    SORT_PRIORITY.update({f"Fireball Projectile {k}": 3, f"Fireball Projectile {k} Body": 2,
                          f"Fireball Projectile {k} Trail Glow": 2, f"Fireball Projectile {k} Flames": 1,
                          f"Fireball Projectile {k} Embers": 2})


def with_sort_priority(name, emitter):
    """The emitter's properties with its draw layer; a fireball projectile waits for its cast."""
    extra = {"auto_play": False} if name.startswith("Fireball Projectile") else {}
    return dict(emitter, sort_priority=SORT_PRIORITY.get(name, 0), **extra)


EFFECTS = []    # (name, parent, local position, emitter props or None, light props or None)
ROTATIONS = {}  # name -> local euler rotation, for the few effects that are turned


def fx(name, parent, position, emitter=None, light_props=None, rotation=None):
    EFFECTS.append((name, parent, list(position), emitter, light_props))
    if rotation:
        ROTATIONS[name] = list(rotation)


# ============================================================================================ ambient

# Directional art (spark, arcs, shards) is drawn pointing up: aligned particles turn its top along their motion.
SPARK = {"texture": f"{P}/spark.png", "align_to_direction": True, "render_mode": "billboard", "blend_mode": "additive"}


def define_ambient():
    fx("Ambient", None, (0, 0, 0))
    for k, angle in enumerate(BRAZIER_ANGLES):
        root = f"Brazier {k + 1}"
        fx(root, "Ambient", polar(PILLAR_RADIUS, angle, 3.1), ambient(
            shape="circle", direction="up", emission_shape_scale=[0.3, 0.02, 0.3], max_particles=48, emission_rate=9.0,
            lifetime=1.3, texture=f"{P}/fire_8x8.png", texture_sheet_tiles=[8, 8], texture_sheet_cycles=1,
            texture_sheet_randomize=True, frame_blending=True, render_mode="vertical_billboard", blend_mode="additive",
            pivot=[0.5, 1.0], color_intensity=1.7, soft_fade_distance=0.15, velocity_gradient=span((0.05, 0.15)),
            scale_gradient=span((0.95, 1.35), (0.85, 1.15)),
            color_gradient=keys((0, [1, 1, 1, 0]), (0.12, [1, 1, 1, 1]), (0.75, [1, 1, 1, 0.85]), (1, [1, 1, 1, 0]))))
        fx(f"{root} Glow", root, (0, 0.35, 0), ambient(
            shape="sphere", emission_shape_scale=[0.05, 0.05, 0.05], max_particles=8, emission_rate=5.0, lifetime=0.8,
            texture=f"{P}/glow.png", render_mode="billboard", blend_mode="additive", color_intensity=2.5,
            rotation=[0, 360], soft_fade_distance=0.3, velocity_gradient=span((0.0, 0.05)),
            scale_gradient=span((1.6, 2.0)), color_gradient=fade(ORANGE, 0.3, 0.4, 0.35)))
        fx(f"{root} Embers", root, (0, 0.05, 0), ambient(
            shape="hemisphere", direction="outward", emission_shape_scale=[0.32, 0.45, 0.32], max_particles=64,
            emission_rate=14.0, lifetime=1.7, texture=f"{P}/ember.png", render_mode="billboard", blend_mode="additive",
            color_intensity=14.0, force_over_lifetime=[0.15, 0.35, 0.1], velocity_gradient=span((1.0, 2.2)),
            scale_gradient=span((0.035, 0.06), (0.008, 0.015)),
            color_gradient=keys((0, [1.0, 0.9, 0.6, 0]), (0.08, [1.0, 0.82, 0.45, 1]), (0.55, [1.0, 0.5, 0.14, 0.9]),
                                (1, [0.75, 0.18, 0.04, 0]))))
        fx(f"{root} Smoke", root, (0, 0.35, 0), ambient(
            shape="sphere", direction="outward", emission_shape_position=[0, 6.0, 0],
            emission_shape_scale=[0.08, 0.1, 0.08], max_particles=40, emission_rate=4.0, lifetime=5.5,
            texture=f"{V}/smoke_puffs_4x4.png", texture_sheet_tiles=[4, 4], texture_sheet_randomize=True,
            rotation=[0, 360], rotation_speed=[5, 20], random_rotation_direction=True, soft_fade_distance=0.4,
            render_mode="billboard", blend_mode="normal", force_over_lifetime=[0.03, 0.06, 0.02],
            velocity_gradient=span((2.0, 3.0)), scale_gradient=span((0.5, 0.7), (2.4, 3.0)),
            color_gradient=keys((0, [0.2, 0.19, 0.18, 0]), (0.12, [0.2, 0.19, 0.18, 0.42]),
                                (0.5, [0.26, 0.25, 0.25, 0.26]), (1, [0.32, 0.32, 0.32, 0]))))
        fx(f"{root} Light", root, (0, 0.6, 0), None, light(ORANGE, 220.0, 9.0))

    fx("Mana Fountain", "Ambient", (0, 1.72, 15.5), ambient(**dict(SPARK,
        shape="sphere", direction="outward", emission_shape_position=[0, 3.75, 0], emission_shape_scale=[0.08, 0.08, 0.08],
        max_particles=600, emission_rate=170.0, lifetime=1.7, gravity_scale=0.2, initial_scale_3d=[1, 2.5, 1],
        color_intensity=7.0, velocity_gradient=span((4.0, 4.6)), scale_gradient=span((0.1, 0.14), (0.06, 0.08)),
        color_gradient=keys((0, [0.6, 0.95, 1, 0]), (0.05, [0.6, 0.95, 1, 1]), (0.7, [0.3, 0.7, 1, 0.9]),
                            (1, [0.2, 0.5, 1, 0])))))
    fx("Mana Fountain Splash", "Mana Fountain", (0, -1.0, 0), ambient(
        shape="circle", direction="up", emission_shape_scale=[1.1, 0.02, 1.1], max_particles=128, emission_rate=50.0,
        lifetime=0.5, gravity_scale=0.6, texture=f"{P}/glow.png", render_mode="billboard", blend_mode="additive",
        color_intensity=7.0, velocity_gradient=span((0.2, 0.45)), scale_gradient=span((0.06, 0.1), (0.02, 0.04)),
        color_gradient=fade([0.6, 0.95, 1.0], 0.1, 0.4)))
    fx("Mana Fountain Mist", "Mana Fountain", (0, -0.8, 0), ambient(
        shape="circle", direction="up", emission_shape_scale=[1.3, 0.02, 1.3], max_particles=32, emission_rate=4.0,
        lifetime=3.2, texture=f"{V}/smoke_puffs_4x4.png", texture_sheet_tiles=[4, 4], texture_sheet_randomize=True,
        rotation=[0, 360], rotation_speed=[5, 15], random_rotation_direction=True, soft_fade_distance=0.6,
        render_mode="billboard", blend_mode="normal", force_over_lifetime=[0.0, 0.03, 0.0],
        velocity_gradient=span((0.4, 0.8)), scale_gradient=span((0.9, 1.2), (1.8, 2.4)),
        color_gradient=fade([0.55, 0.85, 1.0], 0.25, 0.4, 0.2)))
    fx("Mana Fountain Light", "Mana Fountain", (0, 1.2, 0), None, light(CYAN, 260.0, 10.0))

    fx("Fireflies", "Ambient", (0, 1.7, 2.0), ambient(
        shape="box", direction="outward", emission_shape_scale=[11, 1.2, 11], max_particles=64, emission_rate=7.0,
        lifetime=7.0, position_easing="InOutSine", texture=f"{P}/glow.png", render_mode="billboard",
        blend_mode="additive", color_intensity=8.0, velocity_gradient=span((0.3, 0.9)),
        scale_gradient=span((0.06, 0.09)), color_gradient=twinkle([0.8, 1, 0.45])))
    fx("Ground Mist", "Ambient", (0, 0.2, 0), ambient(
        shape="circle", direction="inward", spawn_location="surface", emission_shape_scale=[12.0, 0.05, 12.0],
        max_particles=64, emission_rate=4.0, lifetime=12.0, texture=f"{V}/smoke_puffs_4x4.png",
        texture_sheet_tiles=[4, 4], texture_sheet_randomize=True, rotation=[0, 360], rotation_speed=[2, 6],
        random_rotation_direction=True, soft_fade_distance=1.2, render_mode="billboard", blend_mode="normal",
        velocity_gradient=span((2.0, 3.0)), scale_gradient=span((4.0, 5.0), (6.0, 7.5)),
        color_gradient=fade([0.45, 0.5, 0.62], 0.3, 0.3, 0.12, 0.1)))
    # The focus spins (Spinner on its entity), so its local-space motes and wisps orbit it as they rise.
    fx("Focus Motes", "Spell Focus", (0, -0.5, 0), ambient(
        shape="circle", direction="up", spawn_location="surface", emission_shape_scale=[0.6, 0.02, 0.6],
        max_particles=64, emission_rate=16.0, lifetime=1.8, simulation_space="local", culling_mode="always_simulate",
        texture=f"{P}/sparkle.png",
        render_mode="billboard", blend_mode="additive", color_intensity=6.0, rotation=[0, 360],
        rotation_speed=[40, 120], random_rotation_direction=True, velocity_gradient=span((0.9, 1.4)),
        scale_gradient=span((0.14, 0.2), (0.03, 0.05)), color_gradient=twinkle(PINK)))
    fx("Focus Wisps", "Spell Focus", (0, -0.35, 0), ambient(
        shape="circle", direction="up", spawn_location="surface", emission_shape_scale=[0.45, 0.02, 0.45],
        max_particles=32, emission_rate=8.0, lifetime=1.6, simulation_space="local", culling_mode="always_simulate",
        texture=f"{V}/wisp_2x2.png",
        texture_sheet_tiles=[2, 2], texture_sheet_randomize=True, render_mode="billboard", blend_mode="additive",
        color_intensity=3.0, rotation=[0, 360], rotation_speed=[90, 180], random_rotation_direction=True,
        velocity_gradient=span((0.4, 0.7)), scale_gradient=span((0.35, 0.45), (0.15, 0.2)),
        color_gradient=fade(VIOLET, 0.2, 0.4, 0.8)))
    fx("Focus Halo", "Spell Focus", (0, 0, 0), ambient(
        shape="sphere", emission_shape_scale=[0.05, 0.05, 0.05], max_particles=16, emission_rate=6.0, lifetime=1.0,
        simulation_space="local", culling_mode="always_simulate", texture=f"{P}/glow.png", render_mode="billboard", blend_mode="additive",
        color_intensity=2.0, velocity_gradient=span((0.0, 0.0)), scale_gradient=span((1.5, 1.7), (1.8, 2.0)),
        soft_fade_distance=0.2, color_gradient=fade(VIOLET, 0.35, 0.4, 0.45)))
    fx("Focus Light", "Spell Focus", (0, 0.3, 0), None, light(VIOLET, 60.0, 6.0))


# ============================================================================================ spells

SMOKE_PUFFS = {"texture": f"{V}/smoke_puffs_4x4.png", "texture_sheet_tiles": [4, 4], "texture_sheet_randomize": True,
               "render_mode": "billboard", "blend_mode": "normal", "rotation": [0, 360]}
FIRE_SHEET = {"texture": f"{V}/explosion_fire_8x8.png", "texture_sheet_tiles": [8, 8], "blend_mode": "additive",
              "render_mode": "billboard", "frame_blending": True}


def ring(colour, sizes, life, intensity, texture="ring_energy.png", folder=V):
    """A ring on the floor bursting out from sizes[0] to sizes[1] metres, fast and then slowing as a
    shockwave does."""
    return burst(max_particles=8, lifetime=life, texture=f"{folder}/{texture}", render_mode="horizontal_billboard",
                 blend_mode="additive", color_intensity=intensity, rotation=[0, 360],
                 scale_gradient=punch((sizes[0], sizes[0]), (sizes[1], sizes[1])),
                 color_gradient=keys((0, colour + [0.95]), (0.5, colour + [0.55]), (1, colour + [0])))


def flash(colour, sizes, life, intensity, soft=0.6):
    return burst(max_particles=8, lifetime=life, texture=f"{P}/glow.png", render_mode="billboard",
                 blend_mode="additive", color_intensity=intensity, rotation=[0, 360], soft_fade_distance=soft,
                 scale_gradient=span(*sizes), color_gradient=keys((0, colour + [1]), (0.45, colour + [0.5]), (1, colour + [0])))


def decal(texture, sizes, colour, alpha, life, blend="normal", intensity=1.0, spread_seconds=0.4):
    return burst(max_particles=16, lifetime=life, texture=texture, render_mode="horizontal_billboard",
                 blend_mode=blend, color_intensity=intensity, rotation=[0, 360], scale_gradient=spread(sizes, spread_seconds, life),
                 color_gradient=keys((0, colour + [0]), (0.03, colour + [alpha]), (0.6, colour + [alpha * 0.85]),
                                     (1, colour + [0])))


def define_lightning():
    fx("Lightning", "Spells", (0, 0, 0))
    fx("Lightning Charge Ring", "Lightning", (0, 0.05, 0), burst(
        max_particles=8, lifetime=0.28, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=6.0, rotation=[0, 360], scale_gradient=span((3.6, 3.6), (0.4, 0.4)),
        color_gradient=keys((0, BLUE + [0.2]), (0.5, BLUE + [1.0]), (1, BLUE_WHITE + [0.9]))))
    fx("Lightning Charge Sparks", "Lightning", (0, 0.15, 0), burst(**dict(SPARK,
        shape="circle", direction="inward", spawn_location="surface", emission_shape_scale=[2.2, 0.02, 2.2],
        max_particles=64, lifetime=0.28, position_easing="InQuad", initial_scale_3d=[1, 3, 1], color_intensity=16.0,
        velocity_gradient=span((2.0, 2.2)), scale_gradient=span((0.16, 0.22), (0.08, 0.1)),
        color_gradient=keys((0, BLUE + [0]), (0.3, BLUE + [1]), (1, BLUE_WHITE + [1])))))
    fx("Lightning Bolt", "Lightning", (0, 0, 0), burst(
        emission_shape_scale=[0.12, 0.01, 0.12], max_particles=16, lifetime=0.22, texture=f"{V}/bolt_8x1.png",
        texture_sheet_tiles=[8, 1], texture_sheet_randomize=True, render_mode="vertical_billboard",
        pivot=[0.5, 1.0], initial_scale_3d=[1, 9, 1], blend_mode="additive", color_intensity=16.0,
        scale_gradient=span((1.5, 1.9)), color_gradient=strobe(BLUE_WHITE)))
    fx("Lightning Bolt Glow", "Lightning", (0, 0, 0), burst(
        max_particles=8, lifetime=0.3, texture=f"{V}/beam_4x1.png", texture_sheet_tiles=[4, 1],
        texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 1.0], initial_scale_3d=[1, 4, 1],
        blend_mode="additive", color_intensity=1.2, scale_gradient=span((1.5, 1.9)), color_gradient=strobe(BLUE)))
    # Upright like the bolt: a vertical billboard only turns around the world up axis, so the flash
    # stands on the floor at the strike from every camera angle instead of leaning into it.
    fx("Lightning Flash", "Lightning", (0, 0, 0), dict(flash(BLUE_WHITE, ((3.0, 3.5), (1.6, 2.0)), 0.16, 6.0),
                                                     render_mode="vertical_billboard", pivot=[0.5, 0.8],
                                                     rotation=[0, 0]))
    fx("Lightning Ground Flash", "Lightning", (0, 0.04, 0), burst(
        max_particles=4, lifetime=0.4, texture=f"{P}/glow.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=1.5, scale_gradient=span((5.0, 6.0)),
        color_gradient=keys((0, BLUE + [1]), (1, BLUE + [0]))))
    fx("Lightning Shock Ring", "Lightning", (0, 0.06, 0), ring(BLUE_WHITE, (1.0, 8.0), 0.35, 7.0))
    fx("Lightning Sparks", "Lightning", (0, 0.1, 0), burst(**dict(SPARK,
        shape="hemisphere", emission_shape_scale=[0.3, 0.3, 0.3], max_particles=256, lifetime=0.85,
        gravity_scale=0.7, position_easing="OutQuad", initial_scale_3d=[1, 4, 1], color_intensity=22.0,
        velocity_gradient=span((3.0, 7.0)), scale_gradient=span((0.1, 0.16), (0.03, 0.05)),
        color_gradient=keys((0, [0.85, 0.92, 1, 1]), (0.5, [0.6, 0.75, 1, 1]), (1, [0.4, 0.5, 1, 0])))))
    # Ground arcs crawl over the floor: horizontal billboards lie flat and align_to_direction turns the
    # arc's length (the texture's height) along their motion, so they run outward.
    fx("Lightning Ground Arcs", "Lightning", (0, 0.05, 0), stream(
        shape="circle", direction="outward", spawn_location="surface", emission_shape_scale=[0.3, 0.02, 0.3],
        max_particles=64, emission_rate=50.0, emission_lifetime=0.5, lifetime=0.22, texture=f"{V}/arc_8x1.png",
        texture_sheet_tiles=[8, 1], texture_sheet_randomize=True, render_mode="horizontal_billboard",
        align_to_direction=True, rotation=[0, 0], initial_scale_3d=[0.18, 1, 1], blend_mode="additive",
        color_intensity=10.0, velocity_gradient=span((1.2, 2.4)), scale_gradient=span((1.4, 2.2), (1.0, 1.4)),
        color_gradient=strobe(BLUE_WHITE)))
    # Crackles stand up around the strike and where the chain hits a dummy: upright arcs (a vertical
    # billboard, tilted a little either way) that flicker for a few frames.
    fx("Lightning Impact Arcs", "Lightning", (0, 0.8, 0), stream(
        shape="sphere", emission_shape_scale=[0.5, 0.25, 0.5], max_particles=64, emission_rate=45.0,
        emission_lifetime=0.45, lifetime=0.12, texture=f"{V}/arc_8x1.png", texture_sheet_tiles=[8, 1],
        texture_sheet_randomize=True, render_mode="vertical_billboard", rotation=[-25, 25],
        initial_scale_3d=[0.2, 1, 1], blend_mode="additive", color_intensity=12.0,
        velocity_gradient=span((0.0, 0.0)), scale_gradient=span((1.0, 1.7)), color_gradient=strobe(BLUE_WHITE)))
    fx("Lightning Chain Arcs", "Lightning", (0, 1.0, 0), burst(
        max_particles=32, lifetime=0.3, texture=f"{V}/arc_8x1.png", texture_sheet_tiles=[8, 1],
        texture_sheet_randomize=True, render_mode="billboard", align_to_direction=True, initial_scale_3d=[0.18, 1, 1],
        blend_mode="additive", color_intensity=12.0, velocity_gradient=span((0.3, 0.3)), scale_gradient=span((1.0, 1.0)),
        color_gradient=strobe(BLUE_WHITE)))
    fx("Lightning Smoke", "Lightning", (0, 0.4, 0), burst(**dict(SMOKE_PUFFS,
        emission_shape_scale=[0.6, 0.3, 0.6], max_particles=32, lifetime=2.8, position_easing="OutCubic",
        rotation_speed=[10, 30], random_rotation_direction=True, soft_fade_distance=0.6,
        force_over_lifetime=[0, 0.25, 0], velocity_gradient=span((0.6, 1.4)), scale_gradient=span((1.0, 1.4), (2.2, 3.0)),
        color_gradient=keys((0, [0.32, 0.36, 0.45, 0]), (0.1, [0.32, 0.36, 0.45, 0.45]), (0.5, [0.3, 0.32, 0.38, 0.3]),
                            (1, [0.3, 0.32, 0.38, 0])))))
    fx("Lightning Embers", "Lightning", (0, 0.3, 0), burst(
        shape="sphere", direction="up", emission_shape_scale=[0.8, 0.3, 0.8], max_particles=64, lifetime=1.6,
        texture=f"{P}/glow.png", render_mode="billboard", blend_mode="additive", color_intensity=12.0,
        velocity_gradient=span((1.0, 2.2)), scale_gradient=span((0.05, 0.09), (0.01, 0.02)),
        color_gradient=twinkle(BLUE)))
    fx("Lightning Scorch", "Lightning", (0, 0.02, 0), decal(f"{P}/scorch.png", (2.6, 3.2), [0.05, 0.045, 0.05], 0.75, 7.0))
    fx("Lightning Scorch Glow", "Lightning", (0, 0.03, 0), burst(
        max_particles=8, lifetime=1.4, texture=f"{V}/scorch_embers.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=6.0, rotation=[0, 360], scale_gradient=span((2.4, 2.8)),
        color_gradient=keys((0, BLUE_WHITE + [1]), (0.3, BLUE + [0.7]), (1, BLUE + [0]))))
    fx("Lightning Light", "Lightning", (0, 2.0, 0), None, light(BLUE_WHITE, 0.0, 16.0, enabled=False))


def define_fireball():
    fx("Fireball", "Spells", (0, 0, 0))
    for k in range(1, 4):
        root = f"Fireball Projectile {k}"
        fx(root, "Fireball", (0, -20, 0), {
            "shape": "sphere", "direction": "outward", "emission_shape_scale": [0.05, 0.05, 0.05], "max_particles": 32,
            "emission_rate": 45.0, "lifetime": 0.15, "loop": True, "simulation_space": "local",
            "culling_mode": "always_simulate", "texture": f"{P}/glow.png", "render_mode": "billboard",
            "blend_mode": "additive", "color_intensity": 8.0, "velocity_gradient": span((0.0, 0.0)),
            "scale_gradient": span((0.5, 0.6), (0.35, 0.4)), "color_gradient": fade([1.0, 0.78, 0.45], 0.2, 0.3)})
        fx(f"{root} Body", root, (0, 0, 0), dict(FIRE_SHEET,
            shape="sphere", direction="outward", emission_shape_scale=[0.05, 0.05, 0.05], max_particles=24,
            emission_rate=30.0, lifetime=0.3, loop=True, simulation_space="local", culling_mode="always_simulate",
            texture_sheet_cycles=1, texture_sheet_randomize=False, rotation=[0, 360],
            rotation_speed=[180, 360], random_rotation_direction=True, color_intensity=3.0,
            velocity_gradient=span((0.0, 0.0)), scale_gradient=span((0.9, 1.2), (0.9, 1.2)),
            color_gradient=fade([1, 1, 1], 0.2, 0.3)))
        # The trail is built from soft-edged layers so it reads as one tapering tail from every side;
        # crisp round flipbook puffs spaced along a fast path read as a string of beads.
        # Hot core: soft glows packed a few centimetres apart merge into one streak that thins with age.
        fx(f"{root} Trail Glow", root, (0, 0, 0), {
            "shape": "sphere", "direction": "outward", "emission_shape_scale": [0.03, 0.03, 0.03],
            "max_particles": 96, "emission_rate": 240.0, "lifetime": 0.2, "loop": True, "simulation_space": "world",
            "temporal_motion": 1.0, "culling_mode": "always_simulate", "texture": f"{P}/glow.png",
            "render_mode": "billboard", "blend_mode": "additive", "color_intensity": 4.0,
            "velocity_gradient": span((0.0, 0.05)), "scale_gradient": span((0.55, 0.6), (0.12, 0.16)),
            "color_gradient": keys((0, [1.0, 0.85, 0.55, 0.9]), (0.4, [1.0, 0.55, 0.18, 0.6]),
                                   (1, [0.8, 0.25, 0.05, 0]))})
        # Rolling flames: the soft billow sprites drawn additively in fire colours, cooling from yellow to
        # deep red as they drift back and spread.
        fx(f"{root} Flames", root, (0, 0, 0), dict(SMOKE_PUFFS,
            shape="sphere", direction="outward", emission_shape_scale=[0.12, 0.12, 0.12], max_particles=128,
            emission_rate=120.0, lifetime=0.45, loop=True, simulation_space="world", temporal_motion=1.0,
            culling_mode="always_simulate", blend_mode="additive", rotation_speed=[60, 160],
            random_rotation_direction=True, soft_fade_distance=0.3, color_intensity=3.0,
            velocity_gradient=span((0.2, 0.45)), scale_gradient=span((0.5, 0.6), (0.8, 1.0)),
            color_gradient=keys((0, [1.0, 0.8, 0.45, 1]), (0.3, [1.0, 0.45, 0.12, 0.8]),
                                (0.7, [0.6, 0.14, 0.04, 0.35]), (1, [0.3, 0.05, 0.02, 0]))))
        # Smoke takes over behind the fire: it fades in instead of appearing dark at the head.
        fx(f"{root} Smoke", root, (0, 0, 0), dict(SMOKE_PUFFS,
            shape="sphere", direction="outward", emission_shape_scale=[0.15, 0.15, 0.15], max_particles=96,
            emission_rate=45.0, lifetime=1.6, loop=True, simulation_space="world", temporal_motion=1.0,
            culling_mode="always_simulate", rotation_speed=[10, 30], random_rotation_direction=True,
            soft_fade_distance=0.5, force_over_lifetime=[0, 0.3, 0], velocity_gradient=span((0.1, 0.3)),
            scale_gradient=span((0.45, 0.55), (1.3, 1.6)),
            color_gradient=keys((0, [0.2, 0.18, 0.16, 0]), (0.25, [0.2, 0.18, 0.16, 0.45]),
                                (1, [0.26, 0.25, 0.24, 0]))))
        fx(f"{root} Embers", root, (0, 0, 0), {
            "shape": "sphere", "direction": "outward", "emission_shape_scale": [0.15, 0.15, 0.15], "max_particles": 64,
            "emission_rate": 30.0, "lifetime": 0.8, "loop": True, "simulation_space": "world",
            "culling_mode": "always_simulate", "gravity_scale": 0.5, "texture": f"{P}/ember.png",
            "render_mode": "billboard", "blend_mode": "additive", "color_intensity": 10.0,
            "velocity_gradient": span((0.5, 1.4)), "scale_gradient": span((0.04, 0.06), (0.015, 0.025)),
            "color_gradient": keys((0, [1.0, 0.9, 0.6, 1]), (0.6, [1.0, 0.5, 0.14, 0.9]), (1, [0.75, 0.18, 0.04, 0]))})
        fx(f"{root} Light", root, (0, 0, 0), None, light(ORANGE, 600.0, 7.0, enabled=False))

    fx("Fireball Launch Flash", "Fireball", (0, 1.75, -9.0), flash([1.0, 0.8, 0.5], ((2.2, 2.6), (0.8, 1.0)), 0.18, 12.0, soft=0.2))
    fx("Fireball Launch Sparks", "Fireball", (0, 1.75, -9.0), burst(**dict(SPARK,
        emission_shape_scale=[0.15, 0.15, 0.15], max_particles=64, lifetime=0.45, position_easing="OutQuad",
        initial_scale_3d=[1, 3, 1], color_intensity=16.0, velocity_gradient=span((0.8, 1.8)),
        scale_gradient=span((0.08, 0.12), (0.02, 0.04)),
        color_gradient=keys((0, [1, 0.9, 0.6, 1]), (1, [1, 0.45, 0.1, 0])))))

    fx("Explosion", "Fireball", (0, 0.8, 0), flash([1.0, 0.85, 0.6], ((4.0, 4.5), (2.0, 2.5)), 0.15, 6.0, soft=0.8))
    fx("Explosion Fire", "Explosion", (0, 0.1, 0), burst(**dict(FIRE_SHEET,
        emission_shape_scale=[0.4, 0.3, 0.4], max_particles=24, lifetime=1.4, position_easing="OutCubic",
        texture_sheet_cycles=1, texture_sheet_randomize=False, rotation=[0, 360], rotation_speed=[-25, 25],
        soft_fade_distance=0.8, color_intensity=6.0, velocity_gradient=span((0.4, 1.0)),
        scale_gradient=span((2.6, 3.2), (4.6, 5.4)),
        color_gradient=keys((0, [1, 1, 1, 1]), (0.85, [1, 1, 1, 1]), (1, [1, 1, 1, 0])))))
    # Smoke takes over from the fire: many soft puffs that start lit orange by it (and see-through, so
    # the fire shows first), then grey, rise and spread. One big sprite reads as a dark ball.
    fx("Explosion Smoke Billow", "Explosion", (0, 0.2, 0), burst(**dict(SMOKE_PUFFS,
        emission_shape_scale=[0.9, 0.6, 0.9], max_particles=48, lifetime=2.8, position_easing="OutCubic",
        rotation_speed=[8, 24], random_rotation_direction=True, soft_fade_distance=1.0,
        force_over_lifetime=[0, 0.45, 0], velocity_gradient=span((0.8, 2.0)),
        scale_gradient=span((1.3, 1.9), (3.2, 4.4)),
        color_gradient=keys((0, [0.85, 0.42, 0.16, 0]), (0.1, [0.66, 0.38, 0.2, 0.6]),
                            (0.35, [0.4, 0.36, 0.33, 0.55]), (0.75, [0.42, 0.41, 0.4, 0.32]),
                            (1, [0.45, 0.45, 0.45, 0])))))
    fx("Explosion Column", "Explosion", (0, -0.2, 0), stream(**dict(SMOKE_PUFFS,
        shape="sphere", direction="up", emission_shape_scale=[0.8, 0.3, 0.8], max_particles=32, emission_rate=10.0,
        emission_lifetime=1.2, lifetime=4.5, position_easing="OutQuad", rotation_speed=[10, 25],
        random_rotation_direction=True, soft_fade_distance=0.8, force_over_lifetime=[0, 0.15, 0],
        velocity_gradient=span((2.5, 4.0)), scale_gradient=span((1.2, 1.6), (3.6, 4.4)),
        color_gradient=keys((0, [0.13, 0.12, 0.11, 0]), (0.12, [0.13, 0.12, 0.11, 0.55]),
                            (0.5, [0.18, 0.17, 0.16, 0.35]), (1, [0.25, 0.25, 0.25, 0])))))
    fx("Explosion Debris", "Explosion", (0, -0.7, 0), burst(**dict(SPARK,
        shape="hemisphere", emission_shape_scale=[0.4, 0.4, 0.4], max_particles=256, lifetime=1.4,
        gravity_scale=1.0, position_easing="OutQuad", initial_scale_3d=[1, 5, 1], color_intensity=20.0,
        velocity_gradient=span((4.0, 9.0)), scale_gradient=span((0.1, 0.16), (0.03, 0.05)),
        color_gradient=keys((0, [1, 0.9, 0.6, 1]), (0.5, [1, 0.5, 0.15, 1]), (1, [0.7, 0.15, 0.05, 0])))))
    fx("Explosion Chunks", "Explosion", (0, -0.4, 0), burst(**dict(FIRE_SHEET,
        shape="hemisphere", emission_shape_scale=[0.4, 0.4, 0.4], max_particles=32, lifetime=1.3,
        gravity_scale=0.8, texture_sheet_cycles=1, texture_sheet_randomize=False,
        rotation=[0, 360], rotation_speed=[180, 360], random_rotation_direction=True, color_intensity=3.0,
        velocity_gradient=span((2.5, 5.0)), scale_gradient=span((0.5, 0.7), (0.12, 0.2)),
        color_gradient=keys((0, [1, 1, 1, 1]), (1, [1, 0.8, 0.6, 0])))))
    fx("Explosion Dust Ring", "Explosion", (0, -0.75, 0), burst(
        max_particles=4, lifetime=1.6, texture=f"{V}/dust_ring.png", render_mode="horizontal_billboard",
        blend_mode="normal", rotation=[0, 360], scale_gradient=punch((1.5, 1.5), (10.0, 10.0)),
        color_gradient=keys((0, [0.42, 0.38, 0.34, 0.7]), (0.4, [0.42, 0.38, 0.34, 0.35]), (1, [0.42, 0.38, 0.34, 0]))))
    fx("Explosion Shock Ring", "Explosion", (0, -0.7, 0), ring([1.0, 0.65, 0.3], (1.0, 10.0), 0.4, 7.0))
    fx("Explosion Embers", "Explosion", (0, 1.2, 0), burst(
        emission_shape_scale=[2.0, 1.2, 2.0], max_particles=64, lifetime=3.0, gravity_scale=0.12,
        texture=f"{P}/ember.png", render_mode="billboard", blend_mode="additive", color_intensity=10.0,
        velocity_gradient=span((0.5, 1.5)), scale_gradient=span((0.04, 0.08), (0.01, 0.02)),
        color_gradient=twinkle([1.0, 0.6, 0.2])))
    fx("Explosion Scorch", "Explosion", (0, -0.78, 0), decal(f"{P}/scorch.png", (3.6, 4.4), [0.04, 0.035, 0.03], 0.85, 9.0))
    fx("Explosion Scorch Glow", "Explosion", (0, -0.77, 0), burst(
        max_particles=8, lifetime=3.0, texture=f"{V}/scorch_embers.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=6.0, rotation=[0, 360], scale_gradient=span((3.2, 3.8)),
        color_gradient=keys((0, [1, 0.8, 0.4, 1]), (0.3, [1, 0.45, 0.12, 0.8]), (1, [0.8, 0.2, 0.05, 0]))))
    fx("Explosion Light", "Explosion", (0, 0.8, 0), None, light(ORANGE, 0.0, 18.0, enabled=False))


def define_frost():
    fx("Frost Nova", "Spells", (0, 0, 0))
    fx("Frost Flash", "Frost Nova", (0, 0.8, 0), flash(CYAN, ((4.0, 4.5), (2.0, 2.5)), 0.2, 8.0))
    fx("Frost Spikes Glow", "Frost Nova", (0, 0, 0), burst(
        shape="circle", emission_shape_scale=[0.02, 0.02, 0.02], max_particles=4, lifetime=2.0,
        texture=f"{P}/glow.png", render_mode="horizontal_billboard", blend_mode="additive", color_intensity=2.0,
        scale_gradient=pop_in((6.5, 6.5), (6.0, 6.0), seconds=0.08, life=2.0, overshoot=1.05),
        color_gradient=keys((0, CYAN + [0]), (0.08, CYAN + [0.8]), (0.9, CYAN + [0.5]), (0.96, CYAN + [0]),
                            (1, CYAN + [0]))))
    # The crystals shatter where they stand: FrostNovaSpell emits from each crystal's position.
    fx("Frost Shatter", "Frost Nova", (0, 0.8, 0), burst(
        shape="sphere", spawn_location="surface", emission_shape_scale=[0.25, 0.4, 0.25], max_particles=192,
        lifetime=1.0, gravity_scale=0.9, position_easing="OutQuad", texture=f"{V}/crystal_shard_2x2.png",
        texture_sheet_tiles=[2, 2], texture_sheet_randomize=True, render_mode="billboard", align_to_direction=True,
        blend_mode="additive", color_intensity=3.5, velocity_gradient=span((1.2, 2.8)),
        scale_gradient=span((0.3, 0.45), (0.12, 0.2)),
        color_gradient=keys((0, [0.85, 0.95, 1, 1]), (0.6, CYAN + [0.8]), (1, CYAN + [0]))))
    fx("Frost Shatter Dust", "Frost Nova", (0, 0.8, 0), burst(**dict(SMOKE_PUFFS,
        emission_shape_scale=[0.3, 0.5, 0.3], max_particles=64, lifetime=1.4, position_easing="OutCubic",
        blend_mode="additive", rotation_speed=[20, 60], random_rotation_direction=True, soft_fade_distance=0.4,
        color_intensity=1.2, velocity_gradient=span((0.3, 0.8)), scale_gradient=span((0.6, 0.9), (1.4, 1.9)),
        color_gradient=keys((0, [0.8, 0.95, 1, 0]), (0.1, [0.8, 0.95, 1, 0.5]), (1, [0.6, 0.85, 1, 0])))))
    fx("Frost Shards", "Frost Nova", (0, 0.5, 0), burst(
        shape="sphere", spawn_location="surface", emission_shape_scale=[0.4, 0.12, 0.4], max_particles=64,
        lifetime=0.6, position_easing="OutCubic", texture=f"{V}/crystal_shard_2x2.png", texture_sheet_tiles=[2, 2],
        texture_sheet_randomize=True, render_mode="billboard", align_to_direction=True, blend_mode="additive",
        color_intensity=4.0, velocity_gradient=span((3.0, 4.2)), scale_gradient=span((0.6, 0.8), (0.15, 0.2)),
        color_gradient=keys((0, [0.85, 0.95, 1, 1]), (0.4, [0.7, 0.9, 1, 0.9]), (1, [0.6, 0.85, 1, 0]))))
    fx("Frost Ring", "Frost Nova", (0, 0.08, 0), ring(CYAN, (0.8, 8.5), 0.6, 5.0))
    fx("Frost Mist", "Frost Nova", (0, 0.3, 0), burst(**dict(SMOKE_PUFFS,
        spawn_location="surface", emission_shape_scale=[0.6, 0.1, 0.6], max_particles=48, lifetime=2.6,
        position_easing="OutCubic", rotation_speed=[5, 20], random_rotation_direction=True, soft_fade_distance=1.0,
        velocity_gradient=span((2.5, 3.8)), scale_gradient=span((1.4, 1.8), (3.0, 3.6)),
        color_gradient=keys((0, [0.85, 0.94, 1, 0]), (0.1, [0.85, 0.94, 1, 0.3]), (0.5, [0.85, 0.94, 1, 0.18]),
                            (1, [0.85, 0.94, 1, 0])))))
    fx("Frost Patch", "Frost Nova", (0, 0.03, 0), decal(f"{V}/frost_decal.png", (7.0, 7.5), [0.85, 0.95, 1.0], 0.7, 5.0,
                                                          spread_seconds=0.7))
    fx("Frost Snow", "Frost Nova", (0, 2.5, 0), burst(
        shape="box", direction="outward", emission_shape_scale=[3.5, 1.5, 3.5], max_particles=64, lifetime=3.0,
        gravity_scale=0.05, texture=f"{V}/snowflake_2x2.png", texture_sheet_tiles=[2, 2], texture_sheet_randomize=True,
        render_mode="billboard", blend_mode="additive", color_intensity=4.0, rotation=[0, 360],
        rotation_speed=[30, 90], random_rotation_direction=True, soft_fade_distance=0.2,
        velocity_gradient=span((0.1, 0.3)), scale_gradient=span((0.08, 0.14)), color_gradient=twinkle([0.85, 0.95, 1.0])))
    fx("Frost Sparkles", "Frost Nova", (0, 0.6, 0), burst(
        shape="box", direction="up", emission_shape_scale=[3.2, 0.6, 3.2], max_particles=128, lifetime=2.0,
        texture=f"{P}/sparkle.png", render_mode="billboard", blend_mode="additive", color_intensity=6.0,
        rotation=[0, 360], velocity_gradient=span((0.2, 0.5)), scale_gradient=span((0.18, 0.3), (0.02, 0.05)),
        color_gradient=twinkle([0.8, 0.95, 1.0])))
    fx("Frost Light", "Frost Nova", (0, 1.5, 0), None, light(CYAN, 0.0, 12.0, enabled=False))


def define_vortex():
    # The spell entity holds only the script; the effect is a prefab every cast takes a pooled instance of.
    fx("Arcane Vortex", "Spells", (0, 0, 0))
    fx("Vortex Effect", None, (0, 0, 0))
    fx("Vortex Runes Outer", "Vortex Effect", (0, 0.04, 0), burst(
        max_particles=4, lifetime=3.6, texture=f"{P}/rune_circle.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=3.5, rotation_speed=[35, 35], scale_gradient=pop_in((6.0, 6.0), seconds=0.18, life=3.6),
        color_gradient=fade(VIOLET, 0.1, 0.15)))
    fx("Vortex Runes Inner", "Vortex Effect", (0, 0.05, 0), burst(
        max_particles=4, lifetime=3.6, texture=f"{P}/rune_circle.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=4.5, rotation_speed=[-70, -70], scale_gradient=pop_in((3.2, 3.2), seconds=0.15, life=3.6),
        color_gradient=fade(PINK, 0.15, 0.15)))
    fx("Vortex Glyphs", "Vortex Effect", (0, 0.2, 0), ambient(
        shape="circle", direction="up", spawn_location="surface", emission_shape_scale=[2.8, 0.02, 2.8],
        max_particles=96, emission_rate=0.0, lifetime=2.0, culling_mode="always_simulate", texture=f"{V}/glyphs_4x4.png",
        texture_sheet_tiles=[4, 4], texture_sheet_randomize=True, render_mode="billboard", blend_mode="additive",
        color_intensity=7.0, velocity_gradient=span((2.0, 3.0)), scale_gradient=pop_in((0.35, 0.5), seconds=0.08, life=2.0, overshoot=1.15),
        color_gradient=keys((0, VIOLET + [0]), (0.2, VIOLET + [1]), (0.7, PINK + [0.8]), (1, PINK + [0]))))
    fx("Vortex Wisps", "Vortex Effect", (0, 0.4, 0), ambient(
        shape="circle", direction="inward", spawn_location="surface", emission_shape_scale=[3.0, 0.05, 3.0],
        max_particles=256, emission_rate=0.0, lifetime=1.2, simulation_space="local", culling_mode="always_simulate",
        position_easing="InQuad", texture=f"{V}/wisp_2x2.png", texture_sheet_tiles=[2, 2], texture_sheet_randomize=True,
        render_mode="billboard", blend_mode="additive", color_intensity=7.0, rotation=[0, 360],
        rotation_speed=[120, 240], random_rotation_direction=True, velocity_gradient=span((2.6, 3.0)),
        scale_gradient=span((1.0, 1.6), (0.3, 0.5)),
        color_gradient=keys((0, VIOLET + [0]), (0.2, VIOLET + [0.9]), (0.8, PINK + [0.8]), (1, PINK + [0]))))
    fx("Vortex Swirl", "Vortex Effect", (0, 0.3, 0), ambient(**dict(SPARK,
        shape="circle", direction="inward", spawn_location="surface", emission_shape_scale=[3.2, 0.05, 3.2],
        max_particles=400, emission_rate=0.0, lifetime=1.1, simulation_space="local", culling_mode="always_simulate",
        position_easing="InQuad", initial_scale_3d=[1, 3, 1], color_intensity=10.0,
        velocity_gradient=span((2.8, 3.1)), scale_gradient=span((0.16, 0.22), (0.07, 0.1)),
        color_gradient=keys((0, [0.9, 0.6, 1, 0]), (0.15, [0.9, 0.6, 1, 1]), (0.85, [0.6, 0.35, 1, 0.8]),
                            (1, [0.5, 0.3, 1, 0])))))
    fx("Vortex Column", "Vortex Effect", (0, 0, 0), ambient(
        shape="sphere", emission_shape_scale=[0.05, 0.01, 0.05], max_particles=32, emission_rate=0.0, lifetime=0.5,
        culling_mode="always_simulate", texture=f"{V}/beam_4x1.png", texture_sheet_tiles=[4, 1],
        texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 1.0], initial_scale_3d=[1, 5, 1],
        blend_mode="additive", color_intensity=4.0, velocity_gradient=span((0.0, 0.0)),
        scale_gradient=span((0.8, 1.2)), color_gradient=fade(VIOLET, 0.3, 0.4, 0.6)))
    fx("Vortex Core", "Vortex Effect", (0, 0.6, 0), ambient(
        shape="sphere", emission_shape_scale=[0.05, 0.05, 0.05], max_particles=16, emission_rate=0.0, lifetime=0.45,
        simulation_space="local", culling_mode="always_simulate", texture=f"{P}/glow.png", render_mode="billboard",
        blend_mode="additive", color_intensity=5.0, rotation=[0, 360], velocity_gradient=span((0.0, 0.0)),
        scale_gradient=span((1.4, 1.8), (0.8, 1.0)), color_gradient=fade([0.8, 0.5, 1.0], 0.3, 0.4, 0.8)))
    fx("Vortex Flash", "Vortex Effect", (0, 0.8, 0), flash([0.9, 0.7, 1.0], ((5.0, 6.0), (2.0, 2.5)), 0.2, 14.0))
    fx("Vortex Burst", "Vortex Effect", (0, 0.6, 0), burst(**dict(SPARK,
        spawn_location="surface", emission_shape_scale=[0.3, 0.3, 0.3], max_particles=256, lifetime=0.9,
        position_easing="OutExpo", initial_scale_3d=[1, 3, 1], color_intensity=12.0,
        velocity_gradient=span((3.5, 6.0)), scale_gradient=span((0.12, 0.18), (0.04, 0.06)),
        color_gradient=keys((0, [1, 0.8, 1, 1]), (0.5, [0.7, 0.4, 1, 1]), (1, [0.4, 0.2, 1, 0])))))
    fx("Vortex Burst Wisps", "Vortex Effect", (0, 0.6, 0), burst(
        spawn_location="surface", emission_shape_scale=[0.5, 0.3, 0.5], max_particles=64, lifetime=1.0,
        position_easing="OutExpo", texture=f"{V}/wisp_2x2.png", texture_sheet_tiles=[2, 2], texture_sheet_randomize=True,
        render_mode="billboard", blend_mode="additive", color_intensity=7.0, rotation=[0, 360],
        rotation_speed=[200, 400], random_rotation_direction=True, velocity_gradient=span((2.5, 4.5)),
        scale_gradient=span((1.2, 1.8), (0.6, 0.9)), color_gradient=fade(PINK, 0.05, 0.6)))
    fx("Vortex Burst Glyphs", "Vortex Effect", (0, 1.0, 0), burst(
        spawn_location="surface", emission_shape_scale=[0.4, 0.4, 0.4], max_particles=32, lifetime=1.2,
        position_easing="OutExpo", texture=f"{V}/glyphs_4x4.png", texture_sheet_tiles=[4, 4],
        texture_sheet_randomize=True, render_mode="billboard", blend_mode="additive", color_intensity=8.0,
        velocity_gradient=span((2.0, 3.5)), scale_gradient=pop_in((0.4, 0.55), (0.2, 0.3), seconds=0.05, life=1.2, overshoot=1.15), color_gradient=fade(VIOLET, 0.05, 0.6)))
    fx("Vortex Ring", "Vortex Effect", (0, 0.1, 0), ring(VIOLET, (1.0, 11.0), 0.55, 8.0))
    fx("Vortex Smoke", "Vortex Effect", (0, 0.6, 0), burst(**dict(SMOKE_PUFFS,
        emission_shape_scale=[0.8, 0.4, 0.8], max_particles=32, lifetime=2.6, position_easing="OutCubic",
        rotation_speed=[10, 30], random_rotation_direction=True, soft_fade_distance=0.6,
        force_over_lifetime=[0, 0.2, 0], velocity_gradient=span((0.8, 1.6)), scale_gradient=span((1.0, 1.4), (2.4, 3.0)),
        color_gradient=keys((0, [0.3, 0.18, 0.4, 0]), (0.12, [0.3, 0.18, 0.4, 0.4]), (1, [0.25, 0.2, 0.3, 0])))))
    fx("Vortex Light", "Vortex Effect", (0, 1.5, 0), None, light(VIOLET, 0.0, 12.0, enabled=False))


def define_holy():
    fx("Holy Light", "Spells", (0, 0, 0))
    fx("Holy Beam", "Holy Light", (0, 0, 0), burst(
        emission_shape_scale=[0.1, 0.01, 0.1], max_particles=8, lifetime=2.0, texture=f"{V}/beam_4x1.png",
        texture_sheet_tiles=[4, 1], texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 1.0],
        initial_scale_3d=[1, 4.5, 1], blend_mode="additive", color_intensity=3.5, scale_gradient=span((1.6, 3.0)),
        color_gradient=keys((0, GOLD + [0]), (0.08, GOLD + [1]), (0.7, GOLD + [0.85]), (1, GOLD + [0]))))
    fx("Holy Core Beam", "Holy Light", (0, 0, 0), burst(
        max_particles=4, lifetime=1.8, texture=f"{V}/beam_4x1.png", texture_sheet_tiles=[4, 1],
        texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 1.0], initial_scale_3d=[1, 12, 1],
        blend_mode="additive", color_intensity=10.0, scale_gradient=span((0.5, 0.7)),
        color_gradient=keys((0, [1, 0.95, 0.8, 0]), (0.06, [1, 0.95, 0.8, 1]), (0.6, [1, 0.9, 0.7, 0.8]),
                            (1, [1, 0.9, 0.7, 0]))))
    fx("Holy Rays", "Holy Light", (0, 0, 0), stream(
        shape="circle", direction="up", spawn_location="inside", emission_shape_scale=[2.2, 0.02, 2.2],
        max_particles=32, emission_rate=10.0, emission_lifetime=1.2, lifetime=0.7, texture=f"{V}/beam_4x1.png",
        texture_sheet_tiles=[4, 1], texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 1.0],
        initial_scale_3d=[1, 14, 1], blend_mode="additive", color_intensity=5.0, velocity_gradient=span((0.0, 0.0)),
        scale_gradient=span((0.25, 0.4)), color_gradient=twinkle(GOLD)))
    fx("Holy Runes", "Holy Light", (0, 0.05, 0), burst(
        max_particles=4, lifetime=2.4, texture=f"{P}/rune_circle.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=3.5, rotation_speed=[25, 25], scale_gradient=pop_in((4.5, 4.5), (5.0, 5.0), seconds=0.15, life=2.4),
        color_gradient=fade(GOLD, 0.12, 0.3)))
    fx("Holy Rings", "Holy Light", (0, 0.06, 0), ring(GOLD, (0.6, 6.0), 1.0, 4.0))
    fx("Holy Glow", "Holy Light", (0, 0.03, 0), burst(
        max_particles=4, lifetime=2.0, texture=f"{P}/glow.png", render_mode="horizontal_billboard",
        blend_mode="additive", color_intensity=2.5, scale_gradient=span((4.5, 5.0)), color_gradient=fade(GOLD, 0.2, 0.4, 0.7)))
    fx("Holy Descent", "Holy Light", (0, 9.0, 0), stream(
        shape="box", direction="up", emission_shape_scale=[1.2, 0.5, 1.2], max_particles=96, emission_rate=50.0,
        emission_lifetime=1.0, lifetime=1.3, position_easing="InSine", texture=f"{P}/sparkle.png",
        render_mode="billboard", blend_mode="additive", color_intensity=8.0, rotation=[0, 360],
        rotation_speed=[60, 180], random_rotation_direction=True, velocity_gradient=span((7.0, 8.5)),
        scale_gradient=span((0.12, 0.2)), color_gradient=twinkle(GOLD)), rotation=(180, 0, 0))
    fx("Holy Rise", "Holy Light", (0, 0.1, 0), stream(
        shape="circle", direction="up", emission_shape_scale=[1.3, 0.05, 1.3], max_particles=256, emission_rate=60.0,
        emission_lifetime=1.4, lifetime=1.6, texture=f"{P}/sparkle.png", render_mode="billboard", blend_mode="additive",
        color_intensity=7.0, rotation=[0, 360], rotation_speed=[30, 90], random_rotation_direction=True,
        velocity_gradient=span((2.5, 4.5)), scale_gradient=span((0.14, 0.22), (0.02, 0.04)), color_gradient=twinkle(GOLD)))
    fx("Holy Heal", "Holy Light", (0, 0, 0), burst(
        emission_shape_scale=[0.4, 0.6, 0.4], direction="up", max_particles=64, lifetime=1.4,
        texture=f"{V}/heal_plus.png", render_mode="billboard", blend_mode="additive", color_intensity=6.0,
        velocity_gradient=span((1.0, 1.6)), scale_gradient=span((0.18, 0.26), (0.1, 0.14)),
        color_gradient=fade([0.75, 1.0, 0.55], 0.15, 0.4)))
    fx("Holy Light Glow", "Holy Light", (0, 2.0, 0), None, light(GOLD, 0.0, 12.0, enabled=False))


def define_spells():
    fx("Spells", None, (0, 0, 0))
    define_lightning()
    define_fireball()
    define_frost()
    define_vortex()
    define_holy()


# ============================================================================================ scripts

def script_bindings():
    out = []

    def attach(name, script, **fields):
        out.append((name, NS + script, fields))

    for k in range(1, 5):
        attach(f"Brazier {k}", "Brazier")
        attach(f"Brazier {k} Light", "Flicker", amount=0.25, speed=1.0)
        attach(f"Brazier {k} Smoke", "WindAffected", response=1.0)
        attach(f"Brazier {k} Embers", "WindAffected", response=0.6)
    attach("Ground Mist", "WindAffected", response=0.5)
    attach("Mana Fountain Mist", "WindAffected", response=0.8)
    attach("Explosion Column", "WindAffected", response=1.0)
    for k in range(1, 4):
        attach(f"Fireball Projectile {k} Smoke", "WindAffected", response=0.5)
    attach("Mana Fountain Light", "Flicker", amount=0.1, speed=0.4)
    attach("Focus Light", "Flicker", amount=0.15, speed=0.5)
    for light_name in ("Lightning Light", "Explosion Light", "Frost Light", "Vortex Light", "Holy Light Glow"):
        attach(light_name, "LightFlash")
    attach("Lightning", "LightningSpell", castSound=f"{AUDIO}/thunder.wav", castVolume=0.9)
    attach("Fireball", "FireballSpell", castSound=f"{AUDIO}/fireball_cast.wav", castVolume=0.7)
    attach("Frost Nova", "FrostNovaSpell", castSound=f"{AUDIO}/frost.wav", castVolume=0.8)
    attach("Arcane Vortex", "ArcaneVortexSpell", castSound=f"{AUDIO}/vortex.wav", castVolume=0.8, cooldown=0.5)
    attach("Holy Light", "HolyLightSpell", castSound=f"{AUDIO}/holy.wav", castVolume=0.8)
    return out


# ============================================================================================ MCP plumbing

IDS = {}


def find_id(name):
    found = call("scene_find_entities_batch", {"name_exact": name}).get("entities", [])
    return found[0]["id"] if found else None


def report(result):
    for item in result.get("results", []):
        if not item.get("ok", True) or item.get("errors") or item.get("unknown"):
            print("PROBLEM", json.dumps(item)[:700])
    if result.get("ok") is False:
        print("PROBLEM", json.dumps(result)[:700])


def rebuild():
    old = [i for i in (find_id(n) for n in ("Ambient", "Spells", "Vortex Effect", "Frost Crystals", "Focus Motes",
                                              "Focus Wisps", "Focus Halo", "Focus Light")) if i]
    if old:
        call("scene_delete_entities_batch", {"entity_ids": old})
    IDS["Spell Focus"] = find_id("Spell Focus")
    define_ambient()
    define_spells()
    for name, parent, position, emitter, light_props in EFFECTS:
        item = {"name": name, "position": position, "rotation_euler": ROTATIONS.get(name, [0, 0, 0])}
        if parent:
            item["parent_id"] = IDS[parent]
            item["space"] = "local"
        else:
            item["space"] = "world"
        IDS[name] = call("scene_create_entities_batch", {"items": [item]})["created"][0]["id"]
    adds = [{"entity_id": IDS[n], "component_type": "Particle Emitter"} for n, _, _, e, _ in EFFECTS if e is not None]
    adds += [{"entity_id": IDS[n], "component_type": "Light"} for n, _, _, _, l in EFFECTS if l is not None]
    call("scene_add_components_batch", {"items": adds})
    batch = [{"entity_id": IDS[n], "component": "Particle Emitter", "properties": with_sort_priority(n, e)}
             for n, _, _, e, _ in EFFECTS if e]
    for start in range(0, len(batch), 10):
        report(call("scene_set_component_properties_batch", {"items": batch[start:start + 10]}))
    report(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": IDS[n], "component": "Light", "properties": l} for n, _, _, _, l in EFFECTS if l]}))
    bindings = script_bindings()
    call("scene_add_scripts_batch", {"items": [{"entity_id": IDS[n], "type_name": s} for n, s, _ in bindings]})
    report(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": IDS[n], "component": "Script", "type_name": s, "properties": f} for n, s, f in bindings if f]}))
    make_prefab("Vortex Effect", "app:/data/Prefabs", "ArcaneVortex")
    build_frost_crystals()
    print("effects", len(EFFECTS))


FROST_CRYSTAL_COUNT = 22
ICE = {"base_color": [0.78, 0.9, 1.0, 1.0], "roughness": 0.05, "metalness": 0.0,
       "emissive_color": [0.55, 0.85, 1.0, 1.0], "emissive_intensity": 1.1}


def build_frost_crystals():
    """The Frost Nova crystal ring: faceted ice meshes in a prefab the spell pools, one instance per cast.
    The spell lays the ring out and grows each crystal, so they are saved tiny."""
    here = os.path.dirname(os.path.abspath(__file__))
    meshes = [os.path.join(here, "particles_src", "Meshes", f"ice_cluster_{v}.glb") for v in "abc"]
    call("assets_import_files", {"paths": meshes, "folder": "app:/data/Models", "wait_ms": 20000})
    existing = {a["location"] for a in call("assets_find_batch", {"prefix": "app:/data/Materials/"}).get("assets", [])}
    if "app:/data/Materials/Ice.mat" not in existing:
        call("materials_create_batch", {"items": [{"folder": "app:/data/Materials", "name": "Ice"}], "wait_ms": 10000})
    call("materials_set", {"key": "app:/data/Materials/Ice.mat", "properties": ICE})
    root = call("scene_create_entities_batch", {"items": [{"name": "Frost Crystals", "space": "world"}]})["created"][0]["id"]
    created = call("scene_create_meshes_batch", {"items": [
        {"asset_key": f"app:/data/Models/ice_cluster_{'abc'[k % 3]}.glb", "name": f"Frost Crystal {k + 1}",
         "parent_id": root, "space": "local", "scale": [0.001, 0.001, 0.001]} for k in range(FROST_CRYSTAL_COUNT)]})
    ids = [c["id"] for c in created["created"]]
    call("scene_set_model_materials_batch", {"items": [{"entity_id": i, "material_key": "app:/data/Materials/Ice.mat"}
                                                       for i in ids]})
    report(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": i, "component": "Model", "properties": {"static": False}} for i in ids]}))
    make_prefab("Frost Crystals", "app:/data/Prefabs", "FrostCrystals", root)


def make_prefab(name, folder, prefab_name, entity=None):
    """Saves the effect built in the scene as a prefab and removes the scene copy: the spell pools instances."""
    entity = entity or (IDS[name] if name in IDS else find_id(name))
    print(json.dumps(call("prefabs_create_from_entities_batch", {"items": [
        {"entity_id": entity, "folder": folder, "name": prefab_name}], "wait_ms": 10000}))[:300])
    call("scene_delete_entities_batch", {"entity_ids": [entity]})


def tune(name):
    if not EFFECTS:
        define_ambient()
        define_spells()
    for effect_name, _, position, emitter, light_props in EFFECTS:
        if effect_name == name:
            entity = find_id(name)
            if emitter:
                report(call("scene_set_component_properties_batch", {"items": [
                    {"entity_id": entity, "component": "Particle Emitter",
                     "properties": with_sort_priority(name, emitter)}]}))
            if light_props:
                report(call("scene_set_component_properties_batch", {"items": [
                    {"entity_id": entity, "component": "Light", "properties": light_props}]}))
            print("tuned", name)


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "tune":
        for target in sys.argv[2:]:
            tune(target)
    else:
        rebuild()
