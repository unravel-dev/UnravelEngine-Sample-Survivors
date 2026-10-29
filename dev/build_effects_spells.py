"""Spell effects, second pass: layered impacts, particle projectile heads, status flames.

    python build_effects_spells.py                 list the effects defined here (no editor call)
    python build_effects_spells.py build [group]   create missing effects and (re)apply their settings

Layering (FxComposite in Fx/Effects.cs): one moment = several emitters fired together. Sized layers take
size = layer.size x size unit (the hit radius) as the particle's BIRTH size, or its PEAK when the curve starts at 0
(the engine fits size_over_life to the passed size). Counts scale with the weapon's fxScale (rank, evolution).
Projectile heads (ProjectileHead): one particle per frame at the projectile; the short life fades into a comet tail.
Flash layers carry particle lights (light_count) so impacts light the moor.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import EFFECTS, P, V, WHITE  # noqa: E402,F401
from effect_helpers import Registry, shot, sizes, scalar, alpha, keys  # noqa: E402

K = "app:/data/Textures/Kenney"
W = "app:/data/Textures/weapons"

GROUPS = ("Heads", "Status", "MageFx")
REGISTRY = Registry(GROUPS)
define = REGISTRY.define

GROUND = [[0.0, 1.0, 0.0, 0.0]]
ADD = {"blend_mode": "additive"}
SPIN = {"start_rotation": [0, 360], "spin_speed": [60, 220], "random_spin_direction": True}
FLIPBOOK_FIRE = {"texture": f"{W}/fire_8x8_white.png", "texture_sheet_tiles": [8, 8], "texture_sheet_randomize": True}
EXPLOSION = {"texture": f"{V}/explosion_fire_8x8.png", "texture_sheet_tiles": [8, 8], "texture_sheet_cycles": 1.0,
             "frame_blending": True}
A = "app:/data/Textures/Atlases"
FLAMES = {"texture": f"{A}/flames_4x2.png", "texture_sheet_tiles": [4, 2], "texture_sheet_randomize": True}
SMOKE_LIT = {"texture": f"{A}/smoke_4x2.png", "texture_sheet_tiles": [4, 2], "texture_sheet_randomize": True,
             "render_mode": "billboard", "blend_mode": "normal", "lighting": "volumetric", "soft_fade_distance": 0.8,
             "start_rotation": [0, 360], "spin_speed": [10, 40], "random_spin_direction": True}
DECAL = {"render_mode": "horizontal_billboard", "blend_mode": "normal", "start_rotation": [0, 360]}
SCRIPTED = {"lifetime": [1.0, 1.0]}


def light(count, intensity, reach):
    return {"light_count": count, "light_intensity": intensity, "light_range": reach, "light_color_from_particle": True}


def fade(*points):
    """White gradient with an alpha shape (tinted layers: the caller's colour multiplies it)."""
    return alpha(*points)


# ------------------------------------------------------------------------------------------------------- heads
# Fired every frame per projectile with EmitSized: birth size = head size, the curve only shrinks.
define("Heads", "HeadCore", shot(**ADD,
    max_particles=2048, lifetime=[0.07, 0.07], texture=f"{P}/glow.png", render_mode="camera_facing",
    primitive="octagon", color_intensity=3.5, size_over_life=sizes((0.0, 1.0), (1.0, 0.55)),
    color_gradient=keys((0.0, WHITE + [1.0]), (1.0, WHITE + [0.0]))), 8)
define("Heads", "HeadHalo", shot(**ADD, **light(10, 7.0, 4.5),
    max_particles=2048, lifetime=[0.08, 0.08], texture=f"{K}/light_01.png", render_mode="camera_facing",
    primitive="octagon", color_intensity=1.4, start_rotation=[0, 360],
    size_over_life=sizes((0.0, 1.0), (1.0, 0.7)), color_gradient=fade((0.0, 0.7), (1.0, 0.0))), 6)
define("Heads", "HeadFlame", shot(**ADD, **FLIPBOOK_FIRE, **SPIN,
    max_particles=2048, lifetime=[0.08, 0.12], render_mode="camera_facing", direction="up", direction_spread=35.0,
    start_speed=[0.4, 1.0], color_intensity=3.2, size_over_life=sizes((0.0, 1.0), (1.0, 0.3, 0.45)),
    color_gradient=keys((0.0, WHITE + [0.0]), (0.15, WHITE + [1.0]), (1.0, [0.8, 0.8, 0.8, 0.0]))), 7)
define("Heads", "HeadStar", shot(**ADD,
    max_particles=2048, lifetime=[0.1, 0.1], texture=f"{K}/star_06.png", render_mode="camera_facing",
    color_intensity=4.0, start_rotation=[0, 90], spin_speed=[200, 400], random_spin_direction=True,
    size_over_life=sizes((0.0, 1.0), (1.0, 0.6)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 9)
define("Heads", "HeadCorona", shot(**ADD,
    max_particles=1024, lifetime=[0.08, 0.08], texture=f"{K}/star_08.png", render_mode="camera_facing",
    color_intensity=3.0, start_rotation=[0, 360], spin_speed=[90, 180], random_spin_direction=True,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.25)), color_gradient=fade((0.0, 0.8), (1.0, 0.0))), 7)

# ------------------------------------------------------------------------------------------------------ status
define("Status", "BurnLick", shot(**ADD, **FLIPBOOK_FIRE, **SPIN,
    max_particles=1024, lifetime=[0.35, 0.55], render_mode="camera_facing", direction="up", direction_spread=25.0,
    start_speed=[0.9, 1.8], drag=1.0, color_intensity=2.6, soft_fade_distance=0.3,
    size_over_life=sizes((0.0, 1.0), (0.4, 0.85), (1.0, 0.25)),
    color_gradient=keys((0.0, [1.0, 0.85, 0.5, 0.0]), (0.12, [1.0, 0.7, 0.3, 0.95]), (0.6, [1.0, 0.38, 0.1, 0.6]),
                        (1.0, [0.6, 0.15, 0.05, 0.0]))), 4)

# ------------------------------------------------------------------------------------------------- shared fire
# Explosion layers sized by the blast radius (size unit = radius).
define("MageFx", "FireFlash", shot(**ADD, **light(4, 28.0, 8.0),
    max_particles=128, lifetime=[0.12, 0.12], texture=f"{P}/glow.png", render_mode="camera_facing", primitive="octagon", color_intensity=2.0,
    size_over_life=sizes((0.0, 0.0), (0.12, 0.9), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (0.35, 0.7), (1.0, 0.0))), 12)
define("MageFx", "FireBall", shot(**ADD, **FLAMES,
    max_particles=1024, lifetime=[0.35, 0.6], render_mode="camera_facing", shape="sphere", direction="outward",
    start_speed=[2.0, 4.5], drag=4.5, acceleration=[0.0, 1.5, 0.0], start_rotation=[0, 360], spin_speed=[-120, 120],
    color_intensity=2.4, size_over_life=sizes((0.0, 1.0), (0.35, 1.35, 1.5), (1.0, 1.5, 1.8)),
    color_gradient=keys((0.0, [1.0, 0.95, 0.75, 0.0]), (0.08, [1.0, 0.9, 0.6, 1.0]), (0.4, [1.0, 0.55, 0.2, 0.9]),
                        (1.0, [0.6, 0.2, 0.08, 0.0]))), 10)
define("MageFx", "FireShock", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{W}/shock_ring.png", render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.3, size_over_life=sizes((0.0, 0.0), (0.05, 0.3), (0.45, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), 2)
define("MageFx", "FireSparks", shot(**ADD,
    max_particles=1024, lifetime=[0.3, 0.6], texture=f"{P}/ember.png", render_mode="stretched", shape="sphere",
    direction="outward", start_speed=[7.0, 14.0], gravity_scale=1.4, drag=2.2, color_intensity=9.0,
    stretch_length_scale=1.0, stretch_speed_scale=0.06, size_over_life=sizes((0.0, 0.07, 0.11), (1.0, 0.02, 0.04)),
    color_gradient=keys((0.0, [1.0, 0.95, 0.75, 1.0]), (0.5, [1.0, 0.6, 0.2, 1.0]), (1.0, [1.0, 0.3, 0.05, 0.0]))), 11)
define("MageFx", "FireEmbers", shot(**ADD,
    max_particles=1024, lifetime=[1.0, 1.8], texture=f"{P}/ember.png", render_mode="camera_facing", shape="sphere",
    direction="outward", start_speed=[1.0, 3.0], gravity_scale=-0.25, drag=1.6, turbulence_strength=1.8,
    color_intensity=7.0, size_over_life=sizes((0.0, 0.05, 0.09), (1.0, 0.0, 0.02)),
    color_gradient=keys((0.0, [1.0, 0.85, 0.5, 1.0]), (0.5, [1.0, 0.5, 0.15, 0.9]), (1.0, [1.0, 0.25, 0.05, 0.0]))), 11)
define("MageFx", "FireSmoke", shot(**SMOKE_LIT,
    max_particles=256, lifetime=[1.2, 1.8], shape="sphere", direction="outward", start_speed=[0.6, 1.4], drag=1.5,
    acceleration=[0.0, 0.9, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 2.2, 2.6)),
    color_gradient=keys((0.0, [0.25, 0.22, 0.2, 0.0]), (0.15, [0.22, 0.2, 0.18, 0.55]), (1.0, [0.3, 0.3, 0.3, 0.0]))), 1)
define("MageFx", "ScorchMark", shot(**DECAL, **SCRIPTED,
    max_particles=128, texture=f"{K}/scorch_02.png", color_intensity=1.0, size_over_life=sizes((0.0, 1.0), (1.0, 1.05)),
    color_gradient=keys((0.0, [0.12, 0.08, 0.06, 0.0]), (0.04, [0.12, 0.08, 0.06, 0.75]), (0.7, [0.12, 0.08, 0.06, 0.6]),
                        (1.0, [0.12, 0.08, 0.06, 0.0]))), -20)
define("MageFx", "FireMuzzle", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=f"{K}/magic_05.png", render_mode="camera_facing", start_rotation=[0, 90],
    color_intensity=4.0, size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.6)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 9)
