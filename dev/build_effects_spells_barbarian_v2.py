"""Barbarian class weapons, second set: War Cry / Warlord's Bellow and Butcher's Chains / Maw of the Pit. Data only,
like build_effects_spells.py (read its notes first); the first set lives in build_effects_spells_barbarian.py and is
reused by name where it already looks right (dust, rocks, grit, shockwave, dust ring, cracks, Fury flames).

    python build_effects_spells_barbarian_v2.py                         list the effects defined here (no editor call)
    python build_effects_spells_barbarian_v2.py build [BarbarianFx2]    create missing effects and (re)apply their settings

Palette: earthy dust and rock keep their own colours (fired untinted); the roar's air is pale and turns red as it
spreads (blood red at full Fury, the script's tint); iron chains are cold grey (Maw of the Pit tints them with old
blood); blood is a dark, non-glowing crimson, and only the Fury motes and the Maw's swirl glow red.
Chain links and hooks are drawn every frame by the script with a lifetime of ~1.6 frames (one render each), so their
gradients hold full alpha until 75% of the life and only scripted, longer-lived links (the slam) fade out.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import P  # noqa: E402
from build_effects_spells import K, W, A, GROUND, ADD, SMOKE_LIT  # noqa: E402
from build_effects_spells_barbarian import HEADED, TRACE, STREAK, DUST, DUST_SETTLED, DUST_WAVE, ROCK  # noqa: E402
from effect_helpers import Registry, shot, sizes, keys, grow  # noqa: E402

GROUP = "BarbarianFx2"
REGISTRY = Registry((GROUP,))


def define(name, props, sort):
    """Every emitter of this set lives in one group."""
    REGISTRY.define(GROUP, name, props, sort)


SCRIPTED = {"lifetime": [1.0, 1.0]}
ROAR_PALE = [1.0, 0.88, 0.76]
ROAR_RED = [0.92, 0.24, 0.2]
SHADE = [0.15, 0.1, 0.075]
IRON = [0.74, 0.72, 0.68]
IRON_EDGE = [0.56, 0.54, 0.51]
HOOK = [0.84, 0.82, 0.78]
FURROW = [0.085, 0.065, 0.05]
BLOOD = [0.5, 0.03, 0.03]
BLOOD_BRIGHT = [0.72, 0.06, 0.05]
VORTEX = [0.13, 0.012, 0.016]
CRIMSON = [0.9, 0.12, 0.1]
MIST = [0.4, 0.06, 0.05]
MIST_DARK = [0.22, 0.05, 0.05]
# circle_03: a thick ring with a soft sheen filling ~90% of the quad. Headed (the top of the texture along the chain), squeezed
# across: a flat link reads as an oval, an edge-on link as a narrow sliver between two flat ones.
LINK = {"texture": f"{K}/circle_03.png", "render_mode": "horizontal_billboard", "align_to_direction": True,
        "blend_mode": "normal"}
HOLD = ((0.0, 1.0), (0.75, 1.0), (1.0, 0.0))


def hold(rgb):
    """Full alpha for the scripted short life, a fade over its last quarter (longer-lived links)."""
    return keys(*[(p, rgb + [a]) for p, a in HOLD])


# ------------------------------------------------------------------------------------------------------ war cry
# The roar's air: a soft ring at chest height bursting out to the radius (the script's size is the PEAK quad, the
# ring sits at 0.45 x it), pale at its crest and red-tinged as it spreads; the script tints it blood red at full Fury.
define("BarbarianRoarWave", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{K}/circle_04.png", render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.3, size_over_life=grow(0.4),
    color_gradient=keys((0.0, ROAR_PALE + [0.0]), (0.06, ROAR_PALE + [0.9]), (0.45, ROAR_RED + [0.55]),
                        (1.0, ROAR_RED + [0.0]))), 4)
# The trough behind the crest: a dark band just inside it, so the wave reads like shimmering air, not a glow.
define("BarbarianRoarShade", shot(**SCRIPTED,
    max_particles=64, texture=f"{K}/circle_04.png", render_mode="horizontal_billboard", blend_mode="normal",
    primitive="octagon", start_rotation=[0, 360], size_over_life=grow(0.45),
    color_gradient=keys((0.0, SHADE + [0.0]), (0.08, SHADE + [0.45]), (0.6, SHADE + [0.25]), (1.0, SHADE + [0.0]))), -6)
# Dust blown out across the radius by the roar (EmitMoving: launched at 2.6 m/s per metre of radius, this drag settles
# it near the rim).
define("BarbarianRoarDust", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.8, 1.2], drag=3.0, acceleration=[0.0, 0.4, 0.0],
    size_over_life=sizes((0.0, 1.0), (1.0, 2.2, 2.8)),
    color_gradient=keys((0.0, DUST_WAVE + [0.0]), (0.08, DUST_WAVE + [0.7]), (0.5, DUST + [0.45]),
                        (1.0, DUST_SETTLED + [0.0]))), 2)
# Fright lines: three short streaks flicking up from a skull in a fan (Emit at the head, tinted like the roar).
define("BarbarianFright", shot(**ADD, **STREAK,
    max_particles=1024, lifetime=[0.28, 0.36], shape="cone", cone_angle=40.0, direction="up",
    start_speed=[3.0, 5.0], drag=7.0, stretch_length_scale=1.0, stretch_speed_scale=0.05, color_intensity=2.2,
    size_over_life=sizes((0.0, 0.3, 0.38), (1.0, 0.12, 0.16)),
    color_gradient=keys((0.0, [1.0, 0.94, 0.86, 1.0]), (0.5, [1.0, 0.62, 0.52, 0.8]), (1.0, [1.0, 0.32, 0.22, 0.0]))), 9)
# Warlord's Bellow: shards of rock torn from the ground fly to the barbarian and clad him (EmitMoving to his chest,
# scripted flight time); the crumble when the armor ends drifts them off again.
define("BarbarianStoneSkin", shot(
    max_particles=256, lifetime=[0.3, 0.3], texture=f"{W}/rock_chunk_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", blend_mode="normal", lighting="surface",
    start_rotation=[0, 360], spin_speed=[400, 900], random_spin_direction=True,
    size_over_life=sizes((0.0, 1.0), (0.8, 0.9), (1.0, 0.3)),
    color_gradient=keys((0.0, ROCK + [0.0]), (0.12, ROCK + [1.0]), (1.0, ROCK + [1.0]))), 5)

# --------------------------------------------------------------------------------------------- butcher's chains
define("BarbarianChainLink", shot(**LINK, **SCRIPTED,
    max_particles=2048, size_axes=[0.56, 1.0, 1.0], size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=hold(IRON)), 6)
define("BarbarianChainLinkEdge", shot(**LINK, **SCRIPTED,
    max_particles=2048, size_axes=[0.2, 1.0, 1.0], size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=hold(IRON_EDGE)), 6)
# The hook: slash_01's crescent, headed back along the chain so its horns point at the barbarian (a barb that
# catches on the pull).
define("BarbarianChainHook", shot(**SCRIPTED,
    max_particles=256, texture=f"{K}/slash_01.png", render_mode="horizontal_billboard", align_to_direction=True,
    blend_mode="normal", size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=hold(HOOK)), 7)
# Dust at the feet of a yanked enemy: launched with the enemy's own yank speed, and this drag is the enemy world's
# knockback damping, so the puff slides exactly as far as the enemy does.
define("BarbarianDragDust", shot(**SMOKE_LIT,
    max_particles=1024, lifetime=[0.6, 0.85], drag=9.0, acceleration=[0.0, 0.3, 0.0],
    size_over_life=sizes((0.0, 1.0), (1.0, 1.9, 2.3)),
    color_gradient=keys((0.0, DUST_WAVE + [0.0]), (0.06, DUST_WAVE + [0.75]), (0.5, DUST + [0.45]),
                        (1.0, DUST_SETTLED + [0.0]))), 2)
# The furrow a dragged enemy ploughs: one headed trace_06 stroke over its whole drag (sized 2.6 x the drag, the
# streak covering ~1.04 x it); the core is ~0.15 m wide on a 4 m drag.
define("BarbarianDragFurrow", shot(**HEADED, **TRACE, **SCRIPTED,
    max_particles=512, blend_mode="normal", size_axes=[0.15, 1.0, 1.0],
    size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, FURROW + [0.0]), (0.05, FURROW + [0.85]), (0.7, FURROW + [0.7]), (1.0, FURROW + [0.0]))), -20)
# Bloodied enemies drip (one drop every ~0.3 s while bloodied): dark, non-glowing droplets that die on the ground.
define("BarbarianBloodDrip", shot(
    max_particles=1024, lifetime=[0.5, 0.7], texture=f"{P}/ember.png", render_mode="stretched", blend_mode="normal",
    shape="cone", cone_angle=30.0, direction="up", start_speed=[0.3, 0.9], gravity_scale=1.5, collision="kill",
    collision_planes=GROUND, stretch_length_scale=1.2, stretch_speed_scale=0.05,
    size_over_life=sizes((0.0, 0.09, 0.13), (1.0, 0.06, 0.09)),
    color_gradient=keys((0.0, BLOOD + [1.0]), (0.85, BLOOD + [1.0]), (1.0, BLOOD + [0.0]))), 6)
# A spray of blood: a hooked enemy, a bloodied death, the Maw's slam.
define("BarbarianBloodSpray", shot(
    max_particles=1024, lifetime=[0.45, 0.75], texture=f"{P}/ember.png", render_mode="stretched", blend_mode="normal",
    shape="hemisphere", direction="outward", start_speed=[2.5, 5.5], gravity_scale=2.0, drag=0.6, collision="kill",
    collision_planes=GROUND, stretch_length_scale=1.3, stretch_speed_scale=0.045,
    size_over_life=sizes((0.0, 0.1, 0.16), (1.0, 0.07, 0.1)),
    color_gradient=keys((0.0, BLOOD_BRIGHT + [1.0]), (0.8, BLOOD + [1.0]), (1.0, BLOOD + [0.0]))), 6)
# A bloodied enemy's death feeds Fury: a red streak flying from the corpse into the barbarian (EmitMoving, scripted
# flight time, tinted Fury red).
define("BarbarianFuryMote", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=f"{P}/ember.png", render_mode="stretched", color_intensity=3.0,
    stretch_length_scale=1.0, stretch_speed_scale=0.04, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.6)),
    color_gradient=keys((0.0, [1.0, 0.75, 0.6, 0.0]), (0.12, [1.0, 0.65, 0.55, 1.0]), (1.0, [1.0, 0.35, 0.25, 0.6]))), 9)
# Maw of the Pit: the blood vortex's dark eye (vortex.png, the arms reach ~0.42 x the quad), collapsing as it turns;
# renewed while the drag lasts so it follows the barbarian.
define("BarbarianBloodVortex", shot(**SCRIPTED,
    max_particles=32, texture=f"{W}/vortex.png", render_mode="horizontal_billboard", blend_mode="normal",
    primitive="octagon", start_rotation=[0, 360], spin_speed=[160, 160], size_over_life=sizes((0.0, 1.0), (1.0, 0.6)),
    color_gradient=keys((0.0, VORTEX + [0.0]), (0.15, VORTEX + [0.85]), (0.75, VORTEX + [0.8]), (1.0, VORTEX + [0.0]))), -14)
# Crimson spiral arms turning over the dark eye (the Maw's only glow besides the Fury motes).
define("BarbarianBloodSwirl", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{A}/twirls_2x2.png", texture_sheet_tiles=[2, 2], texture_sheet_randomize=True,
    render_mode="horizontal_billboard", start_rotation=[0, 360], spin_speed=[260, 340], color_intensity=1.2,
    size_over_life=sizes((0.0, 1.0), (1.0, 0.4)),
    color_gradient=keys((0.0, CRIMSON + [0.0]), (0.15, CRIMSON + [0.8]), (1.0, CRIMSON + [0.0]))), -12)
# Blood mist spiralling into the maw (EmitMoving from the rim, scripted velocity).
define("BarbarianBloodMist", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.7, 1.0], drag=1.2, size_over_life=sizes((0.0, 1.0), (1.0, 1.6, 2.0)),
    color_gradient=keys((0.0, MIST + [0.0]), (0.15, MIST + [0.55]), (1.0, MIST_DARK + [0.0]))), 1)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_barbarian_v2.py")
