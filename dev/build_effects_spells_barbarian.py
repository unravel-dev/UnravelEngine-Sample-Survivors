"""Barbarian spell effects: Ravager Chop / Skullsplitter, Whirlwind / Maelstrom, Fault Line / Tectonic Rift, Earthshaker
Leap and the full-Fury look. Data only, like build_effects_spells.py (read its notes first).

    python build_effects_spells_barbarian.py                       list the effects defined here (no editor call)
    python build_effects_spells_barbarian.py build [BarbarianFx]   create missing effects and (re)apply their settings

Palette: earthy dust, rock and dark earth carry their own colours (fired untinted, Color.white); arcs, flashes and
sparks are white and take the script's tint: warm steel, blood-red Fury and Skullsplitter crimson, lava orange for
Tectonic Rift, cold steel and pale lightning for Maelstrom.
Headed layers (axe arcs, fissure lines, ground bolts) are horizontal billboards aligned to the velocity the script
gives with EmitMoving (a tiny speed: only the heading counts); the texture's top points along it, and a positive spin
turns it from +Z toward -X (Ground.Rotate's negative sense).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import P  # noqa: E402
from build_effects_spells import K, W, A, GROUND, ADD, SPIN, FLAMES, SMOKE_LIT, light, fade  # noqa: E402
from effect_helpers import Registry, shot, sizes, scalar, keys, grow  # noqa: E402

GROUP = "BarbarianFx"
REGISTRY = Registry((GROUP,))


def define(name, props, sort):
    """Every Barbarian emitter lives in one group (the hero-opacity setting fades it)."""
    REGISTRY.define(GROUP, name, props, sort)


DUST = [0.56, 0.48, 0.39]
DUST_SETTLED = [0.48, 0.44, 0.4]
ROCK = [0.62, 0.54, 0.46]
RUBBLE = [0.46, 0.4, 0.34]
EARTH = [0.36, 0.3, 0.24]
CRACK = [0.08, 0.06, 0.05]
SHOCK_DUST = [1.0, 0.9, 0.74]
STORM_SLATE = [0.04, 0.045, 0.07]
GROOVE = [0.05, 0.035, 0.03]
DUST_WAVE = [0.72, 0.64, 0.52]
STORM_DUST = [0.3, 0.32, 0.4]
LAVA_SMOKE = [0.2, 0.17, 0.15]

UP_BIAS = [0.0, 0.7, 0.0]
HEADED = {"render_mode": "horizontal_billboard", "align_to_direction": True}
# slash_04: a 120 deg arc of a circle of radius 0.30 x the quad around uv (0.5, 0.66); that point is the pivot, so the
# script places the arc's centre (the hero) and sizes the quad as radius / 0.30.
ARC = {"texture": f"{K}/slash_04.png", "pivot": [0.5, 0.66], "primitive": "cutout"}
BOLTS = {"texture": f"{A}/bolts_4x1.png", "texture_sheet_tiles": [4, 1], "texture_sheet_randomize": True}
ROCK_TEX = {"texture": f"{W}/rock_chunk_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True,
            "render_mode": "camera_facing", "blend_mode": "normal", "lighting": "surface", "start_rotation": [0, 360],
            "random_spin_direction": True, "shape": "hemisphere", "direction": "outward", "collision": "bounce",
            "collision_planes": GROUND, "restitution": 0.3, "max_bounces": 2}
STREAK = {"texture": f"{P}/spark.png", "render_mode": "stretched"}
# Crack strokes: trace_06 is a smooth streak over the middle 40% of the quad, its opaque core 10% of the quad's width.
# FaultLine draws a crack as a zigzag of headed strokes sized 3.125 x their length (25% overlap at the joints), so a
# stroke's width is 0.31 x its length x size_axes.x: the axes below are set for the stroke lengths noted.
TRACE = {"texture": f"{K}/trace_06.png", "primitive": "cutout"}

# ------------------------------------------------------------------------------------------------ shared impacts
# One short flash per heavy hit (size unit = hit radius or cone length): lights the moor around the impact.
define("BarbarianFlash", shot(**ADD, **light(3, 20.0, 7.0),
    max_particles=128, lifetime=[0.13, 0.13], texture=f"{P}/glow.png", render_mode="camera_facing", primitive="octagon",
    color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.15, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.35, 0.65), (1.0, 0.0))), 12)
# The crisp ground shockwave: the size is its peak quad (the ring sits at 0.45 x it). Pale dust of its own, so it never
# reads as a red-orange telegraph; Maelstrom tints it cold.
define("BarbarianShockRing", shot(**ADD,
    max_particles=128, lifetime=[0.32, 0.32], texture=f"{W}/shock_ring.png", render_mode="horizontal_billboard",
    primitive="octagon", start_rotation=[0, 360], color_intensity=1.4, size_over_life=grow(),
    color_gradient=keys((0.0, SHOCK_DUST + [1.0]), (0.5, SHOCK_DUST + [0.6]), (1.0, SHOCK_DUST + [0.0]))), 3)
# The rolling dust donut that follows the shockwave out (lit, slower, lingers).
define("BarbarianDustRing", shot(
    max_particles=128, lifetime=[0.75, 0.75], texture=f"{K}/smoke_10.png", render_mode="horizontal_billboard",
    blend_mode="normal", lighting="volumetric", start_rotation=[0, 360], spin_speed=[10, 30], random_spin_direction=True,
    size_over_life=grow(0.4),
    color_gradient=keys((0.0, DUST + [0.0]), (0.06, DUST + [0.8]), (0.5, DUST + [0.5]), (1.0, DUST_SETTLED + [0.0]))), -8)
# Dust billows thrown up and out (birth size = the script's); also the whirl's swept dust and the leap's trail.
define("BarbarianDust", shot(**SMOKE_LIT, shape_position=UP_BIAS,
    max_particles=1024, lifetime=[0.8, 1.3], direction="outward", start_speed=[2.5, 6.0], drag=4.5,
    acceleration=[0.0, 0.5, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 2.0, 2.6)),
    color_gradient=keys((0.0, DUST + [0.0]), (0.1, DUST + [0.6]), (1.0, DUST_SETTLED + [0.0]))), 2)
define("BarbarianRocks", shot(**ROCK_TEX,
    max_particles=1024, lifetime=[0.9, 1.4], spin_speed=[300, 800], start_speed=[4.0, 8.5], gravity_scale=2.2,
    size_over_life=sizes((0.0, 0.12, 0.28), (1.0, 0.1, 0.25)),
    color_gradient=keys((0.0, ROCK + [1.0]), (0.85, ROCK + [1.0]), (1.0, ROCK + [0.0]))), 5)
define("BarbarianBoulders", shot(**ROCK_TEX,
    max_particles=256, lifetime=[1.1, 1.6], spin_speed=[150, 450], start_speed=[5.0, 9.0], gravity_scale=2.4,
    size_over_life=sizes((0.0, 0.35, 0.6), (1.0, 0.32, 0.55)),
    color_gradient=keys((0.0, ROCK + [1.0]), (0.85, ROCK + [1.0]), (1.0, ROCK + [0.0]))), 5)
# Stone sparks: the axe and the rock grind (radial streaks, tinted: warm steel, crimson, lava, cold storm).
define("BarbarianGrit", shot(**ADD, shape_position=[0.0, 0.5, 0.0],
    max_particles=1024, lifetime=[0.3, 0.55], texture=f"{P}/ember.png", render_mode="stretched", direction="outward",
    start_speed=[7.0, 13.0], gravity_scale=1.6, drag=2.4, color_intensity=7.0, stretch_length_scale=1.0,
    stretch_speed_scale=0.05, size_over_life=sizes((0.0, 0.06, 0.1), (1.0, 0.02, 0.04)),
    color_gradient=keys((0.0, [1.0, 0.97, 0.9, 1.0]), (0.45, [1.0, 0.82, 0.6, 1.0]), (1.0, [0.9, 0.55, 0.3, 0.0]))), 11)
# Clods of earth sprayed up from the ground; they vanish into it when they land.
define("BarbarianDirtSpray", shot(
    max_particles=1024, lifetime=[0.55, 0.85], texture=f"{A}/dirt_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", blend_mode="normal", lighting="surface",
    start_rotation=[0, 360], spin_speed=[40, 160], random_spin_direction=True, shape_position=[0.0, 1.2, 0.0],
    direction="outward", start_speed=[3.5, 6.5], gravity_scale=1.8, drag=0.8, collision="kill", collision_planes=GROUND,
    size_over_life=sizes((0.0, 0.35, 0.55), (1.0, 0.7, 1.0)),
    color_gradient=keys((0.0, EARTH + [1.0]), (0.7, EARTH + [0.9]), (1.0, EARTH + [0.0]))), 4)

# ------------------------------------------------------------------------------------------------ ground scars
# Radial cracks (headed: the dark crack and its glow share a heading so they overlay). The size is the peak quad.
define("BarbarianCrack", shot(**HEADED,
    max_particles=128, lifetime=[2.2, 2.2], texture=f"{W}/crack_radial.png", blend_mode="normal",
    size_over_life=sizes((0.0, 0.0), (0.04, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, CRACK + [0.0]), (0.03, CRACK + [0.9]), (0.6, CRACK + [0.8]), (1.0, CRACK + [0.0]))), -20)
define("BarbarianCrackGlow", shot(**ADD, **HEADED,
    max_particles=64, lifetime=[1.6, 1.6], texture=f"{W}/crack_radial.png", color_intensity=2.4,
    size_over_life=sizes((0.0, 0.0), (0.04, 1.0), (1.0, 1.0)), intensity_over_life=scalar((0.0, 1.5), (0.15, 1.0), (1.0, 0.35)),
    color_gradient=fade((0.0, 0.0), (0.03, 1.0), (0.6, 0.7), (1.0, 0.0))), -19)
# Fissure lines: one jagged strand per segment along its heading (the quad is 1.5 x wider than long so the strand
# reads 5-9 cm wide); the script sizes it 1.5 x the segment so neighbours overlap.
define("BarbarianFissure", shot(**HEADED, **BOLTS,
    max_particles=512, lifetime=[1.8, 1.8], blend_mode="normal", size_axes=[1.5, 1.0, 1.0],
    size_over_life=sizes((0.0, 0.0), (0.05, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, CRACK + [0.0]), (0.04, CRACK + [0.95]), (0.7, CRACK + [0.85]), (1.0, CRACK + [0.0]))), -20)
# The glowing seam in Skullsplitter's fissure: a crimson strand over the dark crack line.
define("BarbarianFissureGlow", shot(**ADD, **HEADED, **BOLTS,
    max_particles=512, lifetime=[0.9, 0.9], size_axes=[1.5, 1.0, 1.0], color_intensity=3.0,
    size_over_life=sizes((0.0, 0.0), (0.05, 1.0), (1.0, 1.0)),
    intensity_over_life=scalar((0.0, 1.6), (0.1, 1.0), (0.6, 0.85), (1.0, 0.45)),
    color_gradient=fade((0.0, 0.0), (0.04, 1.0), (0.85, 0.75), (1.0, 0.0))), -18)
# Broken earth lining a fissure: pebble clusters stretched along it.
define("BarbarianRubble", shot(**HEADED,
    max_particles=512, lifetime=[1.8, 1.8], texture=f"{A}/dirt_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, blend_mode="normal", lighting="surface", size_axes=[0.75, 1.0, 1.0],
    size_over_life=sizes((0.0, 0.0), (0.06, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, RUBBLE + [0.0]), (0.05, RUBBLE + [1.0]), (0.75, RUBBLE + [0.9]), (1.0, RUBBLE + [0.0]))), -16)
# Fault Line's crack: a dark groove, 0.3 m wide on 0.52 m strokes (1 m segments), 0.6 m on Tectonic Rift's 1 m strokes
# (2 m spans), where it is the dark crust around the lava. Scripted lifetime.
define("BarbarianGroove", shot(**HEADED, **TRACE,
    max_particles=1024, lifetime=[2.0, 2.0], blend_mode="normal", size_axes=[1.84, 1.0, 1.0],
    size_over_life=sizes((0.0, 0.0), (0.04, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, GROOVE + [0.0]), (0.03, GROOVE + [0.95]), (0.8, GROOVE + [0.9]), (1.0, GROOVE + [0.0]))), -20)
# A rolling dust wave pushed out to both sides as the crack runs (EmitMoving sideways; lighter than the impact dust so
# it reads on the moor).
define("BarbarianDustWave", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.9, 1.3], drag=3.2, acceleration=[0.0, 0.35, 0.0],
    size_over_life=sizes((0.0, 1.0), (1.0, 2.0, 2.4)),
    color_gradient=keys((0.0, DUST_WAVE + [0.0]), (0.08, DUST_WAVE + [0.75]), (0.5, DUST_WAVE + [0.5]),
                        (1.0, DUST_SETTLED + [0.0]))), 2)
# Rock spikes bursting from the ground: chunky (1.4 x wider than the sheet's spikes), standing on their base (pivot),
# up in 0.06 s, tilted a little, sinking at the end. The size is the spike's height.
define("BarbarianRockSpike", shot(
    max_particles=512, lifetime=[0.6, 0.75], texture=f"{W}/rock_spike_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 0.96], blend_mode="normal",
    lighting="surface", start_rotation=[-14, 14], size_axes=[1.4, 1.0, 1.0],
    size_over_life=sizes((0.0, 0.0), (0.09, 1.0), (0.72, 0.94), (1.0, 0.0)),
    color_gradient=keys((0.0, ROCK + [1.0]), (0.85, ROCK + [1.0]), (1.0, ROCK + [0.0]))), 0)

# ------------------------------------------------------------------------------------------------ magma and blood
# Molten droplets spurting 1.5-2.8 m up and dying on the ground: lava on its own, blood spurts tinted crimson.
define("BarbarianMagma", shot(**ADD,
    max_particles=1024, lifetime=[0.5, 0.85], texture=f"{P}/ember.png", render_mode="stretched", shape="cone",
    cone_angle=28.0, direction="up", start_speed=[4.0, 7.5], gravity_scale=1.6, collision="kill", collision_planes=GROUND,
    color_intensity=6.0, stretch_length_scale=1.3, stretch_speed_scale=0.035,
    size_over_life=sizes((0.0, 0.1, 0.16), (1.0, 0.05, 0.08)),
    color_gradient=keys((0.0, [1.0, 0.92, 0.7, 1.0]), (0.35, [1.0, 0.62, 0.22, 1.0]), (1.0, [0.85, 0.25, 0.06, 0.0]))), 10)
# Tectonic Rift's molten crack over its dark crust (BarbarianGroove), drawn as the same zigzag strokes (1 m strokes
# over 2 m spans) and lasting until the lava cools (scripted lifetime). The seam is 0.22 m of orange cooling to deep
# red, leaving the 0.6 m crust dark at its edges; the core 0.1 m of white-yellow heat, gone by 60% of the life.
define("BarbarianLavaSeam", shot(**ADD, **HEADED, **TRACE,
    max_particles=1024, lifetime=[3.0, 3.0], size_axes=[0.7, 1.0, 1.0], color_intensity=2.4,
    size_over_life=sizes((0.0, 0.0), (0.04, 1.0), (1.0, 1.0)), intensity_over_life=scalar((0.0, 1.4), (0.2, 1.0), (1.0, 0.6)),
    color_gradient=keys((0.0, [1.0, 0.75, 0.35, 0.0]), (0.03, [1.0, 0.7, 0.3, 1.0]), (0.25, [1.0, 0.5, 0.15, 1.0]),
                        (0.75, [1.0, 0.32, 0.06, 0.85]), (1.0, [0.7, 0.15, 0.03, 0.0]))), -17)
define("BarbarianLavaCore", shot(**ADD, **HEADED, **TRACE,
    max_particles=1024, lifetime=[3.0, 3.0], size_axes=[0.3, 1.0, 1.0], color_intensity=2.0,
    size_over_life=sizes((0.0, 0.0), (0.04, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, [1.0, 0.95, 0.8, 0.0]), (0.03, [1.0, 0.95, 0.75, 1.0]), (0.35, [1.0, 0.85, 0.5, 0.8]),
                        (0.6, [1.0, 0.6, 0.25, 0.0]), (1.0, [1.0, 0.6, 0.25, 0.0]))), -16)
# The heat the lava throws on the ground around it: a soft glow stretched along each span (never a disc; the script
# makes it 1.6 x the span so neighbours merge into one band), lighting whatever stands in it.
define("BarbarianLavaGlow", shot(**ADD, **HEADED, **light(4, 12.0, 4.0),
    max_particles=256, lifetime=[3.0, 3.0], texture=f"{P}/soft.png", primitive="octagon", size_axes=[0.3, 1.0, 1.0],
    color_intensity=0.9, size_over_life=sizes((0.0, 0.0), (0.06, 1.0), (1.0, 0.9)),
    color_gradient=keys((0.0, [1.0, 0.4, 0.1, 0.0]), (0.05, [1.0, 0.4, 0.1, 0.6]), (0.7, [1.0, 0.28, 0.06, 0.42]),
                        (1.0, [0.8, 0.15, 0.03, 0.0]))), -21)
# Embers lifting off the lava while it lasts (FaultLine sprinkles them along each line).
define("BarbarianLavaEmbers", shot(**ADD,
    max_particles=512, lifetime=[0.9, 1.5], texture=f"{P}/ember.png", render_mode="camera_facing", direction="up",
    direction_spread=30.0, start_speed=[0.6, 1.4], gravity_scale=-0.1, turbulence_strength=0.6, color_intensity=6.0,
    size_over_life=sizes((0.0, 0.06, 0.1), (1.0, 0.0, 0.02)),
    color_gradient=keys((0.0, [1.0, 0.85, 0.5, 0.0]), (0.1, [1.0, 0.75, 0.35, 1.0]), (0.6, [1.0, 0.45, 0.12, 0.8]),
                        (1.0, [0.8, 0.2, 0.05, 0.0]))), 7)
# The lava zone's bubbling (GroundZones, unsized, tinted lava): small droplets hopping 0.3-0.7 m.
define("BarbarianLavaSpurt", shot(**ADD,
    max_particles=1024, lifetime=[0.4, 0.7], texture=f"{P}/ember.png", render_mode="stretched", shape="cone",
    cone_angle=35.0, direction="up", start_speed=[2.5, 4.5], gravity_scale=1.4, collision="kill", collision_planes=GROUND,
    color_intensity=6.0, stretch_length_scale=1.2, stretch_speed_scale=0.04,
    size_over_life=sizes((0.0, 0.07, 0.12), (1.0, 0.03, 0.05)),
    color_gradient=keys((0.0, [1.0, 1.0, 0.85, 1.0]), (0.5, [1.0, 0.85, 0.65, 1.0]), (1.0, [0.8, 0.6, 0.5, 0.0]))), 10)
define("BarbarianLavaSmoke", shot(**SMOKE_LIT, shape_position=UP_BIAS,
    max_particles=256, lifetime=[1.8, 2.6], direction="outward", start_speed=[0.3, 0.8], drag=1.0,
    acceleration=[0.0, 0.7, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 2.2, 2.8)),
    color_gradient=keys((0.0, LAVA_SMOKE + [0.0]), (0.2, LAVA_SMOKE + [0.45]), (1.0, [0.28, 0.27, 0.27, 0.0]))), 1)

# ------------------------------------------------------------------------------------------------ ravager chop
# The axe's arc at waist height, headed along the aim: it sweeps ~45 deg over its 0.2 s (Cw turns toward +X like
# Ground.Rotate, the sweep of sweepSign -1; Ccw the other way) and grows 12% outward as it fades.
define("BarbarianChopArcCw", shot(**ADD, **HEADED, **ARC,
    max_particles=64, lifetime=[0.2, 0.2], start_rotation=[22.5, 22.5], spin_speed=[-225, -225], color_intensity=2.6,
    size_over_life=sizes((0.0, 1.0), (0.3, 1.07), (1.0, 1.12)),
    color_gradient=fade((0.0, 0.0), (0.06, 1.0), (0.35, 0.8), (1.0, 0.0))), 4)
define("BarbarianChopArcCcw", shot(**ADD, **HEADED, **ARC,
    max_particles=64, lifetime=[0.2, 0.2], start_rotation=[-22.5, -22.5], spin_speed=[225, 225], color_intensity=2.6,
    size_over_life=sizes((0.0, 1.0), (0.3, 1.07), (1.0, 1.12)),
    color_gradient=fade((0.0, 0.0), (0.06, 1.0), (0.35, 0.8), (1.0, 0.0))), 4)
# A soft, broad afterimage inside the arc: slash_02's crescent (radius 0.33 x the quad around uv (0.5, 0.43)) turned
# to face the aim.
define("BarbarianChopSmear", shot(**ADD, **HEADED,
    max_particles=64, lifetime=[0.3, 0.3], texture=f"{K}/slash_02.png", pivot=[0.5, 0.43], primitive="cutout",
    start_rotation=[180, 180], color_intensity=1.1, size_over_life=sizes((0.0, 1.0), (1.0, 1.15)),
    color_gradient=fade((0.0, 0.0), (0.08, 0.85), (0.4, 0.5), (1.0, 0.0))), 3)
# Steel streaks along a swing (EmitMoving along the motion, drag stops them): the chop's sweep, the whirl's blade tips,
# the leap's wind. The spark texture is a thin line, so the size is ~14x its visible width.
define("BarbarianSweepStreak", shot(**ADD, **STREAK,
    max_particles=1024, lifetime=[0.2, 0.2], drag=7.0, stretch_length_scale=1.6, stretch_speed_scale=0.06,
    color_intensity=3.0, size_over_life=sizes((0.0, 1.0), (1.0, 0.45)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 9)

# ------------------------------------------------------------------------------------------------ whirlwind
# A blade arc trailing each blade: emitted every 0.05 s centred on the hero (radius = blade reach), its leading end at
# the blade, turning after it at a third of its speed while it fades: the arcs chain into a spinning ring.
define("BarbarianWhirlArc", shot(**ADD, **HEADED, **ARC,
    max_particles=256, lifetime=[0.16, 0.16], start_rotation=[60, 60], spin_speed=[-300, -300], color_intensity=1.8,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.06)), color_gradient=fade((0.0, 0.0), (0.12, 0.9), (0.5, 0.5), (1.0, 0.0))), 4)
# Maelstrom: the storm's dark eye reaching past the pull reach, turning slowly with its arms crawling inward
# (positive spin with this spiral); three overlap at once.
define("BarbarianStormVortex", shot(
    max_particles=64, lifetime=[1.4, 1.4], texture=f"{W}/vortex.png", render_mode="horizontal_billboard",
    blend_mode="normal", primitive="octagon", start_rotation=[0, 360], spin_speed=[55, 55],
    size_over_life=sizes((0.0, 1.0), (1.0, 0.85)),
    color_gradient=keys((0.0, STORM_SLATE + [0.0]), (0.25, STORM_SLATE + [0.9]), (0.7, STORM_SLATE + [0.85]),
                        (1.0, STORM_SLATE + [0.0]))), -14)
# Lightning webs crackling in the storm (flickering twice), lighting it cold.
define("BarbarianStormBolt", shot(**ADD, **light(2, 16.0, 6.0),
    max_particles=256, lifetime=[0.12, 0.18], texture=f"{A}/sparks_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", start_rotation=[0, 360], color_intensity=3.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.15)),
    color_gradient=fade((0.0, 1.0), (0.25, 0.35), (0.45, 0.95), (0.7, 0.3), (1.0, 0.0))), 10)
# A bolt running along the ground from the storm's rim toward the hero (headed radially, sized to the reach).
define("BarbarianStormStrand", shot(**ADD, **HEADED, **BOLTS,
    max_particles=128, lifetime=[0.14, 0.14], size_axes=[1.0, 1.0, 1.0], color_intensity=3.2,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (0.3, 0.4), (0.5, 1.0), (1.0, 0.0))), 9)
define("BarbarianStormDust", shot(**SMOKE_LIT, shape_position=[0.0, 0.5, 0.0],
    max_particles=512, lifetime=[0.9, 1.3], direction="outward", start_speed=[2.0, 5.0], drag=3.0,
    turbulence_strength=0.6, size_over_life=sizes((0.0, 1.0), (1.0, 1.7, 2.1)),
    color_gradient=keys((0.0, STORM_DUST + [0.0]), (0.12, STORM_DUST + [0.6]), (1.0, STORM_DUST + [0.0]))), 1)

# ------------------------------------------------------------------------------------------------ bloodlust
# Full Fury: blood-red flame licks around the body (scripted peak size, tinted) and embers in the eyes.
define("BarbarianFuryFlame", shot(**ADD, **FLAMES, **SPIN,
    max_particles=256, lifetime=[0.4, 0.6], render_mode="camera_facing", direction="up", direction_spread=20.0,
    start_speed=[0.8, 1.6], drag=1.2, color_intensity=2.2, soft_fade_distance=0.3,
    size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.35)),
    color_gradient=keys((0.0, [1.0, 0.9, 0.8, 0.0]), (0.15, [1.0, 0.85, 0.75, 0.6]), (0.6, [0.9, 0.7, 0.6, 0.4]),
                        (1.0, [0.5, 0.3, 0.25, 0.0]))), 6)
define("BarbarianFuryEmbers", shot(**ADD,
    max_particles=256, lifetime=[0.5, 0.8], texture=f"{P}/ember.png", render_mode="camera_facing", direction="up",
    direction_spread=35.0, start_speed=[0.6, 1.4], turbulence_strength=0.4, color_intensity=7.0,
    size_over_life=sizes((0.0, 0.08, 0.13), (1.0, 0.02, 0.04)),
    color_gradient=keys((0.0, [1.0, 0.85, 0.7, 0.0]), (0.1, [1.0, 0.8, 0.65, 1.0]), (1.0, [0.8, 0.45, 0.35, 0.0]))), 7)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_barbarian.py")
