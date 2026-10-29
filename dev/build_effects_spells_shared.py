"""Shared weapon effects, second pass: layered impacts, particle projectile heads and path trails for the seven shared
weapons and their evolutions (src/Scripts/Weapons/Shared/**).

    python build_effects_spells_shared.py                 list the effects defined here (no editor call)
    python build_effects_spells_shared.py build [group]   create missing effects and (re)apply their settings

Conventions (see build_effects_spells.py): tinted emitters keep neutral gradients so each weapon's colour sets the
hue; sized emitters start their size curve at 1 (birth = the script's size) or at 0 peaking at 1; emitters the
scripts fire with a lifetime of their own (EmitShape, EmitMoving with a lifetime) keep lifetime [1, 1].
Path trails (render_mode trail) are ONE particle per projectile launched with the projectile's own velocity and
gravity (EmitMoving), so the ribbon draws its exact path and dies when it lands.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import P, V, WHITE  # noqa: E402
from build_effects_spells import A, K, W, ADD, SMOKE_LIT, SCRIPTED, GROUND, light, fade  # noqa: E402
from effect_helpers import Registry, shot, sizes, scalar, keys  # noqa: E402

GROUPS = ("SharedFx",)
REGISTRY = Registry(GROUPS)
define = REGISTRY.define

# Fire Flask lobs at 25 m/s^2 (FireFlask.GRAVITY): its path trail falls with the same gravity.
STANDARD_GRAVITY = 9.81
FLASK_GRAVITY_SCALE = round(25.0 / STANDARD_GRAVITY, 4)


def sheet(path, tiles):
    """A texture atlas with a random tile per particle."""
    return {"texture": path, "texture_sheet_tiles": tiles, "texture_sheet_randomize": True}


STARS = sheet(f"{A}/stars_4x2.png", [4, 2])
SPARK_WEBS = sheet(f"{A}/sparks_2x2.png", [2, 2])
BOLTS = sheet(f"{A}/bolts_4x1.png", [4, 1])
TWIRLS = sheet(f"{A}/twirls_2x2.png", [2, 2])
SLASHES = sheet(f"{A}/slashes_2x2.png", [2, 2])
WISPS = sheet(f"{V}/wisp_2x2.png", [2, 2])
SPIKES = sheet(f"{W}/rock_spike_2x2.png", [2, 2])
CRYSTALS = sheet(f"{V}/crystal_shard_2x2.png", [2, 2])
TRACES = sheet(f"{A}/traces_4x2.png", [4, 2])
# Licking flames: the white fire flipbook played once per flame.
FIRE_FLIPBOOK = {"texture": f"{W}/fire_8x8_white.png", "texture_sheet_tiles": [8, 8], "texture_sheet_cycles": 1.0,
                 "frame_blending": True}
UPRIGHT = {"render_mode": "vertical_billboard", "pivot": [0.5, 1.0]}
# Ground rings the script turns: align_to_direction on a flat quad faces its top along the (tiny) launch velocity,
# so the script sets each copy's angle and the ring turns smoothly while copies follow the hero.
TURNED_DECAL = {"render_mode": "horizontal_billboard", "align_to_direction": True, "start_rotation": [0, 0]}
FLICKER = scalar((0.0, 1.5), (0.3, 0.8), (0.55, 1.3), (0.8, 0.7), (1.0, 0.5))
EXACT = sizes((0.0, 1.0), (1.0, 1.0))
FIRE_COOLING = keys((0.0, WHITE + [0.0]), (0.12, WHITE + [1.0]), (0.6, [0.8, 0.8, 0.8, 0.7]), (1.0, [0.5, 0.5, 0.5, 0.0]))

# ------------------------------------------------------------------------------------------------------- heads
# Fired every frame per projectile with EmitSized (ProjectileWorld.EmitHeads): birth = head size, the curve shrinks.
# Spirit dart tail: wispy crescents that spin and fade behind the dart into a ghostly comet.
define("SharedFx", "SharedHeadWisp", shot(**ADD, **WISPS,
    max_particles=2048, lifetime=[0.2, 0.24], render_mode="camera_facing", start_rotation=[0, 360], spin_speed=[200, 420],
    random_spin_direction=True, color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 0.25)),
    color_gradient=fade((0.0, 0.85), (1.0, 0.0))), 6)
# Disc blade blur: a flat swirl spinning with the disc, smeared behind it.
define("SharedFx", "SharedHeadWhirl", shot(**ADD, **TWIRLS,
    max_particles=512, lifetime=[0.09, 0.09], render_mode="horizontal_billboard", start_rotation=[0, 360],
    spin_speed=[720, 900], color_intensity=2.4, size_over_life=sizes((0.0, 1.0), (1.0, 0.85)),
    color_gradient=fade((0.0, 0.9), (1.0, 0.0))), 5)
# Eclipse Disc: a black sun (untinted, normal blend) inside a burning violet ring.
define("SharedFx", "SharedHeadEclipseCore", shot(
    max_particles=256, lifetime=[0.06, 0.06], texture=f"{K}/circle_05.png", render_mode="camera_facing",
    blend_mode="normal", primitive="octagon", size_over_life=sizes((0.0, 1.0), (1.0, 0.7)),
    color_gradient=keys((0.0, [0.04, 0.0, 0.08, 0.95]), (1.0, [0.06, 0.02, 0.12, 0.0]))), 6)
define("SharedFx", "SharedHeadEclipseRing", shot(**ADD, **light(3, 8.0, 4.0),
    max_particles=256, lifetime=[0.07, 0.07], texture=f"{K}/circle_03.png", render_mode="camera_facing",
    primitive="octagon", start_rotation=[0, 360], color_intensity=2.6, size_over_life=sizes((0.0, 1.0), (1.0, 1.15)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 7)
# Wisp flame: a slender spirit flame licking upward; wisps light the ground around the hero as they circle.
define("SharedFx", "SharedHeadWispFlame", shot(**ADD, **light(4, 6.0, 3.5),
    max_particles=2048, lifetime=[0.16, 0.22], texture=f"{K}/flame_06.png", render_mode="camera_facing",
    direction="up", direction_spread=15.0, start_speed=[0.6, 1.1], start_rotation=[-12, 12], color_intensity=2.8,
    size_over_life=sizes((0.0, 1.0), (1.0, 0.3)), color_gradient=fade((0.0, 0.0), (0.15, 1.0), (1.0, 0.0))), 6)
# Hellfire Cask in flight: dark smoke rolling off the cask (untinted).
define("SharedFx", "SharedHeadSmoke", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.35, 0.45], direction="up", start_speed=[0.3, 0.6], size_over_life=sizes((0.0, 1.0), (1.0, 1.8)),
    color_gradient=keys((0.0, [0.14, 0.06, 0.05, 0.0]), (0.15, [0.12, 0.06, 0.05, 0.55]), (1.0, [0.1, 0.08, 0.08, 0.0]))), 1)

# ----------------------------------------------------------------------------------------------- shared layers
# Tinted per weapon. Flash: one per impact, the brightest four light the moor.
define("SharedFx", "SharedFlash", shot(**ADD, **light(4, 16.0, 5.0),
    max_particles=256, lifetime=[0.13, 0.13], texture=f"{K}/star_06.png", render_mode="camera_facing", start_rotation=[0, 90],
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.55)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.75), (1.0, 0.0))), 12)
# Cast accents at the hero's hand (no light: fast weapons fire them often).
define("SharedFx", "SharedMuzzleStar", shot(**ADD,
    max_particles=256, lifetime=[0.14, 0.14], texture=f"{K}/star_07.png", render_mode="camera_facing", start_rotation=[0, 90],
    spin_speed=[150, 300], random_spin_direction=True, color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.35)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 9)
# A thin ring that pops outward (the disc's ping, dart pops).
define("SharedFx", "SharedPing", shot(**ADD,
    max_particles=256, lifetime=[0.2, 0.2], texture=f"{K}/circle_02.png", render_mode="camera_facing", primitive="octagon",
    color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.3, 0.8), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.6), (1.0, 0.0))), 11)
define("SharedFx", "SharedSparks", shot(**ADD,
    max_particles=2048, lifetime=[0.2, 0.35], texture=f"{P}/spark.png", render_mode="stretched", shape="sphere",
    direction="outward", start_speed=[6.0, 11.0], drag=4.5, gravity_scale=0.8, color_intensity=4.5,
    stretch_length_scale=1.0, stretch_speed_scale=0.05, size_over_life=sizes((0.0, 0.06, 0.1), (1.0, 0.02, 0.03)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.85), (1.0, 0.0))), 11)
define("SharedFx", "SharedGlints", shot(**ADD, **STARS,
    max_particles=2048, lifetime=[0.3, 0.55], render_mode="camera_facing", shape="sphere", direction="outward",
    start_speed=[2.5, 6.0], drag=3.5, start_rotation=[0, 360], spin_speed=[90, 300], random_spin_direction=True,
    color_intensity=2.8, size_over_life=sizes((0.0, 0.14, 0.26), (1.0, 0.0, 0.04)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.9), (1.0, 0.0))), 11)
# Lingering: pinpoint motes drifting up and wandering after the hit.
define("SharedFx", "SharedMotes", shot(**ADD,
    max_particles=2048, lifetime=[0.7, 1.2], texture=f"{K}/flare_01.png", render_mode="camera_facing", shape="sphere",
    direction="outward", start_speed=[0.6, 1.8], drag=2.0, acceleration=[0.0, 0.9, 0.0], turbulence_strength=0.7,
    color_intensity=4.0, size_over_life=sizes((0.0, 0.0), (0.15, 0.22, 0.34), (1.0, 0.0, 0.06)),
    color_gradient=fade((0.0, 0.0), (0.15, 1.0), (1.0, 0.0))), 10)
# A fast ring racing out over the ground (FxLayer.Ground, sized by the hit radius).
define("SharedFx", "SharedGroundRing", shot(**ADD,
    max_particles=256, lifetime=[0.4, 0.4], texture=f"{K}/circle_02.png", render_mode="horizontal_billboard",
    primitive="octagon", start_rotation=[0, 360], color_intensity=2.2,
    size_over_life=sizes((0.0, 0.0), (0.05, 0.3), (0.4, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.6), (1.0, 0.0))), -6)
# A flat swirl bursting open (disc throw, wisp splash).
define("SharedFx", "SharedTwirl", shot(**ADD, **TWIRLS,
    max_particles=256, lifetime=[0.25, 0.25], render_mode="horizontal_billboard", start_rotation=[0, 360],
    spin_speed=[540, 720], color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.25, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 8)
# A ghost flame rising out of the hit (Soul Hail kills, wisp splashes).
define("SharedFx", "SharedRise", shot(**ADD,
    max_particles=512, lifetime=[0.8, 1.1], texture=f"{K}/flame_05.png", render_mode="camera_facing", direction="up",
    direction_spread=12.0, start_speed=[1.6, 2.6], drag=1.2, turbulence_strength=0.3, start_rotation=[-8, 8],
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.5)),
    color_gradient=fade((0.0, 0.0), (0.15, 0.9), (0.6, 0.6), (1.0, 0.0))), 9)
# Earthy dust kicked up by heavy hits (untinted).
define("SharedFx", "SharedDust", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.6, 1.0], shape="hemisphere", direction="outward", start_speed=[2.0, 4.5], drag=4.0,
    acceleration=[0.0, 0.4, 0.0], size_over_life=sizes((0.0, 0.35, 0.5), (1.0, 0.9, 1.2)),
    color_gradient=keys((0.0, [0.5, 0.47, 0.42, 0.0]), (0.12, [0.48, 0.45, 0.4, 0.5]), (1.0, [0.45, 0.43, 0.4, 0.0]))), 1)
# A column of light standing on the ground (Godspear detonation, Wisp Choir splashes).
define("SharedFx", "SharedHolyPillar", shot(**ADD, **UPRIGHT,
    max_particles=64, lifetime=[0.45, 0.45], texture=f"{P}/light_shaft.png", color_intensity=3.0, size_axes=[0.35, 2.6, 1.0],
    size_over_life=sizes((0.0, 0.0), (0.1, 1.0), (1.0, 0.75)), color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 12)

# ------------------------------------------------------------------------------------------------ spirit darts
# Soul Hail: a soul circle under the hero while the stream fires (EmitShape, re-emitted so it follows the hero).
define("SharedFx", "SharedSoulSigil", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{K}/magic_02.png", render_mode="horizontal_billboard", start_rotation=[0, 0],
    color_intensity=1.6, size_over_life=EXACT, color_gradient=fade((0.0, 0.0), (0.3, 0.45), (0.7, 0.45), (1.0, 0.0))), -9)
define("SharedFx", "SharedSoulShards", shot(**ADD, **CRYSTALS,
    max_particles=1024, lifetime=[0.3, 0.45], render_mode="stretched", shape="sphere", direction="outward",
    start_speed=[6.0, 10.0], drag=4.0, gravity_scale=0.6, color_intensity=2.4, stretch_length_scale=1.0,
    stretch_speed_scale=0.03, size_over_life=sizes((0.0, 0.18, 0.28), (1.0, 0.06, 0.1)),
    color_gradient=fade((0.0, 1.0), (0.7, 0.8), (1.0, 0.0))), 10)

# ------------------------------------------------------------------------------------------------ ricochet disc
define("SharedFx", "SharedDiscSlash", shot(**ADD, **SLASHES,
    max_particles=256, lifetime=[0.16, 0.16], render_mode="camera_facing", start_rotation=[0, 360], color_intensity=2.6,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.2)), color_gradient=fade((0.0, 1.0), (0.5, 0.8), (1.0, 0.0))), 11)
# Eclipse: a dark ring swallowing the hit (untinted, normal blend under the sparks).
define("SharedFx", "SharedEclipseVoid", shot(
    max_particles=128, lifetime=[0.32, 0.32], texture=f"{K}/circle_04.png", render_mode="camera_facing", blend_mode="normal",
    primitive="octagon", start_rotation=[0, 360], size_over_life=sizes((0.0, 0.0), (0.35, 0.85), (1.0, 1.0)),
    color_gradient=keys((0.0, [0.05, 0.0, 0.1, 0.0]), (0.15, [0.06, 0.01, 0.12, 0.85]), (1.0, [0.08, 0.02, 0.15, 0.0]))), 10)

# ------------------------------------------------------------------------------------------------- bramble halo
# The bramble itself (the ring mesh is hidden): dark thorny vines on the ground, two rings turning opposite ways,
# re-emitted every few frames (EmitMoving: angle from the velocity, diameter and lifetime from the script).
define("SharedFx", "SharedBrambleVine", shot(**TURNED_DECAL, **SCRIPTED,
    max_particles=256, texture=f"{W}/thorn_ring.png", blend_mode="normal", size_over_life=EXACT,
    color_gradient=fade((0.0, 0.0), (0.3, 0.85), (0.7, 0.85), (1.0, 0.0))), -10)
# A soft green band under the vines so they read at night (EmitShape, like the vines).
define("SharedFx", "SharedBrambleGlow", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{K}/circle_04.png", render_mode="horizontal_billboard", primitive="octagon",
    color_intensity=1.2, size_over_life=EXACT, color_gradient=fade((0.0, 0.0), (0.3, 0.35), (0.7, 0.35), (1.0, 0.0))), -12)
# Thorns lashing up along the ring (sized, standing on the ground, unlit so they read at night).
define("SharedFx", "SharedBrambleSpike", shot(**SPIKES, **UPRIGHT,
    max_particles=512, lifetime=[0.45, 0.6], blend_mode="normal", start_rotation=[-22, 22],
    size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (0.7, 0.9), (1.0, 0.0)), color_gradient=fade((0.0, 1.0), (1.0, 1.0))), 2)
# Leaves whirled around the ring (EmitMoving: tangential velocity, size and lifetime from the script).
define("SharedFx", "SharedBrambleLeaf", shot(**SCRIPTED,
    max_particles=256, texture=f"{P}/shard.png", render_mode="camera_facing", blend_mode="normal", start_rotation=[0, 360],
    spin_speed=[200, 500], random_spin_direction=True, drag=1.2, gravity_scale=0.15, turbulence_strength=0.4,
    size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (0.8, 1.0), (1.0, 0.0)), color_gradient=fade((0.0, 1.0), (1.0, 1.0))), 2)
# Thorns stabbing up through an enemy the ring hurts.
define("SharedFx", "SharedThornStab", shot(**SPIKES,
    max_particles=1024, lifetime=[0.2, 0.3], render_mode="stretched", blend_mode="normal", shape="sphere", direction="up",
    direction_spread=35.0, start_speed=[5.0, 8.0], drag=6.0, stretch_length_scale=1.4, stretch_speed_scale=0.04,
    size_over_life=sizes((0.0, 0.16, 0.24), (1.0, 0.08, 0.12)), color_gradient=fade((0.0, 1.0), (0.7, 1.0), (1.0, 0.0))), 3)
# The ring tightening on a tick that hits (EmitShape, diameter and lifetime from the script).
define("SharedFx", "SharedBrambleWave", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{W}/thorn_ring.png", render_mode="horizontal_billboard", primitive="octagon",
    color_intensity=2.2, size_over_life=sizes((0.0, 1.0), (1.0, 0.85)), color_gradient=fade((0.0, 0.0), (0.15, 0.9), (1.0, 0.0))), -9)
# Briar Sanctum: a sanctum circle filling the halo (EmitShape like the aura, fixed orientation so copies match).
define("SharedFx", "SharedBriarSigil", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{P}/rune_circle.png", render_mode="horizontal_billboard", start_rotation=[0, 0],
    color_intensity=1.5, size_over_life=EXACT, color_gradient=fade((0.0, 0.0), (0.3, 0.4), (0.7, 0.4), (1.0, 0.0))), -11)
# Briar Sanctum: wild-rose petals drifting inside the halo (untinted, rose and pale blossom).
define("SharedFx", "SharedBriarPetals", shot(
    max_particles=256, lifetime=[1.2, 1.8], texture=f"{P}/shard.png", render_mode="camera_facing", blend_mode="normal",
    shape="sphere", direction="outward", start_speed=[0.2, 0.6], drag=0.8, gravity_scale=0.08, turbulence_strength=0.8,
    start_rotation=[0, 360], spin_speed=[90, 360], random_spin_direction=True, random_between_gradients=True,
    size_over_life=sizes((0.0, 0.1, 0.16), (1.0, 0.08, 0.12)),
    color_gradient=keys((0.0, [0.85, 0.45, 0.55, 0.0]), (0.15, [0.85, 0.45, 0.55, 1.0]), (0.8, [0.8, 0.42, 0.52, 1.0]),
                        (1.0, [0.8, 0.42, 0.52, 0.0])),
    color_gradient_b=keys((0.0, [0.95, 0.85, 0.8, 0.0]), (0.15, [0.95, 0.85, 0.8, 1.0]), (0.8, [0.9, 0.8, 0.75, 1.0]),
                          (1.0, [0.9, 0.8, 0.75, 0.0]))), 2)

# ----------------------------------------------------------------------------------------------- skewer javelin
# Path streaks: one trail particle per javelin, launched with its velocity and lifetime (straight, no gravity).
define("SharedFx", "SharedJavelinStreak", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{V}/trail_flame.png", render_mode="trail", trail_length=0.22, trail_width_end=0.1,
    color_intensity=3.0, size_over_life=EXACT, color_gradient=fade((0.0, 0.0), (0.04, 0.9), (0.9, 0.9), (1.0, 0.0))), 4)
# A bright streak across a skewered enemy along the flight line (EmitMoving: a slow drift along the javelin aligns it).
define("SharedFx", "SharedSkewerFlare", shot(**ADD, **SCRIPTED, **TRACES,
    max_particles=512, render_mode="billboard", align_to_direction=True, start_rotation=[0, 0], size_axes=[0.5, 1.0, 1.0],
    color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.8)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.8), (1.0, 0.0))), 11)
define("SharedFx", "SharedGodspearRay", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{V}/trail_flame.png", render_mode="trail", trail_length=0.28, trail_width_end=0.0,
    color_intensity=2.2, size_over_life=EXACT, color_gradient=fade((0.0, 0.0), (0.04, 0.7), (0.9, 0.7), (1.0, 0.0))), 3)
# Speed lines shooting forward off the throw (EmitMoving: velocity, size and lifetime from the script).
define("SharedFx", "SharedSpeedLines", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=f"{P}/spark.png", render_mode="stretched", drag=3.0, color_intensity=3.0,
    stretch_length_scale=1.0, stretch_speed_scale=0.06, size_over_life=sizes((0.0, 1.0), (1.0, 0.4)),
    color_gradient=fade((0.0, 0.9), (1.0, 0.0))), 9)
# Godspear: glowing cracks torn into the ground by the detonation.
define("SharedFx", "SharedGroundCracks", shot(**ADD,
    max_particles=64, lifetime=[1.2, 1.2], texture=f"{W}/crack_radial.png", render_mode="horizontal_billboard",
    start_rotation=[0, 360], color_intensity=2.4, size_over_life=sizes((0.0, 0.0), (0.06, 1.0), (1.0, 1.0)),
    intensity_over_life=scalar((0.0, 1.6), (0.3, 1.0), (1.0, 0.5)), color_gradient=fade((0.0, 1.0), (0.6, 0.7), (1.0, 0.0))), -9)

# --------------------------------------------------------------------------------------------------- fire flask
# Path streak of a lobbed flask: falls with the flask's gravity so the ribbon follows its arc.
define("SharedFx", "SharedFlaskArc", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{V}/trail_flame.png", render_mode="trail", trail_length=0.2, trail_width_end=0.1,
    gravity_scale=FLASK_GRAVITY_SCALE, color_intensity=2.6, size_over_life=EXACT,
    color_gradient=fade((0.0, 0.0), (0.05, 0.9), (0.92, 0.9), (1.0, 0.0))), 3)
# Glass shards of the shattered flask, bouncing on the ground (untinted).
define("SharedFx", "SharedFlaskGlass", shot(**CRYSTALS,
    max_particles=512, lifetime=[0.5, 0.8], render_mode="camera_facing", blend_mode="normal", lighting="surface",
    shape="hemisphere", direction="outward", start_speed=[3.0, 6.5], gravity_scale=2.0, collision="bounce",
    collision_planes=GROUND, restitution=0.3, start_rotation=[0, 360], spin_speed=[400, 900], random_spin_direction=True,
    size_over_life=sizes((0.0, 0.08, 0.16), (1.0, 0.06, 0.12)),
    color_gradient=keys((0.0, [0.75, 0.85, 0.7, 1.0]), (0.8, [0.7, 0.8, 0.65, 0.9]), (1.0, [0.7, 0.8, 0.65, 0.0]))), 5)
# Burning liquid flung out of the flask: droplets arcing down and cooling.
define("SharedFx", "SharedFlaskSplash", shot(**ADD,
    max_particles=1024, lifetime=[0.45, 0.7], texture=f"{P}/ember.png", render_mode="stretched", shape="hemisphere",
    direction="outward", start_speed=[4.0, 8.0], gravity_scale=1.8, drag=1.0, color_intensity=6.0,
    stretch_length_scale=1.0, stretch_speed_scale=0.05, size_over_life=sizes((0.0, 0.08, 0.13), (1.0, 0.03, 0.05)),
    intensity_over_life=scalar((0.0, 1.4), (0.5, 1.0), (1.0, 0.5)), color_gradient=fade((0.0, 1.0), (0.75, 0.9), (1.0, 0.0))), 11)
# Pool flames (the zone's particle): fire flipbook tongues standing on the ground, licking up and cooling; the
# brightest flames light the moor, so the pool's light flickers with its fire.
define("SharedFx", "SharedFlaskFire", shot(**ADD, **FIRE_FLIPBOOK, **light(4, 18.0, 4.5),
    max_particles=1024, lifetime=[0.5, 0.8], render_mode="camera_facing", pivot=[0.5, 1.0], direction="up",
    direction_spread=10.0, start_speed=[0.2, 0.5], start_rotation=[-8, 8], color_intensity=2.4, soft_fade_distance=0.25,
    size_over_life=sizes((0.0, 0.5, 0.75), (0.25, 0.9, 1.25), (1.0, 0.7, 1.0)), color_gradient=FIRE_COOLING), 0)
define("SharedFx", "SharedHellfireFire", shot(**ADD, **FIRE_FLIPBOOK, **light(4, 18.0, 5.0),
    max_particles=2048, lifetime=[0.55, 0.9], render_mode="camera_facing", pivot=[0.5, 1.0], direction="up",
    direction_spread=10.0, start_speed=[0.3, 0.7], start_rotation=[-8, 8], color_intensity=2.6, soft_fade_distance=0.25,
    size_over_life=sizes((0.0, 0.8, 1.1), (0.25, 1.4, 1.9), (1.0, 1.0, 1.4)), color_gradient=FIRE_COOLING), 0)
# Coals smouldering on the scorched ground of a pool (a few at a time, flickering).
define("SharedFx", "SharedFlaskCoals", shot(**ADD,
    max_particles=1024, lifetime=[0.8, 1.4], texture=f"{P}/ember.png", render_mode="horizontal_billboard",
    start_rotation=[0, 360], color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (0.2, 0.12, 0.22), (1.0, 0.06, 0.12)),
    intensity_over_life=scalar((0.0, 1.2), (0.3, 0.6), (0.5, 1.1), (0.75, 0.5), (1.0, 0.9)),
    color_gradient=fade((0.0, 0.0), (0.15, 1.0), (0.8, 0.8), (1.0, 0.0))), -7)
# Hellfire Cask: a column of fire erupting where each cask lands.
define("SharedFx", "SharedHellfirePillar", shot(**ADD, **UPRIGHT,
    max_particles=64, lifetime=[0.5, 0.6], texture=f"{K}/muzzle_01.png", start_rotation=[-8, 8], color_intensity=2.6,
    size_axes=[0.6, 1.0, 1.0], size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.85)),
    intensity_over_life=scalar((0.0, 1.5), (0.4, 1.0), (1.0, 0.6)), color_gradient=fade((0.0, 1.0), (0.6, 0.8), (1.0, 0.0))), 3)
# Hellfire Cask: a burning pentagon glyph under each pool (EmitShape for the pool's duration).
define("SharedFx", "SharedHellfireSigil", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{K}/magic_01.png", render_mode="horizontal_billboard", start_rotation=[0, 360],
    color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.06, 1.0), (1.0, 1.0)),
    intensity_over_life=scalar((0.0, 1.6), (0.15, 1.0), (1.0, 0.8)),
    color_gradient=fade((0.0, 0.0), (0.04, 0.85), (0.85, 0.6), (1.0, 0.0))), -9)
# Hellfire Cask: black smoke climbing from the pools (untinted).
define("SharedFx", "SharedHellSmoke", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[1.4, 2.0], shape="sphere", direction="up", direction_spread=25.0, start_speed=[1.0, 2.0],
    drag=0.8, acceleration=[0.0, 1.2, 0.0], turbulence_strength=0.6, size_over_life=sizes((0.0, 0.6, 0.9), (1.0, 1.8, 2.4)),
    color_gradient=keys((0.0, [0.12, 0.05, 0.04, 0.0]), (0.15, [0.1, 0.05, 0.04, 0.6]), (1.0, [0.08, 0.06, 0.06, 0.0]))), 1)

# -------------------------------------------------------------------------------------------------- storm totem
# Bolt strands: one lightning strand per segment (EmitMoving: a slow velocity along the segment aligns the strand,
# size = segment length, lifetime = the bolt's), over a soft glow tube.
define("SharedFx", "SharedStormBolt", shot(**ADD, **SCRIPTED, **BOLTS,
    max_particles=2048, render_mode="billboard", align_to_direction=True, start_rotation=[0, 0], size_axes=[1.0, 1.0, 1.0],
    color_intensity=3.5, size_over_life=EXACT, intensity_over_life=FLICKER,
    color_gradient=fade((0.0, 1.0), (0.75, 0.9), (1.0, 0.0))), 9)
define("SharedFx", "SharedStormBoltGlow", shot(**ADD, **SCRIPTED,
    max_particles=2048, texture=f"{P}/glow.png", render_mode="billboard", align_to_direction=True, start_rotation=[0, 0],
    primitive="octagon", size_axes=[0.45, 1.15, 1.0], color_intensity=1.4, size_over_life=EXACT, intensity_over_life=FLICKER,
    color_gradient=fade((0.0, 0.75), (1.0, 0.0))), 8)
# A bolt from the sky onto a new totem (and Thunderspire's thunderclaps): upright strands on the ground.
define("SharedFx", "SharedStormStrike", shot(**ADD, **BOLTS, **UPRIGHT,
    max_particles=128, lifetime=[0.28, 0.28], start_rotation=[-6, 6], color_intensity=3.5, size_over_life=EXACT,
    intensity_over_life=scalar((0.0, 1.6), (0.25, 0.5), (0.45, 1.3), (0.7, 0.4), (1.0, 0.8)),
    color_gradient=fade((0.0, 1.0), (0.7, 0.8), (1.0, 0.0))), 12)
# Crackling webs: totem tips, the Thunderspire rod (head) and every zap's hit.
define("SharedFx", "SharedStormWeb", shot(**ADD, **SPARK_WEBS,
    max_particles=1024, lifetime=[0.08, 0.12], render_mode="camera_facing", start_rotation=[0, 360], color_intensity=3.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.15)), intensity_over_life=scalar((0.0, 1.4), (0.35, 0.5), (0.6, 1.2), (1.0, 0.4)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 10)
# Static crawling over the ground around a totem.
define("SharedFx", "SharedStormStatic", shot(**ADD, **SPARK_WEBS,
    max_particles=512, lifetime=[0.12, 0.16], render_mode="horizontal_billboard", start_rotation=[0, 360], color_intensity=2.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.1)), intensity_over_life=scalar((0.0, 1.3), (0.5, 0.6), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.8), (1.0, 0.0))), -7)
# The charged glow at a totem's tip; its light flickers on the ground.
define("SharedFx", "SharedStormGlow", shot(**ADD, **light(3, 10.0, 4.5),
    max_particles=256, lifetime=[0.14, 0.14], texture=f"{K}/light_02.png", render_mode="camera_facing", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.4, size_over_life=sizes((0.0, 1.0), (1.0, 0.8)),
    intensity_over_life=scalar((0.0, 1.3), (0.4, 0.7), (1.0, 1.0)), color_gradient=fade((0.0, 0.7), (1.0, 0.0))), 6)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_shared.py")
