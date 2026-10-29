"""Rogue spell effects: Repeater Crossbow / Widowmaker, Fan of Knives / Storm of Steel, Assassin's Mark / Death
Sentence, the Smoke Roll active and the Momentum signature. Cold steel, shadow violet, poison green accents and the
Mark's crimson; fast, sharp, precise.

    python build_effects_spells_rogue.py                 list the effects defined here (no editor call)
    python build_effects_spells_rogue.py build [group]   create missing effects and (re)apply their settings

How the scripts fire them:
- FxComposite layers (Fx/Effects.cs): a sized layer's size is the particle's BIRTH size, or its PEAK when the curve
  starts at 0; the size unit is 1 m for the hit moments (HIT_SIZE in the scripts). Unsized layers keep the sizes
  written here.
- EmitMoving (oriented sprites): align_to_direction turns the texture's top along the given velocity, so muzzle
  flicks, knife cuts, thrusts and crescent blades point the way they move. The size and lifetime come from the script.
- Trail tracers (RogueBoltTracer, RogueWidowTracer): ONE particle launched with the bolt's own velocity draws its
  path; the script picks its lifetime (to the target, or the whole flight for bolts that pierce everything).
- Projectile heads (Weapons/Rogue/RogueLooks.cs): one particle per frame at the projectile, birth size = head size.
- Per-frame sprites (roll afterimages, crit charges): short lives, so they follow what they mark.
- Refreshed sprites (mark reticles, Death Sentence runes): one particle every 0.25 s living 0.3 s, drifting with the
  mark; the fade in of the new one meets the fade out of the old one, so the sigil stays steady and never stacks.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects_spells import (P, V, K, W, A, WHITE, GROUND, ADD, SMOKE_LIT, DECAL, SCRIPTED,  # noqa: E402
                                  light, fade)
from effect_helpers import Registry, shot, sizes, keys  # noqa: E402

GROUP = "RogueFx"
REGISTRY = Registry((GROUP,))

TEXTURES = "app:/data/Textures"
GLOW = f"{P}/glow.png"
EMBER = f"{P}/ember.png"
STREAK = f"{P}/spark.png"
SPARKLE = f"{P}/sparkle.png"
RUNE = f"{P}/rune_circle.png"
SHAFT = f"{P}/light_shaft.png"
FLAT = f"{TEXTURES}/white.png"
STAR = f"{K}/star_07.png"
RETICLE = f"{K}/magic_03.png"
MUZZLE = f"{K}/muzzle_04.png"
RING = f"{K}/circle_02.png"
CRESCENT = f"{K}/slash_03.png"
BOW = f"{K}/slash_04.png"
SHADE = f"{K}/flame_05.png"
SPLAT = f"{K}/scorch_03.png"
SLASH_X = f"{W}/slash_x.png"
SHOCK = f"{W}/shock_ring.png"
TWIRLS = {"texture": f"{A}/twirls_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True}
CRYSTAL = {"texture": f"{V}/crystal_shard_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True}
WISPS = {"texture": f"{V}/wisp_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True}
SMOKE_DARK = {"texture": f"{A}/smoke_4x2.png", "texture_sheet_tiles": [4, 2], "texture_sheet_randomize": True,
              "render_mode": "billboard", "blend_mode": "normal", "soft_fade_distance": 0.4, "start_rotation": [0, 360],
              "spin_speed": [20, 70], "random_spin_direction": True}
# Oriented sprites: the texture's top follows the velocity EmitMoving gives.
ALIGNED = {"align_to_direction": True}

VENOM = [0.45, 1.0, 0.4]
VENOM_PALE = [0.72, 1.0, 0.76]
SHADOW = [0.17, 0.09, 0.25]


def define(name, props, sort):
    REGISTRY.define(GROUP, name, props, sort)


# ----------------------------------------------------------------------------------------------- shared accents
# One short flash per moment; the brightest few light the moor.
define("RogueFlash", shot(**ADD, **light(3, 20.0, 6.0),
    max_particles=512, lifetime=[0.1, 0.1], texture=GLOW, render_mode="camera_facing", primitive="octagon",
    color_intensity=1.9, size_over_life=sizes((0.0, 0.0), (0.15, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.3, 0.65), (1.0, 0.0))), 12)
# A crisp four-point steel glint that pops and is gone.
define("RogueGlint", shot(**ADD,
    max_particles=2048, lifetime=[0.12, 0.16], texture=STAR, render_mode="camera_facing", start_rotation=[-8, 8],
    spin_speed=[20, 60], random_spin_direction=True, color_intensity=3.5,
    size_over_life=sizes((0.0, 0.0), (0.18, 1.0), (1.0, 0.15)), color_gradient=fade((0.0, 1.0), (0.6, 0.9), (1.0, 0.0))), 10)
define("RogueSparks", shot(**ADD,
    max_particles=2048, lifetime=[0.16, 0.3], texture=EMBER, render_mode="stretched", shape="sphere", direction="outward",
    start_speed=[6.0, 13.0], drag=3.0, gravity_scale=1.0, stretch_length_scale=1.0, stretch_speed_scale=0.03,
    color_intensity=6.0, size_over_life=sizes((0.0, 0.05, 0.08), (1.0, 0.015, 0.03)),
    color_gradient=keys((0.0, WHITE + [1.0]), (0.5, [0.9, 0.9, 0.9, 0.9]), (1.0, [0.7, 0.7, 0.7, 0.0]))), 11)
# Shadow smoke: unlit dark violet, so it reads as shade on the moonlit moor (untinted).
define("RogueShadowPuff", shot(**SMOKE_DARK,
    max_particles=1024, lifetime=[0.45, 0.7], shape="sphere", direction="outward", start_speed=[1.5, 3.5], drag=4.0,
    acceleration=[0.0, 0.8, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 1.8, 2.2)),
    color_gradient=keys((0.0, SHADOW + [0.0]), (0.12, SHADOW + [0.7]), (1.0, [0.12, 0.07, 0.18, 0.0]))), 1)
define("RogueShadowShock", shot(**ADD,
    max_particles=128, lifetime=[0.45, 0.45], texture=SHOCK, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.4, size_over_life=sizes((0.0, 0.0), (0.05, 0.3), (0.45, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.6), (1.0, 0.0))), 2)
define("RogueDust", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.5, 0.8], shape="circle", direction="outward", shape_position=[0.0, 0.35, 0.0],
    start_speed=[1.0, 2.5], drag=3.0, acceleration=[0.0, 0.4, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 1.8, 2.2)),
    color_gradient=keys((0.0, [0.5, 0.47, 0.44, 0.0]), (0.15, [0.5, 0.47, 0.44, 0.45]), (1.0, [0.45, 0.43, 0.42, 0.0]))), 1)

# ---------------------------------------------------------------------------------------- crossbow / widowmaker
# Muzzle flick: a narrow flame from the crossbow along the shot (pivot at its base).
define("RogueMuzzleFlick", shot(**ADD, **SCRIPTED, **ALIGNED,
    max_particles=256, texture=MUZZLE, render_mode="billboard", pivot=[0.5, 1.0], size_axes=[0.65, 1.3, 1.0],
    color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.55)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.8), (1.0, 0.0))), 9)
# Tracers: one particle per bolt flies the bolt's path; trail_length seconds of it draw a thin bright line.
define("RogueBoltTracer", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=FLAT, render_mode="trail", trail_length=0.07, trail_width_end=0.1, color_intensity=2.6,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (0.8, 0.85), (1.0, 0.0))), 8)
define("RogueWidowTracer", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=FLAT, render_mode="trail", trail_length=0.13, trail_width_end=0.25, color_intensity=2.1,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (0.85, 0.9), (1.0, 0.0))), 8)
# Widowmaker head: venom crystals shed every frame hang in the air behind the bolt and sink as they fade.
define("RogueCrystalTrail", shot(**CRYSTAL, **ADD,
    max_particles=2048, lifetime=[0.3, 0.45], render_mode="camera_facing", start_rotation=[0, 360], spin_speed=[120, 360],
    random_spin_direction=True, shape="sphere", direction="outward", start_speed=[0.3, 0.9], drag=2.5, gravity_scale=0.35,
    color_intensity=2.0, size_over_life=sizes((0.0, 1.0), (0.25, 0.85), (1.0, 0.25)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.8), (1.0, 0.0))), 7)
# Poison drips from bolt hits (untinted green).
define("RogueVenomDrops", shot(**ADD,
    max_particles=2048, lifetime=[0.35, 0.6], texture=EMBER, render_mode="camera_facing", shape="sphere", direction="outward",
    start_speed=[1.0, 3.0], drag=1.5, gravity_scale=1.6, color_intensity=5.0,
    size_over_life=sizes((0.0, 0.05, 0.09), (1.0, 0.02, 0.04)),
    color_gradient=keys((0.0, VENOM_PALE + [1.0]), (0.4, VENOM + [0.9]), (1.0, [0.25, 0.8, 0.2, 0.0]))), 9)
# Widowmaker hits burst in crystal needles (tinted) and crystal debris that bounces (untinted pale venom).
define("RogueShardBurst", shot(**CRYSTAL, **ADD,
    max_particles=2048, lifetime=[0.25, 0.45], render_mode="stretched", shape="sphere", direction="outward",
    start_speed=[7.0, 14.0], drag=3.0, gravity_scale=0.8, stretch_length_scale=1.4, stretch_speed_scale=0.03,
    color_intensity=2.6, size_over_life=sizes((0.0, 0.14, 0.22), (1.0, 0.05, 0.1)),
    color_gradient=fade((0.0, 1.0), (0.7, 0.85), (1.0, 0.0))), 9)
define("RogueCrystalShatter", shot(**CRYSTAL,
    max_particles=2048, lifetime=[0.6, 0.9], render_mode="camera_facing", blend_mode="normal", lighting="surface",
    shape="sphere", direction="outward", start_speed=[3.0, 6.0], gravity_scale=2.0, collision="bounce",
    collision_planes=GROUND, restitution=0.3, max_bounces=2, start_rotation=[0, 360], spin_speed=[300, 700],
    random_spin_direction=True, color_intensity=1.6, size_over_life=sizes((0.0, 0.1, 0.2), (1.0, 0.07, 0.14)),
    color_gradient=keys((0.0, VENOM_PALE + [1.0]), (0.8, VENOM_PALE + [1.0]), (1.0, VENOM_PALE + [0.0]))), 6)
define("RogueVenomRing", shot(**ADD,
    max_particles=512, lifetime=[0.14, 0.14], texture=RING, render_mode="camera_facing", primitive="octagon",
    color_intensity=2.4, size_over_life=sizes((0.0, 0.0), (0.4, 0.8), (1.0, 1.0)), color_gradient=fade((0.0, 0.9), (1.0, 0.0))), 8)

# ----------------------------------------------------------------------------------- fan of knives / storm of steel
# A ring at knife height that grows with the knives (scripted size = diameter at the end, lifetime = its flight).
define("RogueFanRing", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=SHOCK, render_mode="horizontal_billboard", primitive="octagon", start_rotation=[0, 360],
    color_intensity=1.5, size_over_life=sizes((0.0, 0.0), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 0.9), (0.6, 0.6), (1.0, 0.0))), 2)
# The spin of the throw: a swirl of air around the rogue.
define("RogueFanSwirl", shot(**TWIRLS, **ADD,
    max_particles=128, lifetime=[0.25, 0.3], render_mode="horizontal_billboard", start_rotation=[0, 360],
    spin_speed=[500, 700], color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.3, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.12, 1.0), (0.5, 0.8), (1.0, 0.0))), 3)
# One crescent per knife flies out with it, its bow facing out (texture top along the knife).
define("RogueKnifeArc", shot(**ADD, **SCRIPTED, **ALIGNED,
    max_particles=1024, texture=BOW, render_mode="horizontal_billboard", color_intensity=2.2,
    size_over_life=sizes((0.0, 0.0), (0.25, 1.0), (1.0, 0.8)), color_gradient=fade((0.0, 0.0), (0.12, 1.0), (1.0, 0.0))), 4)
# Storm of Steel: crescents that slide around the rogue (tangential velocity) - a whirl of blades.
define("RogueStormBlade", shot(**ADD, **SCRIPTED, **ALIGNED,
    max_particles=1024, texture=CRESCENT, render_mode="horizontal_billboard", color_intensity=2.4,
    size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.9)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.6, 0.7), (1.0, 0.0))), 4)
define("RogueStormGlints", shot(**ADD,
    max_particles=1024, lifetime=[0.3, 0.5], texture=STAR, render_mode="camera_facing", shape="circle", direction="outward",
    start_speed=[4.0, 9.0], drag=3.0, start_rotation=[-10, 10], spin_speed=[90, 240], random_spin_direction=True,
    color_intensity=3.5, size_over_life=sizes((0.0, 0.12, 0.2), (1.0, 0.02, 0.04)),
    color_gradient=fade((0.0, 1.0), (0.7, 0.9), (1.0, 0.0))), 10)
# Knife head: a steel star on every knife, flying (a glinting streak) or stuck in the ground (one fixed glint).
define("RogueKnifeHead", shot(**ADD,
    max_particles=2048, lifetime=[0.09, 0.09], texture=STAR, render_mode="camera_facing", start_rotation=[0, 0],
    color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 0.3)), color_gradient=fade((0.0, 0.9), (1.0, 0.0))), 7)
# A crisp cut across a knife's path where it strikes (bow forward).
define("RogueKnifeCut", shot(**ADD, **SCRIPTED, **ALIGNED,
    max_particles=2048, texture=BOW, render_mode="billboard", color_intensity=2.4,
    size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.85)), color_gradient=fade((0.0, 1.0), (0.5, 0.75), (1.0, 0.0))), 5)

# ------------------------------------------------------------------------------- assassin's mark / death sentence
# The mark lands: a crimson reticle slams down onto the target (birth = the big size, settles at ~45%).
define("RogueMarkStamp", shot(**ADD,
    max_particles=256, lifetime=[0.45, 0.45], texture=RETICLE, render_mode="camera_facing", start_rotation=[0, 0],
    color_intensity=3.0, size_over_life=sizes((0.0, 1.0), (0.14, 0.4), (0.3, 0.46), (1.0, 0.42)),
    color_gradient=fade((0.0, 0.0), (0.06, 1.0), (0.6, 0.85), (1.0, 0.0))), 9)
define("RogueMarkMotes", shot(**ADD,
    max_particles=1024, lifetime=[0.45, 0.65], texture=SPARKLE, render_mode="camera_facing", shape="sphere",
    direction="outward", start_speed=[1.5, 3.0], drag=4.0, acceleration=[0.0, 2.5, 0.0], start_rotation=[0, 90],
    spin_speed=[90, 270], random_spin_direction=True, color_intensity=4.0,
    size_over_life=sizes((0.0, 0.0), (0.15, 0.16, 0.24), (1.0, 0.04, 0.06)),
    color_gradient=fade((0.0, 0.0), (0.15, 1.0), (1.0, 0.0))), 8)
# The mark's reticle over the head, refreshed (see the header): fixed rotation, so old and new line up.
REFRESHED = fade((0.0, 0.0), (0.17, 1.0), (0.83, 1.0), (1.0, 0.0))
define("RogueMarkReticle", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=RETICLE, render_mode="camera_facing", start_rotation=[0, 0], color_intensity=2.4,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=REFRESHED), 7)
# Death Sentence: a thin rune circle hugging each reticle (low intensity: line art, never a filled disc).
define("RogueSentenceSigil", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=RUNE, render_mode="camera_facing", start_rotation=[0, 0], color_intensity=1.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=REFRESHED), 6)
# A crimson shaft on the condemned, standing on the ground (pivot at its foot).
define("RogueSentenceBeam", shot(**ADD,
    max_particles=64, lifetime=[0.4, 0.4], texture=SHAFT, render_mode="vertical_billboard", pivot=[0.5, 1.0],
    size_axes=[0.4, 1.8, 1.0], color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.1, 1.0), (1.0, 0.6)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)
# The shadow clone: a dark shade standing behind the mark (untinted) with a violet rim (tinted); scripted
# size = height, lifetime = until it stabs.
define("RogueCloneShade", shot(**SCRIPTED,
    max_particles=256, texture=SHADE, render_mode="vertical_billboard", pivot=[0.5, 1.0], blend_mode="normal",
    size_axes=[1.0, 1.0, 1.0], size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 1.0)),
    color_gradient=keys((0.0, [0.16, 0.08, 0.24, 0.0]), (0.12, [0.15, 0.07, 0.23, 0.9]), (0.45, [0.14, 0.07, 0.22, 0.85]),
                        (1.0, [0.12, 0.06, 0.2, 0.0]))), 3)
define("RogueCloneRim", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=SHADE, render_mode="vertical_billboard", pivot=[0.5, 1.0], size_axes=[1.18, 1.07, 1.0],
    color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.4, 0.9), (1.0, 0.0))), 4)
define("RogueEyes", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=GLOW, render_mode="camera_facing", primitive="octagon", color_intensity=3.5,
    size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (0.8, 1.0), (1.0, 0.0)), color_gradient=fade((0.0, 1.0), (1.0, 1.0))), 12)
# The stab: a thrust streak from the clone through the mark, a crescent slash, crimson sparks.
define("RogueStabThrust", shot(**ADD, **SCRIPTED, **ALIGNED,
    max_particles=512, texture=STREAK, render_mode="billboard", color_intensity=4.0,
    size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.7)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 10)
define("RogueStabSlash", shot(**ADD,
    max_particles=512, lifetime=[0.16, 0.2], texture=CRESCENT, render_mode="camera_facing", start_rotation=[0, 360],
    color_intensity=2.6, size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.85)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.8), (1.0, 0.0))), 5)
define("RogueSentenceX", shot(**ADD,
    max_particles=256, lifetime=[0.3, 0.3], texture=SLASH_X, render_mode="camera_facing", start_rotation=[-10, 10],
    color_intensity=2.6, size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.92)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.9), (1.0, 0.0))), 7)
# The dark slash of Death Sentence: a near-black crescent (untinted) under a crimson edge (tinted), one diagonal.
define("RogueDarkSlash", shot(
    max_particles=256, lifetime=[0.4, 0.4], texture=CRESCENT, render_mode="camera_facing", blend_mode="normal",
    start_rotation=[-35, -35], size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.95)),
    color_gradient=keys((0.0, [0.06, 0.02, 0.08, 0.0]), (0.06, [0.05, 0.02, 0.07, 0.95]), (0.6, [0.05, 0.02, 0.07, 0.85]),
                        (1.0, [0.05, 0.02, 0.07, 0.0]))), 5)
define("RogueCrimsonSlash", shot(**ADD,
    max_particles=256, lifetime=[0.3, 0.3], texture=CRESCENT, render_mode="camera_facing", start_rotation=[-35, -35],
    color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.95)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.8), (1.0, 0.0))), 6)
# The execution: a rune circle slams onto the condemned while a crimson stroke falls from above.
define("RogueExecSigil", shot(**ADD,
    max_particles=128, lifetime=[0.65, 0.65], texture=RUNE, render_mode="camera_facing", start_rotation=[0, 360],
    spin_speed=[40, 80], random_spin_direction=True, color_intensity=2.2,
    size_over_life=sizes((0.0, 1.0), (0.12, 0.55), (1.0, 0.62)),
    color_gradient=fade((0.0, 0.0), (0.05, 1.0), (0.6, 0.8), (1.0, 0.0))), 8)
define("RogueExecStroke", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=EMBER, render_mode="stretched", stretch_length_scale=1.0, stretch_speed_scale=0.12,
    color_intensity=4.0, size_over_life=sizes((0.0, 1.0), (1.0, 0.6)), color_gradient=fade((0.0, 1.0), (0.7, 1.0), (1.0, 0.0))), 11)
# A dark splat left under the executed (untinted, dark: never the red-orange of enemy telegraphs).
define("RogueExecSplat", shot(**DECAL, **SCRIPTED,
    max_particles=128, texture=SPLAT, color_intensity=1.0, size_over_life=sizes((0.0, 0.0), (0.08, 0.95), (1.0, 1.0)),
    color_gradient=keys((0.0, [0.1, 0.03, 0.08, 0.0]), (0.05, [0.1, 0.03, 0.08, 0.8]), (0.7, [0.09, 0.03, 0.07, 0.6]),
                        (1.0, [0.09, 0.03, 0.07, 0.0]))), -20)

# ------------------------------------------------------------------------------------------- smoke roll / momentum
# The smoke bomb and its cloud. Lit smoke takes the moor's dim light like the ground does, so its albedo is kept well
# above the ground's (pale grey-violet) or it vanishes against it; soft fade is short because the smoke hugs the
# ground, and it draws at full resolution. All untinted.
SMOKE_VOLUME = dict(SMOKE_LIT, soft_fade_distance=0.35)
SMOKE_PALE = [0.72, 0.7, 0.82]
SMOKE_DARK_RING = [0.2, 0.15, 0.27]
# Billows: born at chest height (the script sizes them), they climb 2-3 m and swell.
define("RogueSmokeBurst", shot(**SMOKE_VOLUME,
    max_particles=512, lifetime=[1.4, 2.0], shape="hemisphere", direction="outward", start_speed=[2.0, 4.0], drag=2.5,
    acceleration=[0.0, 0.4, 0.0], turbulence_strength=0.3, turbulence_frequency=0.5, turbulence_speed=0.4,
    size_over_life=sizes((0.0, 1.0), (0.3, 1.6, 1.9), (1.0, 2.2, 2.6)),
    color_gradient=keys((0.0, SMOKE_PALE + [0.0]), (0.06, SMOKE_PALE + [0.85]), (0.5, [0.68, 0.66, 0.78, 0.6]),
                        (1.0, [0.64, 0.62, 0.74, 0.0]))), 1)
# Dark puff ring: unlit shadow smoke rolling out over the ground to the cloud's 3 m edge (speed / drag ~ 3 m).
define("RogueSmokeRing", shot(**SMOKE_DARK,
    max_particles=512, lifetime=[0.9, 1.3], shape="circle", direction="outward", shape_position=[0.0, 0.25, 0.0],
    start_speed=[8.0, 10.5], drag=3.0, size_over_life=sizes((0.0, 1.0), (1.0, 1.9, 2.3)),
    color_gradient=keys((0.0, SMOKE_DARK_RING + [0.0]), (0.08, SMOKE_DARK_RING + [0.75]), (0.6, [0.18, 0.14, 0.24, 0.55]),
                        (1.0, [0.16, 0.12, 0.22, 0.0]))), 2)
# The lingering cloud: wisps the kit sprinkles over the whole radius at knee to chest height for the cloud's life;
# thin (peak alpha 0.32) so enemies inside still read through the overlap.
define("RogueSmokeCloud", shot(**SMOKE_VOLUME,
    max_particles=512, lifetime=[1.0, 1.4], direction="up", direction_spread=70.0, start_speed=[0.2, 0.5], drag=1.0,
    acceleration=[0.0, 0.25, 0.0], turbulence_strength=0.4, turbulence_frequency=0.6, turbulence_speed=0.4,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.6, 1.9)),
    color_gradient=keys((0.0, SMOKE_PALE + [0.0]), (0.25, SMOKE_PALE + [0.32]), (0.7, [0.68, 0.66, 0.78, 0.26]),
                        (1.0, [0.64, 0.62, 0.74, 0.0]))), 1)
# Momentum: wind streaks streaming off the rogue (tinted steel, alpha by stacks).
define("RogueSpeedStreak", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=EMBER, render_mode="stretched", stretch_length_scale=1.0, stretch_speed_scale=0.1,
    color_intensity=2.2, size_over_life=sizes((0.0, 1.0), (1.0, 0.5)), color_gradient=fade((0.0, 0.0), (0.2, 0.7), (1.0, 0.0))), 3)
# Full Momentum: violet shadow wisps curling up from the rogue's feet.
define("RogueMomentumWisp", shot(**WISPS, **ADD,
    max_particles=512, lifetime=[0.45, 0.6], render_mode="camera_facing", start_rotation=[0, 360], spin_speed=[60, 180],
    random_spin_direction=True, direction="up", direction_spread=30.0, start_speed=[0.4, 0.9], drag=1.0,
    color_intensity=1.8, size_over_life=sizes((0.0, 0.25, 0.35), (1.0, 0.45, 0.6)),
    color_gradient=fade((0.0, 0.0), (0.2, 0.8), (1.0, 0.0))), 2)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_rogue.py")
