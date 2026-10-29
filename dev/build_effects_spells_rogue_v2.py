"""Rogue class weapons of design update 5: Venom Shivs / Nightshade Plague and Shadow Dancers / Legion of Shadows.
Poison green over near-black ink for the venom, berry violet for Nightshade, shadow violet for the clones. Emitters
that already look right are reused by name from build_effects_spells_rogue.py (RogueFlash, RogueGlint, RogueSparks,
RogueKnifeArc, RogueKnifeCut, RogueWidowTracer, RogueVenomDrops, RogueVenomRing, RogueShadowPuff, RogueShadowShock,
RogueStormGlints, RogueStormBlade, RogueMarkMotes, RogueMomentumWisp, RogueCloneRim).

    python build_effects_spells_rogue_v2.py                 list the effects defined here (no editor call)
    python build_effects_spells_rogue_v2.py build [group]   create missing effects and (re)apply their settings

How the scripts fire them (Weapons/Rogue/VenomShivs.cs, ToxicClouds.cs, ShadowDancers.cs):
- Sized layers: the script's size is the particle's BIRTH size, or its PEAK when the curve starts at 0.
- Status looks (bubbles, drips, fumes) are emitted on a budget at a few poisoned enemies per tick, with their own
  sizes (bubbles, drips) or sized by the enemy's height (fumes).
- RogueRuptureGlob is ONE trail particle launched on a ballistic arc from a rupture onto a splashed neighbour; its
  gravity (gravity_scale 1) must match GLOB_GRAVITY in VenomShivs.cs, the script picks the flight time.
- Refreshed sprites (clone shade, rim, eyes, pool; cloud stain): one particle every refresh, living a little longer,
  so the next one cross-fades over it; the clone's shimmer comes from that cross-fade plus a small jitter.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects_spells import K, A, ADD, DECAL, SCRIPTED, FLAMES, fade  # noqa: E402
from build_effects_spells_rogue import (EMBER, FLAT, GLOW, SHADE, SPLAT, SHAFT, WISPS, SMOKE_DARK,  # noqa: E402
                                        REFRESHED)
from effect_helpers import Registry, shot, sizes, keys  # noqa: E402

GROUP = "RogueFx2"
REGISTRY = Registry((GROUP,))

SPLASH_SMALL = f"{K}/scorch_01.png"
SPLASH_BIG = f"{K}/scorch_02.png"
# The clone figure: flame_05's flame fills 48% of the quad's height and ends 28% above its bottom edge, so a pivot
# at 72% stands the flame itself on the ground.
FIGURE_PIVOT = [0.5, 0.72]
BUBBLE = f"{K}/circle_02.png"
STAR_BLOOM = f"{K}/star_08.png"
SOFT_DISC = f"{K}/circle_05.png"
MOTTLE = f"{K}/smoke_07.png"

# Venom tones for untinted layers: sickly fumes, near-black ink, dark stains.
FUME = [0.2, 0.4, 0.12]
FUME_END = [0.14, 0.28, 0.1]
INK = [0.03, 0.08, 0.03]
RUPTURE_FUME = [0.07, 0.16, 0.06]
STAIN = [0.04, 0.12, 0.04]
CLOUD = [0.2, 0.42, 0.12]
CLOUD_END = [0.12, 0.24, 0.09]
CLOUD_STAIN = [0.05, 0.13, 0.04]
# Clone tones: a dark violet shade over a darker pool.
DANCER_SHADE = [0.12, 0.06, 0.2]
DANCER_POOL = [0.06, 0.02, 0.1]


def define(name, props, sort):
    REGISTRY.define(GROUP, name, props, sort)


def refreshed(rgb, peak):
    """The refresh fade (in over 17%, out over the last 17%) of one colour at a peak alpha."""
    return keys((0.0, rgb + [0.0]), (0.17, rgb + [peak]), (0.83, rgb + [peak]), (1.0, rgb + [0.0]))


# ------------------------------------------------------------------------------------------------- venom shivs
# Drips falling off a flying shiv (one per shiv every 35 ms; scripted size = birth).
define("RogueShivDrip", shot(**ADD,
    max_particles=1024, lifetime=[0.25, 0.4], texture=EMBER, render_mode="camera_facing", direction="outward",
    start_speed=[0.1, 0.4], gravity_scale=1.5, color_intensity=4.0, size_over_life=sizes((0.0, 1.0), (1.0, 0.35)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.8), (1.0, 0.0))), 8)
# A small venom splash where a shiv strikes.
define("RogueVenomSplat", shot(**ADD,
    max_particles=512, lifetime=[0.18, 0.22], texture=SPLASH_SMALL, render_mode="camera_facing", start_rotation=[0, 360],
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.2, 0.9), (1.0, 1.1)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.8), (1.0, 0.0))), 7)

# ------------------------------------------------------------------------------------------------ venom status
# Bubbles rising off a poisoned body and popping (they swell at the end); also burst from ruptures and clouds.
define("RogueVenomBubble", shot(**ADD,
    max_particles=2048, lifetime=[0.45, 0.75], texture=BUBBLE, render_mode="camera_facing", shape="sphere",
    shape_scale=[0.35, 0.4, 0.35], direction="up", direction_spread=25.0, start_speed=[0.3, 0.8], drag=1.0,
    turbulence_strength=0.5, color_intensity=2.6,
    size_over_life=sizes((0.0, 0.0), (0.2, 0.08, 0.12), (0.85, 0.12, 0.17), (1.0, 0.2, 0.26)),
    color_gradient=fade((0.0, 0.0), (0.15, 0.9), (0.85, 0.75), (1.0, 0.0))), 7)
# Drops running off a poisoned body.
define("RogueVenomDrip", shot(**ADD,
    max_particles=1024, lifetime=[0.35, 0.55], texture=EMBER, render_mode="stretched", shape="sphere",
    shape_scale=[0.3, 0.35, 0.3], direction="outward", start_speed=[0.1, 0.4], gravity_scale=1.3,
    stretch_length_scale=1.0, stretch_speed_scale=0.06, color_intensity=4.0,
    size_over_life=sizes((0.0, 0.05, 0.07), (1.0, 0.02, 0.03)), color_gradient=fade((0.0, 1.0), (0.7, 0.85), (1.0, 0.0))), 8)
# A green toxic vapour wafting off a poisoned body (the venom's answer to BurnLick; scripted size = peak, by the
# enemy's height, larger once primed).
define("RogueVenomAura", shot(**ADD,
    max_particles=1024, lifetime=[0.45, 0.65], texture=f"{A}/smoke_4x2.png", texture_sheet_tiles=[4, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", start_rotation=[0, 360], spin_speed=[30, 90],
    random_spin_direction=True, direction="up", direction_spread=20.0, start_speed=[0.5, 0.9], drag=1.0, color_intensity=1.3,
    size_over_life=sizes((0.0, 0.0), (0.3, 0.85), (1.0, 1.0)), color_gradient=fade((0.0, 0.0), (0.25, 0.6), (1.0, 0.0))), 6)
# Primed (a rupture away): sickly green fumes curling off the head (unlit, untinted; scripted size = birth).
define("RogueVenomFume", shot(**SMOKE_DARK,
    max_particles=1024, lifetime=[0.6, 0.9], direction="up", direction_spread=30.0, start_speed=[0.3, 0.6], drag=1.0,
    acceleration=[0.0, 0.4, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 1.7, 2.0)),
    color_gradient=keys((0.0, FUME + [0.0]), (0.2, FUME + [0.55]), (1.0, FUME_END + [0.0]))), 2)

# ----------------------------------------------------------------------------------------------------- rupture
# The green-black splash: a near-black ink splat (normal blend) under a bright venom splatter (additive), both
# bursting out to their size in ~0.1 s. Size unit = the rupture's scale (more stacks, bigger).
define("RogueRuptureInk", shot(
    max_particles=256, lifetime=[0.55, 0.6], texture=SPLAT, render_mode="camera_facing", blend_mode="normal",
    start_rotation=[0, 360], size_over_life=sizes((0.0, 0.0), (0.1, 0.9), (1.0, 1.05)),
    color_gradient=keys((0.0, INK + [0.0]), (0.05, INK + [0.95]), (0.6, INK + [0.85]), (1.0, INK + [0.0]))), 5)
define("RogueRuptureSplash", shot(**ADD,
    max_particles=256, lifetime=[0.3, 0.36], texture=SPLASH_BIG, render_mode="camera_facing", start_rotation=[0, 360],
    color_intensity=1.7, size_over_life=sizes((0.0, 0.0), (0.12, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.35, 0.85), (1.0, 0.0))), 7)
define("RogueRuptureDroplets", shot(**ADD,
    max_particles=2048, lifetime=[0.35, 0.6], texture=EMBER, render_mode="stretched", shape="sphere", direction="outward",
    start_speed=[4.0, 10.0], drag=1.5, gravity_scale=1.8, stretch_length_scale=1.0, stretch_speed_scale=0.04,
    color_intensity=4.5, size_over_life=sizes((0.0, 0.06, 0.1), (1.0, 0.02, 0.04)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.9), (1.0, 0.0))), 9)
# The splash on the ground under a rupture: a bright venom splatter bursting out flat (seen past any body).
define("RogueRuptureSplat", shot(**ADD,
    max_particles=256, lifetime=[0.4, 0.45], texture=SPLASH_BIG, render_mode="horizontal_billboard", start_rotation=[0, 360],
    color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.1, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.75), (1.0, 0.0))), 3)
# A venom glob arcing onto a splashed neighbour: one trail particle on a ballistic path (see the header).
define("RogueRuptureGlob", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=FLAT, render_mode="trail", trail_length=0.12, trail_width_end=0.2, gravity_scale=1.0,
    color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 0.8)), color_gradient=fade((0.0, 1.0), (0.85, 0.9), (1.0, 0.0))), 8)
# Dark green-black fumes lingering over a rupture (untinted, own sizes).
define("RogueRuptureFume", shot(**SMOKE_DARK,
    max_particles=512, lifetime=[0.9, 1.3], shape="sphere", direction="outward", start_speed=[0.8, 1.6], drag=2.5,
    acceleration=[0.0, 0.5, 0.0], size_over_life=sizes((0.0, 0.6, 0.8), (1.0, 1.4, 1.8)),
    color_gradient=keys((0.0, RUPTURE_FUME + [0.0]), (0.12, RUPTURE_FUME + [0.7]), (1.0, [0.06, 0.12, 0.05, 0.0]))), 1)
# The dark venom stain left on the ground (untinted; scripted size and life).
define("RogueVenomStain", shot(**DECAL, **SCRIPTED,
    max_particles=256, texture=SPLAT, color_intensity=1.0, size_over_life=sizes((0.0, 0.0), (0.08, 0.95), (1.0, 1.0)),
    color_gradient=keys((0.0, STAIN + [0.0]), (0.05, STAIN + [0.8]), (0.7, STAIN + [0.6]), (1.0, STAIN + [0.0]))), -20)

# ------------------------------------------------------------------------------------------- nightshade plague
# A berry-violet nightshade bloom opening over each rupture.
define("RogueNightshadeBloom", shot(**ADD,
    max_particles=256, lifetime=[0.35, 0.4], texture=STAR_BLOOM, render_mode="camera_facing", start_rotation=[0, 360],
    spin_speed=[60, 140], random_spin_direction=True, color_intensity=2.0,
    size_over_life=sizes((0.0, 0.0), (0.12, 0.9), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 8)
# The toxic cloud: thin green-black fumes at knee to chest height (scripted size = birth), faint green glow wisps
# inside, and a mottled stain refreshed under it.
define("RogueToxicCloud", shot(**SMOKE_DARK,
    max_particles=512, lifetime=[1.0, 1.4], direction="up", direction_spread=70.0, start_speed=[0.15, 0.4], drag=1.0,
    acceleration=[0.0, 0.2, 0.0], turbulence_strength=0.4, turbulence_frequency=0.6, turbulence_speed=0.4,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.5, 1.8)),
    color_gradient=keys((0.0, CLOUD + [0.0]), (0.25, CLOUD + [0.4]), (0.7, CLOUD_END + [0.32]), (1.0, CLOUD_END + [0.0]))), 1)
define("RogueToxicGlow", shot(**WISPS, **ADD,
    max_particles=512, lifetime=[0.6, 0.9], render_mode="camera_facing", start_rotation=[0, 360], spin_speed=[40, 120],
    random_spin_direction=True, direction="up", direction_spread=40.0, start_speed=[0.2, 0.5], color_intensity=2.0,
    size_over_life=sizes((0.0, 0.0), (0.3, 1.0), (1.0, 1.2)), color_gradient=fade((0.0, 0.0), (0.3, 0.6), (1.0, 0.0))), 3)
define("RogueToxicStain", shot(**DECAL, **SCRIPTED,
    max_particles=128, texture=MOTTLE, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, CLOUD_STAIN + [0.0]), (0.25, CLOUD_STAIN + [0.5]), (0.75, CLOUD_STAIN + [0.5]), (1.0, CLOUD_STAIN + [0.0]))), -19)

# --------------------------------------------------------------------------------------------- shadow dancers
# The clone: a dark shade (untinted, alpha by the script's presence) drawn over a slightly larger additive violet rim
# (tinted), so only the rim's edge glows around it; pale eyes on the hood and a dark pool on the ground; all refreshed
# (scripted size = sprite size / diameter, life = refresh x 1.3).
define("RogueDancerShade", shot(**SCRIPTED,
    max_particles=256, texture=SHADE, render_mode="vertical_billboard", pivot=FIGURE_PIVOT, blend_mode="normal",
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=refreshed(DANCER_SHADE, 0.88)), 6)
define("RogueDancerRim", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=SHADE, render_mode="vertical_billboard", pivot=FIGURE_PIVOT, size_axes=[1.18, 1.07, 1.0],
    color_intensity=1.8, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=REFRESHED), 4)
define("RogueDancerEyes", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=GLOW, render_mode="camera_facing", primitive="octagon", color_intensity=3.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=REFRESHED), 12)
define("RogueDancerPool", shot(**DECAL, **SCRIPTED,
    max_particles=256, texture=SOFT_DISC, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)),
    color_gradient=refreshed(DANCER_POOL, 0.7)), -18)
# Legion of Shadows: violet shadow flames licking up around each clone's feet.
define("RogueLegionFlame", shot(**ADD, **FLAMES,
    max_particles=512, lifetime=[0.3, 0.45], render_mode="camera_facing", shape="circle", shape_scale=[0.35, 0.05, 0.35],
    direction="up", direction_spread=15.0, start_speed=[1.0, 1.8], drag=1.0, start_rotation=[-15, 15], color_intensity=1.6,
    size_over_life=sizes((0.0, 0.22, 0.3), (0.4, 0.26, 0.34), (1.0, 0.08, 0.12)),
    color_gradient=fade((0.0, 0.0), (0.15, 0.85), (1.0, 0.0))), 5)
# Legion: a violet shaft rising where a clone arrives from the smoke or bursts (pivot at its foot; scripted size = peak).
define("RogueLegionPillar", shot(**ADD,
    max_particles=128, lifetime=[0.45, 0.45], texture=SHAFT, render_mode="vertical_billboard", pivot=[0.5, 1.0],
    size_axes=[0.4, 1.8, 1.0], color_intensity=2.4, size_over_life=sizes((0.0, 0.0), (0.1, 1.0), (1.0, 0.7)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_rogue_v2.py")