# Sunfire: solar rays and a second, brighter ring.
define("MageFx", "SolarRays", shot(**ADD, lifetime=[0.3, 0.3],
    max_particles=128, texture=f"{K}/star_08.png", render_mode="camera_facing", start_rotation=[0, 360],
    spin_speed=[40, 90], random_spin_direction=True, color_intensity=1.6, size_over_life=sizes((0.0, 0.0), (0.1, 0.8), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.8), (1.0, 0.0))), 13)
define("MageFx", "SolarHalo", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{K}/circle_03.png", render_mode="horizontal_billboard", start_rotation=[0, 360],
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.1, 0.55), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 3)

# ------------------------------------------------------------------------------------------------------ arcane
# Blink (Mage active) and anything arcane violet: tinted by the script.
define("MageFx", "ArcaneFlash", shot(**ADD, **light(3, 20.0, 7.0),
    max_particles=128, lifetime=[0.14, 0.14], texture=f"{K}/magic_05.png", render_mode="camera_facing", start_rotation=[0, 90],
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.7)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)
define("MageFx", "ArcaneSigil", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{K}/magic_02.png", render_mode="horizontal_billboard", start_rotation=[0, 360],
    spin_speed=[90, 140], random_spin_direction=True, color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.8, 0.85), (1.0, 0.0))), 2)
