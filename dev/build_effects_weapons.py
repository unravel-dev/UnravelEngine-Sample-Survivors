"""Barrowlight weapon effects: every emitter the weapons module (src/Scripts/Weapons/**) finds by name, plus the
projectile trails named in ProjectileCatalog.cs. Data only; build_effects.apply() creates and tunes them.

    python build_effects_weapons.py                 list the effects defined here (no editor call)
    python build_effects_weapons.py build [group]   create missing effects and (re)apply their settings

How the scripts fire them (Effects.cs) decides how each one is authored:
- Emit(id, pos, count): untinted, the gradient carries the colour (trails, EvolveBurst).
- Emit(id, pos, count, color): tinted, the colour multiplies the gradient, so gradients stay white or neutral grey.
- Emit(id, pos, 1, color, size, lifetime), EmitShape, EmitArc: the size is the particle's size AT BIRTH (the engine
  scales size_over_life so its birth value is the size; a curve that starts at 0 is scaled to peak at it), the
  lifetime replaces the emitter's (left at [1, 1]). Those curves are written in units of the passed size.
- A given position replaces the shape's spawn offset but keeps the direction it gives, so every particle of a burst
  starts at the emit point; spreads ("sphere r 1.5") come from a fast launch with strong drag instead.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import (EFFECTS, effect, burst, ambient, keys, span, punch, SPARK, P, V, T,  # noqa: E402,F401
                           WHITE, BONE, GOLD, FIRE, GREEN, VIOLET, CYAN, DANGER)
from effect_helpers import Registry, shot, sizes, scalar, alpha, grow  # noqa: E402

W = "app:/data/Textures/weapons"

GROUPS = ("Knight", "Barbarian", "Mage", "Rogue", "Weapons", "Trails")
REGISTRY = Registry(GROUPS)
define = REGISTRY.define
NAMES = REGISTRY.names

GROUND = [[0.0, 1.0, 0.0, 0.0]]
CRESCENT_GOLD = [1.0, 0.8, 0.35]
EVOLVE_GOLD = [1.0, 0.85, 0.4]
TEAL = [0.4, 1.0, 0.9]
PALE_BLUE = [0.62, 0.85, 1.0]
DISC_CYAN = [0.45, 0.95, 1.0]
ECLIPSE_VIOLET = [0.62, 0.4, 1.0]
WISP_GREEN = [0.75, 1.0, 0.88]
DUSTY = [0.78, 0.72, 0.62]
SMOKE_GREY = [0.6, 0.6, 0.6]
DAGGER_SHADOW = [0.3, 0.15, 0.45]
WIND_DUST = [0.82, 0.78, 0.7]


SCRIPTED = {"lifetime": [1.0, 1.0]}
SMOKE = {"texture": f"{V}/smoke_billow_4x4.png", "texture_sheet_tiles": [4, 4], "texture_sheet_randomize": True,
         "render_mode": "billboard", "blend_mode": "normal", "start_rotation": [0, 360], "spin_speed": [20, 70],
         "random_spin_direction": True}
SPARKLE = {"texture": f"{P}/sparkle.png", "render_mode": "billboard", "blend_mode": "additive",
           "start_rotation": [0, 90], "spin_speed": [90, 270], "random_spin_direction": True}
GLOW = {"texture": f"{P}/glow.png", "render_mode": "billboard", "blend_mode": "additive", "primitive": "octagon"}
GROUND_GLOW = dict(GLOW, render_mode="horizontal_billboard")
RING = {"texture": f"{V}/ring_energy.png", "render_mode": "horizontal_billboard", "blend_mode": "additive",
        "primitive": "octagon", "start_rotation": [0, 360]}
# Rings sized by the script (diameter): this line sits at the rim, so it lands on the hit radius.
SHOCK = dict(RING, texture=f"{W}/shock_ring.png")
ROCKS = {"texture": f"{W}/rock_chunk_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True,
         "render_mode": "billboard", "blend_mode": "normal", "lighting": "surface", "start_rotation": [0, 360],
         "spin_speed": [360, 900], "random_spin_direction": True, "collision": "bounce", "collision_planes": GROUND,
         "restitution": 0.3, "max_bounces": 2}
FLAMES = {"texture": f"{W}/fire_8x8_white.png", "texture_sheet_tiles": [8, 8], "render_mode": "billboard",
          "blend_mode": "additive"}
UP_BIAS = {"shape_position": [0.0, 0.8, 0.0]}
HORIZONTAL_RING = {"shape": "circle", "direction": "outward"}
# Puffs born on the ground: the ring's directions tilt ~20 deg up so they rise out of it instead of half buried.
GROUND_RING = dict(HORIZONTAL_RING, shape_position=[0.0, 0.35, 0.0])


# ================================================================================================ Knight
# EmitArc: one particle per 12 deg of the arc at 0.8 x radius, size 0.45 x radius (1.3-1.9 m), lifetime 0.08-0.14 s
# with the leading end living longest. Overlapping soft discs make one band that thins as it fades.
define("Knight", "OathbladeSlash", shot(
    max_particles=256, **SCRIPTED, texture=f"{P}/soft.png", render_mode="horizontal_billboard", blend_mode="additive",
    primitive="octagon", color_intensity=2.4, size_over_life=sizes((0.0, 1.0), (0.35, 0.85), (1.0, 0.55)),
    color_gradient=alpha((0.0, 0.0), (0.06, 0.95), (0.45, 0.7), (1.0, 0.0))), 2)
define("Knight", "CrescentLaunch", shot(**SPARK,
    max_particles=128, lifetime=[0.25, 0.35], color_intensity=7.0, size_axes=[1, 3, 1], start_speed=[4.0, 7.0], drag=4.0,
    size_over_life=sizes((0.0, 0.15, 0.3), (1.0, 0.05, 0.1)), color_gradient=alpha((0.0, 1.0), (0.6, 0.8), (1.0, 0.0))), 2)
# 16 gold motes thrown about 1 m out from the knight's chest, then rising at 1 m/s (acceleration / drag).
define("Knight", "AegisSummon", shot(**SPARKLE,
    max_particles=128, lifetime=[0.45, 0.6], color_intensity=6.0, start_speed=[4.0, 7.0], drag=6.0, acceleration=[0, 6.0, 0],
    size_over_life=sizes((0.0, 0.0, 0.0), (0.15, 0.15, 0.3), (1.0, 0.05, 0.1)),
    color_gradient=alpha((0.0, 0.0), (0.15, 1.0), (0.6, 0.8), (1.0, 0.0))), 2)
define("Knight", "AegisFade", shot(**SPARKLE,
    max_particles=128, lifetime=[0.3, 0.3], color_intensity=5.0, start_speed=[1.5, 3.0], drag=7.0,
    size_over_life=sizes((0.0, 0.18, 0.24), (1.0, 0.0, 0.0)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
define("Knight", "AegisBlockSpark", shot(**SPARK,
    max_particles=256, lifetime=[0.2, 0.3], color_intensity=6.0, size_axes=[1, 3, 1], start_speed=[4.0, 8.0], drag=3.0,
    size_over_life=sizes((0.0, 0.1, 0.2), (1.0, 0.04, 0.08)), color_gradient=alpha((0.0, 1.0), (0.6, 0.85), (1.0, 0.0))), 2)
# EmitShape(size = blast diameter, 0.15 s) at 0.6 m: the glow's quad opens from the blast circle to 1.5 x it.
define("Knight", "VerdictImpact", shot(**GROUND_GLOW,
    max_particles=64, **SCRIPTED, color_intensity=4.5,
    size_over_life=sizes((0.0, 1.0), (0.3, 1.35), (1.0, 1.5)), color_gradient=alpha((0.0, 1.0), (0.4, 0.7), (1.0, 0.0))), 3)
# EmitShape(size = blast diameter, 2 s): gold cracks glow (normal blend above 1 blooms) and cool; gone over the last 60%.
define("Knight", "VerdictCrack", shot(
    max_particles=64, **SCRIPTED, texture=f"{W}/crack_radial.png", render_mode="horizontal_billboard",
    blend_mode="normal", start_rotation=[0, 360], color_intensity=2.2, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    intensity_over_life=scalar((0.0, 1.0), (0.12, 0.75), (1.0, 0.4)),
    color_gradient=keys((0.0, WHITE + [1.0]), (0.4, WHITE + [1.0]), (1.0, [0.75, 0.75, 0.75, 0.0]))), -10)
define("Knight", "DustRing", shot(**SMOKE, **GROUND_RING,
    max_particles=256, lifetime=[0.6, 0.9], lighting="volumetric", soft_fade_distance=0.3, start_speed=[4.0, 7.0],
    drag=3.5, acceleration=[0, 0.8, 0], size_over_life=sizes((0.0, 0.5, 0.7), (1.0, 1.2, 1.6)),
    color_gradient=alpha((0.0, 0.0), (0.12, 0.6), (1.0, 0.0))), 1)
define("Knight", "VerdictShockwave", shot(**SHOCK,
    max_particles=64, **SCRIPTED, color_intensity=5.0, size_over_life=grow(),
    color_gradient=alpha((0.0, 1.0), (0.6, 0.7), (1.0, 0.0))), -9)
# Motes along the embedded blade (1.1 m up, 3 m tall): a box shape launches them mostly up and down the blade.
define("Knight", "VerdictDissolve", shot(**SPARKLE,
    max_particles=128, lifetime=[0.6, 0.8], color_intensity=6.0, shape="box", shape_scale=[0.12, 1.0, 0.12],
    start_speed=[2.5, 6.0], drag=5.0, acceleration=[0, 4.0, 0],
    size_over_life=sizes((0.0, 0.0, 0.0), (0.1, 0.15, 0.25), (1.0, 0.04, 0.08)),
    color_gradient=alpha((0.0, 0.0), (0.1, 1.0), (1.0, 0.0))), 2)
# The kit tints it WHITE, so this gradient carries the dust colour.
define("Knight", "RushDust", shot(**SMOKE, **UP_BIAS,
    max_particles=128, lifetime=[0.45, 0.55], lighting="volumetric", soft_fade_distance=0.3, start_speed=[1.5, 3.0],
    drag=4.0, acceleration=[0, 0.6, 0], size_over_life=sizes((0.0, 0.45, 0.55), (1.0, 1.0, 1.3)),
    color_gradient=alpha((0.0, 0.0), (0.15, 0.5), (1.0, 0.0), rgb=DUSTY)), 1)
define("Knight", "RushGlow", shot(**GLOW,
    max_particles=128, lifetime=[0.22, 0.28], color_intensity=5.0, start_speed=[2.0, 4.5], drag=7.0,
    size_over_life=sizes((0.0, 0.25, 0.4), (1.0, 0.08, 0.12)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
# Emit(count 1, GOLD) without a size: absolute metres, 1 -> 3.5 m at chest height.
define("Knight", "RushImpact", shot(**RING,
    max_particles=64, lifetime=[0.3, 0.3], color_intensity=5.0, size_over_life=punch((1.0, 1.0), (3.5, 3.5), 0.3, 0.8),
    color_gradient=alpha((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 2)
define("Knight", "BulwarkShockwave", shot(**SHOCK,
    max_particles=64, **SCRIPTED, color_intensity=4.5, size_over_life=grow(0.4),
    color_gradient=alpha((0.0, 1.0), (0.6, 0.6), (1.0, 0.0))), -9)

# ================================================================================================ Barbarian
# EmitArc like OathbladeSlash: size 0.55 x length (~1.8 m), lifetime 0.1-0.18 s; tinted red / fury crimson.
define("Barbarian", "RavagerSmear", shot(
    max_particles=256, **SCRIPTED, texture=f"{P}/soft.png", render_mode="horizontal_billboard", blend_mode="additive",
    primitive="octagon", color_intensity=2.2, size_over_life=sizes((0.0, 1.0), (0.4, 0.85), (1.0, 0.7)),
    color_gradient=alpha((0.0, 0.0), (0.06, 1.0), (0.5, 0.75), (1.0, 0.0))), 2)
define("Barbarian", "RavagerDebris", shot(**ROCKS, **UP_BIAS,
    max_particles=256, lifetime=[0.6, 0.8], gravity_scale=1.2, start_speed=[3.0, 5.0],
    size_over_life=sizes((0.0, 0.15, 0.3), (1.0, 0.12, 0.25)), color_gradient=alpha((0.0, 1.0), (0.75, 1.0), (1.0, 0.0))), 2)
# Skullsplitter's fissure: two magma droplets per metre jump 1 m high and fall back; they die on the ground.
define("Barbarian", "FissureSpike", shot(
    max_particles=512, lifetime=[0.45, 0.55], texture=f"{P}/ember.png", render_mode="billboard", align_to_direction=True,
    size_axes=[1, 2.2, 1], blend_mode="additive", color_intensity=7.0, shape="cone", cone_angle=25.0, direction="up",
    start_speed=[4.0, 6.0], gravity_scale=1.5, collision="kill", collision_planes=GROUND,
    size_over_life=sizes((0.0, 0.3, 0.5), (1.0, 0.15, 0.25)), intensity_over_life=scalar((0.0, 1.3), (1.0, 0.6)),
    color_gradient=alpha((0.0, 1.0), (0.7, 0.9), (1.0, 0.0))), 2)
define("Barbarian", "DustPuff", shot(**SMOKE,
    max_particles=512, lifetime=[0.55, 0.65], lighting="volumetric", soft_fade_distance=0.3, shape_position=[0.0, 0.3, 0.0],
    start_speed=[1.5, 3.0], drag=6.0, acceleration=[0, 3.0, 0], size_over_life=sizes((0.0, 0.35, 0.45), (1.0, 0.9, 1.1)),
    color_gradient=alpha((0.0, 0.0), (0.12, 0.55), (1.0, 0.0))), 1)
# Emit(count 1, DUST) without a size: a dust ring 0.5 -> 4 m flat on the ground.
define("Barbarian", "FaultStomp", shot(
    max_particles=64, lifetime=[0.35, 0.35], texture=f"{V}/dust_ring.png", render_mode="horizontal_billboard",
    blend_mode="normal", lighting="volumetric", start_rotation=[0, 360], size_over_life=punch((0.5, 0.5), (4.0, 4.0), 0.3, 0.8),
    color_gradient=alpha((0.0, 0.85), (0.5, 0.5), (1.0, 0.0))), -9)
# Rock spikes standing in the fissure: vertical billboards centred on the ground (the sheet's spikes stand on the frame
# centre, the lower half is buried), nudged 0.2-0.3 m apart, up in 0.06 s and sinking back at the end.
define("Barbarian", "FaultSpike", shot(**HORIZONTAL_RING,
    max_particles=512, lifetime=[0.5, 0.6], texture=f"{W}/rock_spike_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="vertical_billboard", blend_mode="normal", lighting="volumetric",
    start_speed=[2.5, 4.0], drag=12.0, start_rotation=[-15, 15],
    size_over_life=sizes((0.0, 0.0, 0.0), (0.12, 1.3, 1.9), (0.7, 1.3, 1.9), (1.0, 0.2, 0.3)),
    color_gradient=alpha((0.0, 1.0), (0.8, 1.0), (1.0, 0.0))), 0)
# Tectonic Rift lava (GroundZones, every 0.1 s at a random point): glowing crack patches lifted 5 cm off the ground.
define("Barbarian", "LavaCrack", shot(
    max_particles=256, lifetime=[0.6, 0.9], texture=f"{V}/scorch_embers.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=4.0, direction="up", start_speed=[0.5, 0.5], drag=10.0, start_rotation=[0, 360],
    size_over_life=sizes((0.0, 0.5, 0.7), (0.25, 0.7, 1.0), (1.0, 0.6, 0.9)),
    intensity_over_life=scalar((0.0, 0.4), (0.2, 1.3), (1.0, 0.5)),
    color_gradient=alpha((0.0, 0.0), (0.15, 1.0), (0.7, 0.8), (1.0, 0.0))), -10)
# One particle per blade per frame (900 deg/s): discs big enough to overlap at 60 fps draw a flat ring trail.
define("Barbarian", "WhirlwindTrail", shot(
    max_particles=128, lifetime=[0.18, 0.18], texture=f"{P}/soft.png", render_mode="horizontal_billboard",
    blend_mode="additive", primitive="octagon", color_intensity=2.2, size_over_life=sizes((0.0, 0.9), (1.0, 0.35)),
    color_gradient=alpha((0.0, 0.9), (1.0, 0.0))), 2)
define("Barbarian", "WhirlwindStop", shot(**SPARK, **HORIZONTAL_RING,
    max_particles=128, lifetime=[0.25, 0.35], color_intensity=6.0, size_axes=[1, 3, 1], start_speed=[5.0, 8.0], drag=3.0,
    size_over_life=sizes((0.0, 0.15, 0.3), (1.0, 0.05, 0.1)), color_gradient=alpha((0.0, 1.0), (0.6, 0.8), (1.0, 0.0))), 2)
define("Barbarian", "MaelstromSwirl", shot(**dict(SMOKE, spin_speed=[120, 240]),
    max_particles=64, lifetime=[0.6, 0.6], lighting="volumetric", soft_fade_distance=0.3, start_speed=[1.0, 2.0],
    drag=1.5, turbulence_strength=0.4, turbulence_frequency=0.8, turbulence_speed=1.0,
    size_over_life=sizes((0.0, 0.5, 0.7), (1.0, 1.0, 1.4)), color_gradient=alpha((0.0, 0.0), (0.2, 0.5), (1.0, 0.0))), 1)
define("Barbarian", "LeapDust", shot(**SMOKE, **GROUND_RING,
    max_particles=64, lifetime=[0.55, 0.65], lighting="volumetric", soft_fade_distance=0.3, start_speed=[3.0, 5.0],
    drag=3.0, acceleration=[0, 0.6, 0], size_over_life=sizes((0.0, 0.5, 0.7), (1.0, 1.4, 1.8)),
    color_gradient=alpha((0.0, 0.0), (0.1, 0.6), (1.0, 0.0))), 1)
define("Barbarian", "LeapShockwave", shot(
    max_particles=64, **SCRIPTED, texture=f"{V}/dust_ring.png", render_mode="horizontal_billboard", blend_mode="normal",
    lighting="volumetric", start_rotation=[0, 360], size_over_life=grow(0.4),
    color_gradient=alpha((0.0, 0.95), (0.5, 0.6), (1.0, 0.0))), -9)
# The landing crack: the same decal as Verdict, darkened (the kit tints it with light dust).
define("Barbarian", "LeapCrack", shot(
    max_particles=64, **SCRIPTED, texture=f"{W}/crack_radial.png", render_mode="horizontal_billboard",
    blend_mode="normal", start_rotation=[0, 360], size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=alpha((0.0, 1.0), (0.4, 1.0), (1.0, 0.0), rgb=[0.32, 0.32, 0.32])), -10)
define("Barbarian", "LeapRocks", shot(**ROCKS, shape_position=[0.0, 0.7, 0.0],
    max_particles=128, lifetime=[0.8, 1.0], gravity_scale=1.5, start_speed=[4.0, 8.0],
    size_over_life=sizes((0.0, 0.2, 0.45), (1.0, 0.18, 0.4)), color_gradient=alpha((0.0, 1.0), (0.8, 1.0), (1.0, 0.0))), 2)
define("Barbarian", "FuryEmbers", shot(
    max_particles=64, lifetime=[0.45, 0.55], texture=f"{P}/ember.png", render_mode="billboard", blend_mode="additive",
    color_intensity=8.0, direction="up", direction_spread=30.0, start_speed=[0.6, 1.0], turbulence_strength=0.15,
    turbulence_frequency=2.0, turbulence_speed=1.5, size_over_life=sizes((0.0, 0.12, 0.2), (1.0, 0.03, 0.06)),
    intensity_over_life=scalar((0.0, 1.4), (1.0, 0.6)), color_gradient=alpha((0.0, 0.0), (0.1, 1.0), (1.0, 0.0))), 2)

# ================================================================================================ Mage
# EmitShape(size = blast diameter, 0.1 s): starts at 0, so the diameter is the glow's peak.
define("Mage", "EmberOrbFlash", shot(**GLOW,
    max_particles=64, **SCRIPTED, color_intensity=4.5, size_over_life=sizes((0.0, 0.0), (0.03, 0.5), (0.4, 0.95), (1.0, 1.0)),
    color_gradient=alpha((0.0, 1.0), (0.5, 0.6), (1.0, 0.0))), 3)
define("Mage", "EmberOrbBurst", shot(
    max_particles=512, lifetime=[0.4, 0.7], texture=f"{P}/ember.png", render_mode="billboard", align_to_direction=True,
    size_axes=[1, 2.5, 1], blend_mode="additive", color_intensity=8.0, start_speed=[3.0, 6.0], gravity_scale=0.4,
    drag=1.5, size_over_life=sizes((0.0, 0.12, 0.25), (1.0, 0.03, 0.06)),
    color_gradient=keys((0.0, WHITE + [1.0]), (0.6, [0.9, 0.9, 0.9, 0.9]), (1.0, [0.7, 0.7, 0.7, 0.0]))), 2)
define("Mage", "EmberFizzle", shot(**SMOKE,
    max_particles=128, lifetime=[0.35, 0.45], direction="up", direction_spread=40.0, start_speed=[0.3, 0.8],
    size_over_life=sizes((0.0, 0.28, 0.32), (1.0, 0.55, 0.65)),
    color_gradient=keys((0.0, SMOKE_GREY + [0.0]), (0.15, SMOKE_GREY + [0.55]), (1.0, [0.45, 0.45, 0.45, 0.0]))), 1)
define("Mage", "EmberSplash", shot(
    max_particles=256, lifetime=[0.3, 0.4], texture=f"{P}/ember.png", render_mode="billboard", align_to_direction=True,
    size_axes=[1, 2, 1], blend_mode="additive", color_intensity=8.0, shape="cone", cone_angle=35.0, direction="up",
    start_speed=[1.0, 3.0], gravity_scale=0.8, size_over_life=sizes((0.0, 0.15, 0.3), (1.0, 0.04, 0.08)),
    color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
# EmitShape(size = diameter, lifetime = fall): grows from nothing to the diameter while darkening; drawn under the
# telegraph fill (-13) so the warning stays readable.
define("Mage", "StarfallShadow", shot(
    max_particles=64, **SCRIPTED, texture=f"{P}/soft.png", render_mode="horizontal_billboard", blend_mode="normal",
    primitive="octagon", size_over_life=sizes((0.0, 0.0), (0.03, 0.2), (1.0, 1.0)),
    color_gradient=alpha((0.0, 0.0), (0.05, 0.2), (0.95, 0.6), (1.0, 0.0))), -14)
define("Mage", "StarfallImpact", shot(**GROUND_GLOW,
    max_particles=64, **SCRIPTED, color_intensity=5.0,
    size_over_life=sizes((0.0, 1.0), (0.3, 1.6), (1.0, 1.9)), color_gradient=alpha((0.0, 1.0), (0.4, 0.7), (1.0, 0.0))), 3)
# Rock tint from Starfall, gold from Godspear (also emits it).
define("Mage", "StarfallDebris", shot(**ROCKS, shape_position=[0.0, 0.9, 0.0],
    max_particles=512, lifetime=[0.7, 0.9], gravity_scale=1.5, start_speed=[5.0, 9.0],
    size_over_life=sizes((0.0, 0.15, 0.35), (1.0, 0.12, 0.3)), color_gradient=alpha((0.0, 1.0), (0.8, 1.0), (1.0, 0.0))), 2)
define("Mage", "ScorchDecal", shot(
    max_particles=64, **SCRIPTED, texture=f"{P}/scorch.png", render_mode="horizontal_billboard", blend_mode="normal",
    start_rotation=[0, 360], size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=alpha((0.0, 0.8), (0.5, 0.8), (1.0, 0.0))), -11)
# Crater zone (every 0.15 s, 1 or 3 at random points on the ground): a flame flipbook played once, tinted fire or echo
# violet. The sheet's flames stand on the frame's bottom edge, so a quick 0.35 m lift (speed / drag) sets them on the
# ground instead of burying their lower half; then they rise at 0.75 m/s.
define("Mage", "CraterFire", shot(**FLAMES,
    max_particles=256, lifetime=[0.5, 0.8], texture_sheet_cycles=1.0, color_intensity=4.0, direction="up",
    start_speed=[2.6, 3.2], drag=8.0, acceleration=[0, 6.0, 0], start_rotation=[-10, 10], soft_fade_distance=0.2,
    size_over_life=sizes((0.0, 0.6, 0.8), (0.6, 0.55, 0.7), (1.0, 0.25, 0.35)),
    color_gradient=alpha((0.0, 0.0), (0.1, 1.0), (0.7, 0.8), (1.0, 0.0))), 0)
# EmitShape(size = 2 x radius, lifetime = expansion + 0.05 s): linear growth that reaches the radius when the last damage
# band does (0.4 / 0.45 of the life), then holds while fading.
define("Mage", "RimeNovaRing", shot(**SHOCK,
    max_particles=64, **SCRIPTED, color_intensity=7.0, size_over_life=sizes((0.0, 0.0), (0.889, 1.0), (1.0, 1.0)),
    color_gradient=alpha((0.0, 1.0), (0.7, 1.0), (1.0, 0.0))), -9)
define("Mage", "FrostMist", shot(**SMOKE, **GROUND_RING,
    max_particles=128, lifetime=[0.7, 0.9], soft_fade_distance=0.4, start_speed=[3.0, 6.0], drag=3.0,
    acceleration=[0, 0.3, 0], size_over_life=sizes((0.0, 0.6, 0.8), (1.0, 1.3, 1.6)),
    color_gradient=alpha((0.0, 0.0), (0.12, 0.5), (1.0, 0.0), rgb=[0.9, 0.9, 0.9])), 1)
define("Mage", "FrostShatter", shot(
    max_particles=256, lifetime=[0.45, 0.55], texture=f"{V}/crystal_shard_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="billboard", blend_mode="additive", color_intensity=3.0,
    shape_position=[0.0, 0.3, 0.0], start_speed=[3.0, 6.0], gravity_scale=1.0, start_rotation=[0, 360],
    spin_speed=[360, 720], random_spin_direction=True, size_over_life=sizes((0.0, 0.2, 0.35), (1.0, 0.12, 0.2)),
    color_gradient=alpha((0.0, 1.0), (0.7, 0.9), (1.0, 0.0))), 2)
# Implosion from one point: a fast, invisible launch to ~1 m, then speed_over_life turns negative and the motes fall
# back into the centre at ~3.3 m/s (net displacement ~0 at death).
define("Mage", "BlinkOut", shot(**SPARKLE,
    max_particles=64, lifetime=[0.35, 0.35], color_intensity=6.0, start_speed=[18.0, 22.0],
    speed_over_life=scalar((0.0, 1.0), (0.1, 1.0), (0.2, -0.166), (1.0, -0.166)),
    size_over_life=sizes((0.0, 0.2, 0.4), (0.7, 0.2, 0.35), (1.0, 0.05, 0.1)),
    color_gradient=alpha((0.0, 0.0), (0.18, 0.0), (0.26, 1.0), (0.85, 0.9), (1.0, 0.0))), 2)
define("Mage", "BlinkIn", shot(**SPARKLE,
    max_particles=64, lifetime=[0.3, 0.4], color_intensity=6.0, start_speed=[2.0, 4.0], drag=3.0,
    size_over_life=sizes((0.0, 0.2, 0.35), (1.0, 0.03, 0.06)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
# Emit(size 6 m, 0.5 s): a vortex that spins at 360 deg/s and tightens to 0.6 of its size before the burst.
define("Mage", "BlinkRift", shot(
    max_particles=64, **SCRIPTED, texture=f"{W}/vortex.png", render_mode="horizontal_billboard", blend_mode="additive",
    primitive="octagon", color_intensity=4.0, start_rotation=[0, 360], spin_speed=[360, 360],
    size_over_life=sizes((0.0, 1.0), (1.0, 0.6)), color_gradient=alpha((0.0, 0.0), (0.15, 1.0), (0.75, 0.9), (1.0, 0.0))), -9)
define("Mage", "BlinkRiftBurst", shot(**SHOCK,
    max_particles=64, **SCRIPTED, color_intensity=6.0, size_over_life=grow(0.4),
    color_gradient=alpha((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 2)

# ================================================================================================ Rogue
define("Rogue", "CrossbowMuzzle", shot(**SPARK,
    max_particles=128, lifetime=[0.12, 0.18], color_intensity=6.0, size_axes=[1, 3, 1], start_speed=[2.0, 4.0], drag=4.0,
    size_over_life=sizes((0.0, 0.08, 0.15), (1.0, 0.03, 0.05)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
define("Rogue", "KnifeGlint", shot(**SPARKLE,
    max_particles=128, lifetime=[0.18, 0.22], color_intensity=4.0, start_speed=[3.0, 5.0], drag=8.0,
    size_over_life=sizes((0.0, 0.0, 0.0), (0.3, 0.2, 0.35), (1.0, 0.0, 0.0)),
    color_gradient=alpha((0.0, 1.0), (0.7, 1.0), (1.0, 0.0))), 3)
# Mark red comes from the script (the Rogue's own mark colour); motes swirl up over the head.
define("Rogue", "MarkApply", shot(**SPARKLE,
    max_particles=128, lifetime=[0.35, 0.45], color_intensity=5.0, start_speed=[2.0, 3.0], drag=6.0,
    acceleration=[0, 5.0, 0], turbulence_strength=0.25, turbulence_frequency=2.5, turbulence_speed=2.0,
    size_over_life=sizes((0.0, 0.0, 0.0), (0.15, 0.15, 0.25), (1.0, 0.05, 0.08)),
    color_gradient=alpha((0.0, 0.0), (0.15, 1.0), (1.0, 0.0))), 2)
define("Rogue", "ShadowClone", shot(**SMOKE,
    max_particles=128, lifetime=[0.3, 0.4], soft_fade_distance=0.3, shape="box", shape_scale=[0.2, 0.8, 0.2],
    start_speed=[2.0, 4.0], drag=8.0, acceleration=[0, 2.0, 0], size_over_life=sizes((0.0, 0.45, 0.55), (1.0, 0.8, 1.0)),
    color_gradient=keys((0.0, [0.75, 0.75, 0.75, 0.0]), (0.15, [0.75, 0.75, 0.75, 0.8]), (1.0, [0.5, 0.5, 0.5, 0.0]))), 1)
define("Rogue", "ShadowStab", shot(**SPARK,
    max_particles=128, lifetime=[0.12, 0.18], color_intensity=7.0, size_axes=[1, 3.5, 1], start_speed=[4.0, 6.0], drag=3.0,
    size_over_life=sizes((0.0, 0.1, 0.2), (1.0, 0.04, 0.06)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
define("Rogue", "MarkBurst", shot(**SMOKE, **HORIZONTAL_RING,
    max_particles=128, lifetime=[0.45, 0.55], soft_fade_distance=0.3, start_speed=[3.0, 5.0], drag=4.0,
    acceleration=[0, 0.8, 0], size_over_life=sizes((0.0, 0.4, 0.5), (1.0, 0.8, 1.0)),
    color_gradient=keys((0.0, [0.8, 0.8, 0.8, 0.0]), (0.1, [0.8, 0.8, 0.8, 0.75]), (1.0, [0.6, 0.6, 0.6, 0.0]))), 1)
# Emit(count 1, MARK_RED) without a size: absolute 1.9 -> 2.8 m crossed slash at body height.
define("Rogue", "ExecuteSlash", shot(
    max_particles=64, lifetime=[0.25, 0.25], texture=f"{W}/slash_x.png", render_mode="billboard", blend_mode="additive",
    color_intensity=5.0, start_rotation=[-12, 12], size_over_life=sizes((0.0, 1.9), (0.15, 2.6), (1.0, 2.8)),
    color_gradient=alpha((0.0, 1.0), (0.3, 1.0), (1.0, 0.0))), 3)
define("Rogue", "SmokePoof", shot(**SMOKE,
    max_particles=64, lifetime=[0.8, 1.0], lighting="volumetric", soft_fade_distance=0.5, half_resolution=True,
    start_speed=[1.0, 3.0], drag=2.0, acceleration=[0, 0.4, 0], size_over_life=sizes((0.0, 0.7, 0.9), (1.0, 1.8, 2.2)),
    color_gradient=alpha((0.0, 0.0), (0.1, 0.7), (0.6, 0.5), (1.0, 0.0))), 1)
# Zone puffs (2 per 0.12 s at random points of the 3 m cloud, on the ground): rise half a metre and swirl.
define("Rogue", "SmokeCloud", shot(**SMOKE,
    max_particles=64, lifetime=[1.1, 1.3], lighting="volumetric", soft_fade_distance=0.6, half_resolution=True,
    direction="up", direction_spread=60.0, start_speed=[0.4, 0.8], drag=1.2, turbulence_strength=0.3,
    turbulence_frequency=0.6, turbulence_speed=0.4, size_over_life=sizes((0.0, 1.4, 1.6), (1.0, 2.3, 2.7)),
    color_gradient=alpha((0.0, 0.0), (0.25, 0.5), (0.7, 0.45), (1.0, 0.0))), 1)
define("Rogue", "MomentumWisps", shot(**GLOW,
    max_particles=64, lifetime=[0.45, 0.55], color_intensity=5.0, direction="up", direction_spread=25.0,
    start_speed=[0.2, 0.4], size_over_life=sizes((0.0, 0.22, 0.28), (1.0, 0.05, 0.08)),
    color_gradient=alpha((0.0, 0.0), (0.15, 0.9), (1.0, 0.0))), 2)

# ================================================================================================ shared weapons
define("Weapons", "SpiritDartPop", shot(**SPARKLE,
    max_particles=256, lifetime=[0.22, 0.28], color_intensity=5.0, start_speed=[1.0, 2.0], drag=3.0,
    size_over_life=sizes((0.0, 0.15, 0.25), (1.0, 0.03, 0.05)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
define("Weapons", "DiscFade", shot(**SPARKLE,
    max_particles=64, lifetime=[0.3, 0.3], color_intensity=5.0, start_speed=[1.5, 2.5], drag=6.0,
    size_over_life=sizes((0.0, 0.18, 0.22), (1.0, 0.02, 0.04)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
# EmitShape(size = aura diameter, 0.35 s) every 0.12 s: about three overlap, so 2.0 per ring adds up to the ~2.5 the
# design asks for (intensity 7 x opacity 0.35); the thorn ring sits at the quad's rim (r 0.93).
define("Weapons", "BrambleAura", shot(
    max_particles=64, **SCRIPTED, texture=f"{W}/thorn_ring.png", render_mode="horizontal_billboard", blend_mode="additive",
    primitive="octagon", color_intensity=2.0, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=alpha((0.0, 0.0), (0.3, 0.6), (0.7, 0.6), (1.0, 0.0))), -10)
define("Weapons", "BrambleThorns", shot(
    max_particles=64, lifetime=[0.45, 0.55], texture=f"{P}/shard.png", render_mode="billboard", blend_mode="normal",
    lighting="volumetric", direction="up", direction_spread=35.0, start_speed=[1.0, 2.0], gravity_scale=0.6,
    start_rotation=[0, 360], spin_speed=[180, 540], random_spin_direction=True,
    size_over_life=sizes((0.0, 0.15, 0.3), (1.0, 0.12, 0.25)), color_gradient=alpha((0.0, 1.0), (0.7, 1.0), (1.0, 0.0))), 2)
define("Weapons", "GodspearBlast", shot(**SHOCK,
    max_particles=64, **SCRIPTED, color_intensity=5.0, size_over_life=grow(),
    color_gradient=alpha((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 2)
define("Weapons", "FlaskShatter", shot(
    max_particles=256, lifetime=[0.35, 0.45], texture=f"{P}/shard.png", render_mode="billboard", blend_mode="additive",
    color_intensity=4.0, shape_position=[0.0, 0.5, 0.0], start_speed=[3.0, 5.0], gravity_scale=1.0,
    start_rotation=[0, 360], spin_speed=[540, 1080], random_spin_direction=True,
    size_over_life=sizes((0.0, 0.1, 0.25), (1.0, 0.06, 0.15)), color_gradient=alpha((0.0, 1.0), (0.7, 0.9), (1.0, 0.0))), 2)
# Pool zone (2 per 0.08 s at random points of each pool): flame flipbook played once per flame, lifted onto the ground
# like CraterFire (0.3 m), rising at 0.7 m/s.
define("Weapons", "FlaskFlames", shot(**FLAMES,
    max_particles=512, lifetime=[0.4, 0.6], texture_sheet_cycles=1.0, color_intensity=4.0, direction="up",
    start_speed=[2.2, 2.8], drag=7.0, acceleration=[0, 5.0, 0], start_rotation=[-10, 10], soft_fade_distance=0.2,
    size_over_life=sizes((0.0, 0.55, 0.7), (1.0, 0.15, 0.22)),
    color_gradient=alpha((0.0, 0.0), (0.1, 1.0), (0.7, 0.85), (1.0, 0.0))), 0)
# EmitShape(size = pool diameter, lifetime = pool duration): the embers fill 0.6 of the quad, so it spreads to 1.6 x the
# diameter in the first 4% (the glow then ends at the pool's edge), flickers, fades over the last 20%.
define("Weapons", "FlaskPoolDecal", shot(
    max_particles=64, **SCRIPTED, texture=f"{V}/scorch_embers.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=1.6, start_rotation=[0, 360], size_over_life=sizes((0.0, 1.0), (0.04, 1.6), (1.0, 1.6)),
    intensity_over_life=scalar((0.0, 1.0), (0.2, 0.8), (0.4, 1.05), (0.6, 0.85), (0.8, 1.0), (1.0, 0.9)),
    color_gradient=alpha((0.0, 0.0), (0.03, 0.8), (0.8, 0.7), (1.0, 0.0))), -10)
define("Weapons", "TotemPlace", shot(**SPARK,
    shape="hemisphere", max_particles=64, lifetime=[0.25, 0.35], color_intensity=8.0, size_axes=[1, 3, 1],
    start_speed=[2.0, 5.0], drag=2.0, gravity_scale=0.5, size_over_life=sizes((0.0, 0.1, 0.2), (1.0, 0.04, 0.06)),
    color_gradient=alpha((0.0, 1.0), (0.6, 0.85), (1.0, 0.0))), 2)
# EmitShape(size 0.55, 0.09 s) every 0.7 m along a jittered line: each dot swells to ~2.2 x its birth size right
# away so neighbours overlap into one jagged bolt, and flickers.
define("Weapons", "LightningBolt", shot(**GLOW,
    max_particles=1024, **SCRIPTED, color_intensity=6.0,
    size_over_life=sizes((0.0, 1.0), (0.12, 2.2), (0.5, 1.9), (1.0, 1.0)),
    intensity_over_life=scalar((0.0, 1.4), (0.25, 0.7), (0.5, 1.2), (0.75, 0.6), (1.0, 0.8)),
    color_gradient=alpha((0.0, 1.0), (0.6, 0.9), (1.0, 0.0))), 3)
define("Weapons", "ZapImpact", shot(**SPARK,
    max_particles=256, lifetime=[0.15, 0.25], color_intensity=8.0, size_axes=[1, 3, 1], start_speed=[3.0, 5.0], drag=3.0,
    size_over_life=sizes((0.0, 0.08, 0.15), (1.0, 0.03, 0.05)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
define("Weapons", "WispSplash", shot(**SPARKLE,
    max_particles=128, lifetime=[0.35, 0.45], color_intensity=5.0, start_speed=[1.0, 3.0], drag=3.0,
    size_over_life=sizes((0.0, 0.2, 0.4), (1.0, 0.04, 0.08)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 2)
# Untinted (Loadout emits 30 at the hero): a gold column of sparkles.
define("Weapons", "EvolveBurst", shot(**SPARKLE,
    max_particles=64, lifetime=[1.0, 1.4], color_intensity=6.0, shape="cone", cone_angle=25.0, direction="up",
    start_speed=[2.5, 5.5], drag=1.2, size_over_life=sizes((0.0, 0.0, 0.0), (0.1, 0.3, 0.6), (1.0, 0.05, 0.1)),
    color_gradient=keys((0.0, EVOLVE_GOLD + [0.0]), (0.08, [1.0, 0.95, 0.75, 1.0]), (0.5, EVOLVE_GOLD + [0.9]),
                        (1.0, EVOLVE_GOLD + [0.0]))), 3)

# ================================================================================================ projectile trails
# ProjectileWorld emits one untinted particle at the projectile every trailInterval: the gradients carry the colour.
define("Trails", "CrescentSparks", shot(**SPARKLE,
    max_particles=128, lifetime=[0.28, 0.32], color_intensity=5.0, start_speed=[0.3, 0.8], acceleration=[0, 1.0, 0],
    size_over_life=sizes((0.0, 0.3, 0.5), (1.0, 0.05, 0.1)),
    color_gradient=keys((0.0, CRESCENT_GOLD + [1.0]), (1.0, [1.0, 0.7, 0.25, 0.0]))), 2)
define("Trails", "ReflectTrail", shot(**GLOW,
    max_particles=128, lifetime=[0.2, 0.2], color_intensity=5.0, size_over_life=sizes((0.0, 0.2), (1.0, 0.06)),
    color_gradient=alpha((0.0, 0.9), (1.0, 0.0), rgb=GOLD)), 2)
define("Trails", "EmberOrbTrail", shot(**FLAMES,
    max_particles=256, lifetime=[0.16, 0.2], texture_sheet_randomize=True, color_intensity=2.2, start_rotation=[0, 360],
    direction="up", start_speed=[0.3, 0.6], size_over_life=sizes((0.0, 0.35, 0.4), (1.0, 0.0, 0.05)),
    color_gradient=keys((0.0, [1.0, 0.75, 0.35, 1.0]), (0.5, [1.0, 0.45, 0.12, 0.8]), (1.0, [1.0, 0.25, 0.06, 0.0]))), 0)
define("Trails", "SunfireOrbTrail", shot(**FLAMES,
    max_particles=256, lifetime=[0.14, 0.18], texture_sheet_randomize=True, color_intensity=2.2, start_rotation=[0, 360],
    direction="up", start_speed=[0.3, 0.6], size_over_life=sizes((0.0, 0.45, 0.55), (1.0, 0.0, 0.05)),
    color_gradient=keys((0.0, [1.0, 0.97, 0.85, 1.0]), (0.5, [1.0, 0.85, 0.5, 0.8]), (1.0, [1.0, 0.7, 0.3, 0.0]))), 0)
define("Trails", "EmberSparks", shot(
    max_particles=256, lifetime=[0.22, 0.28], texture=f"{P}/ember.png", render_mode="billboard", align_to_direction=True,
    size_axes=[1, 2, 1], blend_mode="additive", color_intensity=8.0, start_speed=[0.5, 1.5], gravity_scale=0.5,
    size_over_life=sizes((0.0, 0.07, 0.1), (1.0, 0.02, 0.03)),
    color_gradient=keys((0.0, [1.0, 0.8, 0.4, 1.0]), (1.0, [1.0, 0.35, 0.08, 0.0]))), 2)
# Fire that turns to smoke in one normal-blend emitter: an HDR-bright orange puff (intensity 6, blooms) cooling into
# grey smoke as intensity_over_life drops to 0.7.
define("Trails", "MeteorTrail", shot(**SMOKE,
    max_particles=256, lifetime=[0.35, 0.45], soft_fade_distance=0.3, start_speed=[0.2, 0.5],
    size_over_life=sizes((0.0, 0.8, 0.9), (1.0, 1.5, 1.7)), intensity_over_life=scalar((0.0, 6.0), (0.25, 2.5), (0.5, 0.9), (1.0, 0.7)),
    color_gradient=keys((0.0, [1.0, 0.62, 0.25, 0.95]), (0.3, [0.95, 0.45, 0.18, 0.8]), (0.55, [0.36, 0.33, 0.32, 0.6]),
                        (1.0, [0.3, 0.3, 0.3, 0.0]))), 1)
define("Trails", "BoltTracer", shot(**GLOW,
    max_particles=256, lifetime=[0.12, 0.12], color_intensity=5.0, size_over_life=sizes((0.0, 0.15), (1.0, 0.08)),
    color_gradient=alpha((0.0, 1.0), (1.0, 0.0), rgb=TEAL)), 2)
define("Trails", "ShadowDaggerTrail", shot(**SMOKE,
    max_particles=512, lifetime=[0.28, 0.32], soft_fade_distance=0.2, start_speed=[0.2, 0.5],
    size_over_life=sizes((0.0, 0.3, 0.35), (1.0, 0.55, 0.65)),
    color_gradient=keys((0.0, DAGGER_SHADOW + [0.0]), (0.15, DAGGER_SHADOW + [0.7]), (1.0, [0.2, 0.1, 0.3, 0.0]))), 1)
define("Trails", "SpiritDartTrail", shot(**GLOW,
    max_particles=256, lifetime=[0.25, 0.25], color_intensity=5.0, size_over_life=sizes((0.0, 0.2), (1.0, 0.0)),
    color_gradient=alpha((0.0, 0.9), (1.0, 0.0), rgb=PALE_BLUE)), 2)
define("Trails", "DiscTrail", shot(**GLOW,
    max_particles=64, lifetime=[0.2, 0.2], color_intensity=5.0, size_over_life=sizes((0.0, 0.3), (1.0, 0.1)),
    color_gradient=alpha((0.0, 0.9), (1.0, 0.0), rgb=DISC_CYAN)), 2)
define("Trails", "EclipseTrail", shot(**GLOW,
    max_particles=64, lifetime=[0.2, 0.2], color_intensity=6.0, size_over_life=sizes((0.0, 0.35), (1.0, 0.12)),
    color_gradient=keys((0.0, [1.0, 0.85, 0.5, 1.0]), (0.3, ECLIPSE_VIOLET + [0.9]), (1.0, ECLIPSE_VIOLET + [0.0]))), 2)
define("Trails", "JavelinTrail", shot(**SMOKE,
    max_particles=64, lifetime=[0.25, 0.25], start_speed=[0.1, 0.3], size_over_life=sizes((0.0, 0.25, 0.3), (1.0, 0.4, 0.45)),
    color_gradient=alpha((0.0, 0.0), (0.15, 0.25), (1.0, 0.0), rgb=WIND_DUST)), 1)
define("Trails", "GodspearTrail", shot(**SPARKLE,
    max_particles=64, lifetime=[0.28, 0.32], color_intensity=6.0, start_speed=[0.5, 1.5], drag=2.0,
    size_over_life=sizes((0.0, 0.25), (1.0, 0.05)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0), rgb=EVOLVE_GOLD)), 2)
define("Trails", "FlaskSmoke", shot(**SMOKE,
    max_particles=128, lifetime=[0.35, 0.45], lighting="volumetric", direction="up", start_speed=[0.2, 0.5],
    acceleration=[0, 0.3, 0], size_over_life=sizes((0.0, 0.3), (1.0, 0.6)),
    color_gradient=keys((0.0, SMOKE_GREY + [0.0]), (0.15, SMOKE_GREY + [0.5]), (1.0, [0.5, 0.5, 0.5, 0.0]))), 1)
define("Trails", "SpireSparks", shot(**SPARK,
    max_particles=64, lifetime=[0.18, 0.22], color_intensity=7.0, size_axes=[1, 2.5, 1], start_speed=[1.0, 3.0], drag=2.0,
    size_over_life=sizes((0.0, 0.1), (1.0, 0.03)), color_gradient=alpha((0.0, 1.0), (1.0, 0.0), rgb=DISC_CYAN)), 2)
define("Trails", "WispTrail", shot(**GLOW,
    max_particles=128, lifetime=[0.35, 0.35], color_intensity=4.0, direction="up", start_speed=[0.1, 0.2],
    size_over_life=sizes((0.0, 0.25), (1.0, 0.0)), color_gradient=alpha((0.0, 0.9), (1.0, 0.0), rgb=WISP_GREEN)), 2)
# Enemy trails (EnemyBolt, HexOrb): the enemy red-orange.
define("Trails", "EnemyBoltTrail", shot(**GLOW,
    max_particles=256, lifetime=[0.15, 0.15], color_intensity=4.0, size_over_life=sizes((0.0, 0.15), (1.0, 0.06)),
    color_gradient=alpha((0.0, 1.0), (1.0, 0.0), rgb=DANGER)), 2)
define("Trails", "HexOrbTrail", shot(**GLOW,
    max_particles=512, lifetime=[0.3, 0.3], color_intensity=4.0, size_over_life=sizes((0.0, 0.35), (1.0, 0.0)),
    color_gradient=keys((0.0, [1.0, 0.45, 0.2, 0.9]), (0.5, DANGER + [0.6]), (1.0, DANGER + [0.0]))), 2)


if __name__ == "__main__":
    REGISTRY.main("build_effects_weapons.py")
