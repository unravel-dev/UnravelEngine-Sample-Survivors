"""Mage class weapons of the class update (group MageFx2): Arc Lash and Tempest Crown, Prismatic Ray and Aurora Lance,
thermal shock, and the Arcane Ward look (src/Scripts/Weapons/Mage/ArcLash.cs, PrismaticRay.cs, Kits/MageKit.cs).

    python build_effects_spells_mage_v2.py                 list the effects defined here (no editor call)
    python build_effects_spells_mage_v2.py build [group]   create missing effects and (re)apply their settings

Conventions (see build_effects_spells.py): tinted emitters keep neutral gradients so the script's colour sets the hue;
sized emitters start their size curve at 1 (birth = the script's size) or at 0 peaking at 1; emitters the scripts fire
with a lifetime of their own keep lifetime [1, 1].
Strands and beams are STRETCHED quads whose length is their speed (stretch_length_scale 0, stretch_speed_scale
STRETCH): the script emits one at the middle of its segment with velocity = segment / STRETCH (a slow drift along it)
and size = its width, so the quad lies along the real 3D segment and turns its face to the camera.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import P, V, WHITE  # noqa: E402,F401
from build_effects_spells import A, K, W, ADD, SCRIPTED, light, fade  # noqa: E402
from effect_helpers import Registry, shot, sizes, scalar, keys  # noqa: E402

GROUPS = ("MageFx2",)
REGISTRY = Registry(GROUPS)
define = REGISTRY.define

# ArcLash.STRETCH and PrismaticRay.STRETCH: a strand's length (m) is its speed (m/s) x STRETCH.
STRETCH = 20.0


def sheet(path, tiles):
    """A texture atlas with a random tile per particle."""
    return {"texture": path, "texture_sheet_tiles": tiles, "texture_sheet_randomize": True}


LENGTH_BY_SPEED = {"render_mode": "stretched", "stretch_length_scale": 0.0, "stretch_speed_scale": STRETCH}
BRANCHING_BOLTS = sheet(f"{P}/bolt_4x1.png", [4, 1])
BEAMS = sheet(f"{V}/beam_4x1.png", [4, 1])
SPARK_WEBS = sheet(f"{A}/sparks_2x2.png", [2, 2])
STARS = sheet(f"{A}/stars_4x2.png", [4, 2])
CRYSTALS = sheet(f"{V}/crystal_shard_2x2.png", [2, 2])
STEAM = sheet(f"{A}/smoke_4x2.png", [4, 2])
# Flat quads the script turns: align_to_direction lays the texture's top along the (tiny) launch velocity.
TURNED_DECAL = {"render_mode": "horizontal_billboard", "align_to_direction": True, "start_rotation": [0, 0]}
EXACT = sizes((0.0, 1.0), (1.0, 1.0))
FLICKER = scalar((0.0, 1.5), (0.3, 0.8), (0.55, 1.3), (0.8, 0.7), (1.0, 0.5))
CRACKLE = scalar((0.0, 1.4), (0.2, 0.35), (0.4, 1.2), (0.6, 0.25), (0.8, 1.0), (1.0, 0.0))

# ----------------------------------------------------------------------------------------------------- arc lash
# Bolt strands: branching lightning, two or three per segment (random tiles), thinning as they fade.
define("MageFx2", "MageArcBolt", shot(**ADD, **SCRIPTED, **LENGTH_BY_SPEED, **BRANCHING_BOLTS,
    max_particles=4096, color_intensity=3.2, size_over_life=sizes((0.0, 1.0), (1.0, 0.7)), intensity_over_life=FLICKER,
    color_gradient=fade((0.0, 1.0), (0.7, 0.9), (1.0, 0.0))), 9)
# The soft tube of light around each segment.
define("MageFx2", "MageArcGlow", shot(**ADD, **SCRIPTED, **LENGTH_BY_SPEED,
    max_particles=2048, texture=f"{P}/glow.png", primitive="octagon", color_intensity=1.8, size_over_life=sizes((0.0, 1.0), (1.0, 0.8)),
    intensity_over_life=FLICKER, color_gradient=fade((0.0, 0.8), (1.0, 0.0))), 8)
# One short flash per strike; the brightest light the moor.
define("MageFx2", "MageArcFlash", shot(**ADD, **light(4, 14.0, 5.0),
    max_particles=512, lifetime=[0.1, 0.1], texture=f"{P}/glow.png", render_mode="camera_facing", primitive="octagon",
    color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.75)), color_gradient=fade((0.0, 1.0), (0.4, 0.7), (1.0, 0.0))), 12)
# Crackling webs around the struck body.
define("MageFx2", "MageArcWeb", shot(**ADD, **SPARK_WEBS,
    max_particles=1024, lifetime=[0.1, 0.14], render_mode="camera_facing", start_rotation=[0, 360], color_intensity=3.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.15)), intensity_over_life=scalar((0.0, 1.4), (0.35, 0.5), (0.6, 1.2), (1.0, 0.4)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 10)
# Radial sparks thrown off a strike.
define("MageFx2", "MageArcSparks", shot(**ADD,
    max_particles=2048, lifetime=[0.18, 0.35], texture=f"{P}/spark.png", render_mode="stretched", shape="sphere",
    direction="outward", start_speed=[5.0, 11.0], drag=3.0, gravity_scale=0.8, color_intensity=5.0, stretch_length_scale=1.0,
    stretch_speed_scale=0.05, size_over_life=sizes((0.0, 0.06, 0.1), (1.0, 0.02, 0.03)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)
# Static clinging to the body for a moment after the strike (the aftermath).
define("MageFx2", "MageArcCling", shot(**ADD, **SPARK_WEBS,
    max_particles=1024, lifetime=[0.22, 0.4], render_mode="camera_facing", shape="sphere", shape_scale=[0.4, 0.5, 0.4],
    start_rotation=[0, 360], color_intensity=2.6, size_over_life=sizes((0.0, 0.35, 0.6), (1.0, 0.4, 0.7)),
    intensity_over_life=CRACKLE, color_gradient=fade((0.0, 1.0), (1.0, 0.6))), 10)
# Static crawling over the ground under a strike.
define("MageFx2", "MageArcStatic", shot(**ADD, **SPARK_WEBS,
    max_particles=1024, lifetime=[0.16, 0.24], render_mode="horizontal_billboard", start_rotation=[0, 360], color_intensity=2.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.1)), intensity_over_life=scalar((0.0, 1.3), (0.5, 0.6), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.8), (1.0, 0.0))), -7)
# A frozen target shatters: ice shards streaking outward (with IceShatter debris).
define("MageFx2", "MageArcShatter", shot(**ADD, **CRYSTALS,
    max_particles=1024, lifetime=[0.3, 0.5], render_mode="stretched", shape="sphere", direction="outward", start_speed=[4.0, 9.0],
    drag=2.5, gravity_scale=1.2, color_intensity=2.2, stretch_length_scale=1.2, stretch_speed_scale=0.03,
    size_over_life=sizes((0.0, 0.18, 0.3), (1.0, 0.08, 0.14)), color_gradient=fade((0.0, 1.0), (0.7, 0.85), (1.0, 0.0))), 9)
# Tempest Crown: a flare where a bolt leaves the crown or forks at a frozen or burning enemy.
define("MageFx2", "MageArcNode", shot(**ADD,
    max_particles=512, lifetime=[0.14, 0.14], texture=f"{K}/star_07.png", render_mode="camera_facing", start_rotation=[0, 90],
    spin_speed=[200, 400], random_spin_direction=True, color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.5)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)
# Tempest Crown: the crown's points circling the head (one per point per frame: they leave short comet tails).
define("MageFx2", "MageCrownSpark", shot(**ADD,
    max_particles=1024, lifetime=[0.1, 0.1], texture=f"{K}/star_04.png", render_mode="camera_facing", start_rotation=[0, 90],
    spin_speed=[200, 400], random_spin_direction=True, color_intensity=3.2, size_over_life=sizes((0.0, 1.0), (1.0, 0.4)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)
# Tempest Crown: the crown's band of light (EmitShape each frame for a frame or two); it glows on the mage.
define("MageFx2", "MageCrownHalo", shot(**ADD, **SCRIPTED, **light(1, 8.0, 3.5),
    max_particles=128, texture=f"{V}/ring_energy.png", render_mode="horizontal_billboard", color_intensity=1.2,
    size_over_life=EXACT, color_gradient=fade((0.0, 0.8), (1.0, 0.0))), 6)

# ------------------------------------------------------------------------------------------------ prismatic ray
# The beam, re-emitted every frame for a few frames so a sweeping beam smears into a short fan: a white-hot core,
# a soft glow, and two dispersed fringes (warm on the leading side, cold on the trailing side) spreading toward the
# tip like light through a prism.
define("MageFx2", "MagePrismCore", shot(**ADD, **SCRIPTED, **LENGTH_BY_SPEED, **BEAMS,
    max_particles=1024, color_intensity=3.0, size_over_life=sizes((0.0, 1.0), (1.0, 0.45)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.8), (1.0, 0.0))), 10)
define("MageFx2", "MagePrismGlow", shot(**ADD, **SCRIPTED, **LENGTH_BY_SPEED,
    max_particles=1024, texture=f"{P}/glow.png", primitive="octagon", color_intensity=1.1, size_over_life=sizes((0.0, 1.0), (1.0, 0.8)),
    color_gradient=fade((0.0, 0.6), (1.0, 0.0))), 8)
define("MageFx2", "MagePrismFringe", shot(**ADD, **SCRIPTED, **LENGTH_BY_SPEED, **BEAMS,
    max_particles=2048, color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 0.6)),
    color_gradient=fade((0.0, 0.9), (1.0, 0.0))), 9)
# Glitter shed along the beam in every colour of the spectrum (tinted per particle), drifting and twinkling out.
define("MageFx2", "MagePrismSparkle", shot(**ADD, **STARS,
    max_particles=2048, lifetime=[0.35, 0.7], render_mode="camera_facing", shape="sphere", direction="outward",
    start_speed=[0.3, 1.2], drag=1.5, gravity_scale=-0.15, start_rotation=[0, 360], spin_speed=[90, 300], random_spin_direction=True,
    color_intensity=3.0, size_over_life=sizes((0.0, 0.1, 0.22), (0.2, 0.14, 0.26), (1.0, 0.0, 0.02)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (1.0, 0.0))), 11)
# The flare at the beam's tip; its light sweeps over the moor with the beam.
define("MageFx2", "MagePrismTip", shot(**ADD, **light(3, 12.0, 5.0),
    max_particles=512, lifetime=[0.06, 0.06], texture=f"{K}/star_08.png", render_mode="camera_facing", start_rotation=[0, 360],
    color_intensity=2.4, size_over_life=sizes((0.0, 1.0), (1.0, 0.7)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)
# The prism at the mage's chest the beams leave from.
define("MageFx2", "MagePrismSource", shot(**ADD,
    max_particles=256, lifetime=[0.08, 0.08], texture=f"{K}/magic_05.png", render_mode="camera_facing", start_rotation=[0, 90],
    spin_speed=[90, 180], random_spin_direction=True, color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 0.8)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)
# A faint streak of light on the ground under the beam, fading behind the sweep.
define("MageFx2", "MagePrismTrace", shot(**ADD, **SCRIPTED, **TURNED_DECAL, **BEAMS,
    max_particles=1024, size_axes=[0.06, 1.0, 1.0], color_intensity=1.0, size_over_life=EXACT,
    color_gradient=fade((0.0, 0.4), (1.0, 0.0))), -8)
# A scorched glint where the beam crosses an enemy.
define("MageFx2", "MagePrismGlint", shot(**ADD,
    max_particles=1024, lifetime=[0.16, 0.22], texture=f"{K}/star_06.png", render_mode="camera_facing", start_rotation=[0, 90],
    spin_speed=[120, 260], random_spin_direction=True, color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.3)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)
# The prism flaring as the beams ignite (and fading when they end).
define("MageFx2", "MagePrismFlash", shot(**ADD, **light(2, 18.0, 7.0),
    max_particles=128, lifetime=[0.18, 0.18], texture=f"{K}/magic_05.png", render_mode="camera_facing", start_rotation=[0, 90],
    color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.8)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)

# ------------------------------------------------------------------------------------------------ thermal shock
# Steam, ice and fire: one flash with light, fire tongues (FireBall), ice spikes (FrostSpikes) and debris (IceShatter),
# a hissing plume of steam, and two rings (warm and cold).
define("MageFx2", "MageShockFlash", shot(**ADD, **light(4, 20.0, 6.0),
    max_particles=256, lifetime=[0.12, 0.12], texture=f"{P}/glow.png", render_mode="camera_facing", primitive="octagon",
    color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.12, 0.9), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (0.35, 0.7), (1.0, 0.0))), 12)
define("MageFx2", "MageShockSteam", shot(**STEAM,
    max_particles=1024, lifetime=[0.7, 1.1], render_mode="billboard", blend_mode="normal", lighting="unlit", soft_fade_distance=0.6,
    shape="sphere", direction="outward", start_speed=[1.0, 2.5], drag=2.0, acceleration=[0.0, 3.5, 0.0], start_rotation=[0, 360],
    spin_speed=[20, 60], random_spin_direction=True, size_over_life=sizes((0.0, 0.35, 0.5), (1.0, 1.0, 1.4)),
    color_gradient=keys((0.0, [0.78, 0.84, 0.9, 0.0]), (0.1, [0.8, 0.86, 0.92, 0.38]), (0.5, [0.72, 0.78, 0.84, 0.2]),
                        (1.0, [0.6, 0.65, 0.7, 0.0]))), 2)
define("MageFx2", "MageShockRing", shot(**ADD,
    max_particles=512, lifetime=[0.35, 0.35], texture=f"{W}/shock_ring.png", render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.6, size_over_life=sizes((0.0, 0.0), (0.05, 0.3), (0.4, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 3)

# ------------------------------------------------------------------------------------------------- aurora lance
# Two strands of motes twisting around every beam (one point per strand step per frame: short spiralling trails).
define("MageFx2", "MageAuroraMote", shot(**ADD,
    max_particles=4096, lifetime=[0.14, 0.14], texture=f"{P}/glow.png", render_mode="camera_facing", primitive="octagon",
    color_intensity=3.0, size_over_life=sizes((0.0, 1.0), (1.0, 0.3)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)
# Curtains of aurora light standing at the beams' tips: as the beams turn they draw a glowing wall around the mage.
define("MageFx2", "MageAuroraVeil", shot(**ADD,
    max_particles=2048, lifetime=[0.45, 0.45], texture=f"{P}/light_shaft.png", render_mode="vertical_billboard", pivot=[0.5, 1.0],
    size_axes=[0.25, 1.0, 1.0], color_intensity=1.2, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.9)),
    color_gradient=fade((0.0, 0.0), (0.12, 0.4), (1.0, 0.0))), 4)

# -------------------------------------------------------------------------------------------------- arcane ward
# While Blink's ward holds: a faint shell around the mage, one mote circling her per charge left, and a rune circle
# turning under her feet (re-emitted every frame for a frame or two, so they follow her). Low intensity: she stays
# visible.
define("MageFx2", "MageWardShell", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{V}/ring_energy.png", render_mode="camera_facing", color_intensity=1.0, size_over_life=EXACT,
    color_gradient=fade((0.0, 0.45), (1.0, 0.0))), 5)
define("MageFx2", "MageWardMote", shot(**ADD,
    max_particles=256, lifetime=[0.12, 0.12], texture=f"{K}/magic_04.png", render_mode="camera_facing", start_rotation=[0, 90],
    spin_speed=[120, 240], random_spin_direction=True, color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 0.35)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 11)
define("MageFx2", "MageWardGlyph", shot(**ADD, **SCRIPTED, **TURNED_DECAL,
    max_particles=128, texture=f"{K}/magic_01.png", color_intensity=1.0, size_over_life=EXACT,
    color_gradient=fade((0.0, 0.6), (1.0, 0.0))), -9)
# A ward charge absorbs a hit: violet crystal shards burst off the shell and a ring of force flares out.
define("MageFx2", "MageWardShards", shot(**ADD, **CRYSTALS,
    max_particles=512, lifetime=[0.35, 0.6], render_mode="stretched", shape="sphere", direction="outward", start_speed=[6.0, 11.0],
    drag=3.0, gravity_scale=0.5, color_intensity=2.4, stretch_length_scale=1.2, stretch_speed_scale=0.03,
    size_over_life=sizes((0.0, 0.22, 0.36), (1.0, 0.08, 0.14)), color_gradient=fade((0.0, 1.0), (0.7, 0.85), (1.0, 0.0))), 10)
define("MageFx2", "MageWardBurst", shot(**ADD,
    max_particles=128, lifetime=[0.3, 0.3], texture=f"{V}/ring_energy.png", render_mode="camera_facing", color_intensity=2.6,
    size_over_life=sizes((0.0, 0.0), (0.05, 0.3), (0.35, 0.85), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 9)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_mage_v2.py")