define("MageFx", "ArcaneTwirl", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=f"{A}/twirls_2x2.png", texture_sheet_tiles=[2, 2], texture_sheet_randomize=True,
    render_mode="horizontal_billboard", start_rotation=[0, 360], spin_speed=[300, 420], color_intensity=2.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 0.25)), color_gradient=fade((0.0, 0.0), (0.15, 1.0), (1.0, 0.0))), 3)
define("MageFx", "ArcaneStars", shot(**ADD,
    max_particles=1024, lifetime=[0.35, 0.65], texture=f"{A}/stars_4x2.png", texture_sheet_tiles=[4, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", shape="sphere", direction="outward", start_speed=[3.0, 7.0],
    drag=3.0, start_rotation=[0, 360], spin_speed=[90, 300], random_spin_direction=True, color_intensity=2.6,
    size_over_life=sizes((0.0, 0.16, 0.3), (1.0, 0.0, 0.05)), color_gradient=fade((0.0, 1.0), (0.6, 0.9), (1.0, 0.0))), 11)
define("MageFx", "ArcaneMotes", shot(**ADD,
    max_particles=1024, lifetime=[0.3, 0.45], texture=f"{P}/sparkle.png", render_mode="camera_facing", shape="sphere",
    direction="inward", start_speed=[4.0, 7.0], start_rotation=[0, 90], spin_speed=[180, 360], random_spin_direction=True,
    color_intensity=3.0, size_over_life=sizes((0.0, 0.12, 0.2), (1.0, 0.02, 0.05)), color_gradient=fade((0.0, 0.0), (0.2, 1.0), (1.0, 0.0))), 11)

# ----------------------------------------------------------------------------------------------------- starfall
define("MageFx", "MeteorDust", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.9, 1.4], shape="circle", direction="outward", start_speed=[6.0, 11.0], drag=4.0,
    size_over_life=sizes((0.0, 1.0), (1.0, 2.4, 2.8)),
    color_gradient=keys((0.0, [0.55, 0.48, 0.4, 0.0]), (0.1, [0.5, 0.45, 0.38, 0.6]), (1.0, [0.45, 0.42, 0.38, 0.0]))), 2)
define("MageFx", "MeteorRocks", shot(
    max_particles=512, lifetime=[0.8, 1.3], texture=f"{W}/rock_chunk_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", blend_mode="normal", lighting="surface",
    start_rotation=[0, 360], spin_speed=[300, 800], random_spin_direction=True, shape="hemisphere", direction="outward",
    start_speed=[5.0, 10.0], gravity_scale=2.2, collision="bounce", collision_planes=GROUND, restitution=0.35,
    size_over_life=sizes((0.0, 0.18, 0.34), (1.0, 0.16, 0.3)),
    color_gradient=keys((0.0, [0.55, 0.45, 0.4, 1.0]), (0.85, [0.5, 0.42, 0.38, 1.0]), (1.0, [0.5, 0.42, 0.38, 0.0]))), 5)
define("MageFx", "MeteorPillar", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{P}/light_shaft.png", render_mode="vertical_billboard", pivot=[0.5, 1.0],
    color_intensity=4.0, size_axes=[0.5, 1.6, 1.0], size_over_life=sizes((0.0, 0.0), (0.08, 1.0), (1.0, 0.7)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)

# ---------------------------------------------------------------------------------------------------- rime nova
ICE_TEX = {"texture": f"{V}/crystal_shard_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True}
define("MageFx", "FrostFlash", shot(**ADD, **SCRIPTED, **light(3, 22.0, 9.0),
    max_particles=64, texture=f"{P}/glow.png", render_mode="camera_facing", primitive="octagon", color_intensity=1.8,
    size_over_life=sizes((0.0, 0.0), (0.1, 0.8), (1.0, 1.0)), color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 12)
define("MageFx", "FrostRing", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=f"{K}/circle_02.png", render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=3.0, size_over_life=sizes((0.0, 0.0), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.08, 1.0), (0.8, 0.9), (1.0, 0.0))), 3)
define("MageFx", "FrostSpikes", shot(**ICE_TEX,
    max_particles=1024, lifetime=[0.4, 0.6], render_mode="stretched", blend_mode="additive", shape="circle",
    direction="outward", start_speed=[8.0, 13.0], drag=2.6, color_intensity=2.2, stretch_length_scale=1.0,
    stretch_speed_scale=0.02, size_over_life=sizes((0.0, 0.45, 0.7), (1.0, 0.2, 0.35)),
    color_gradient=fade((0.0, 1.0), (0.7, 0.9), (1.0, 0.0))), 9)
define("MageFx", "FrostFrost", shot(**dict(DECAL, blend_mode="additive"), **SCRIPTED,
    max_particles=64, texture=f"{V}/frost_decal.png", color_intensity=0.9,
    size_over_life=sizes((0.0, 0.0), (0.15, 0.95), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 0.8), (0.75, 0.6), (1.0, 0.0))), -18)
define("MageFx", "FrostSnow", shot(**ADD,
    max_particles=1024, lifetime=[1.2, 2.0], texture=f"{V}/snowflake_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="camera_facing", shape="circle", direction="outward",
    start_speed=[1.0, 4.0], drag=1.8, gravity_scale=0.12, turbulence_strength=1.2, start_rotation=[0, 360],
    spin_speed=[40, 120], random_spin_direction=True, color_intensity=2.0, size_over_life=sizes((0.0, 0.08, 0.14), (1.0, 0.04, 0.08)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (1.0, 0.0))), 8)
# Ice crystals bursting up along the nova's ring (the script places one per point of each band's circle).
define("MageFx", "FrostCrystal", shot(**ICE_TEX,
    max_particles=2048, lifetime=[0.7, 1.0], render_mode="vertical_billboard", pivot=[0.5, 1.0], blend_mode="additive",
    start_rotation=[-18, 18], color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.08, 1.0), (0.75, 0.9), (1.0, 0.0)),
    color_gradient=fade((0.0, 1.0), (0.7, 0.85), (1.0, 0.0))), 7)
define("MageFx", "IceShatter", shot(**ICE_TEX,
    max_particles=1024, lifetime=[0.6, 1.0], render_mode="camera_facing", blend_mode="normal", lighting="surface",
    shape="sphere", direction="outward", start_speed=[3.0, 7.0], gravity_scale=2.0, collision="bounce",
    collision_planes=GROUND, restitution=0.3, start_rotation=[0, 360], spin_speed=[300, 700], random_spin_direction=True,
    color_intensity=1.6, size_over_life=sizes((0.0, 0.12, 0.24), (1.0, 0.08, 0.16)),
    color_gradient=keys((0.0, [0.75, 0.9, 1.0, 1.0]), (0.8, [0.7, 0.88, 1.0, 1.0]), (1.0, [0.7, 0.88, 1.0, 0.0]))), 6)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells.py")
