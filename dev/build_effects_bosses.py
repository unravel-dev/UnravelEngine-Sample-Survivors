"""Barrowlight boss effects (manifest_bosses.md): every boss-only emitter Bosses/BossFx.cs finds by entity name under the
effects root. The shared ones it reuses (TelegraphFill / Ring / Cone, EnemySwing, GraveBurst, SummonMotes, BoneBurst,
EliteBeam) stay in build_effects.py. Data only; build_effects.apply() creates and tunes them.

    python build_effects_bosses.py                 list the effects defined here (no editor call)
    python build_effects_bosses.py build [group]   create missing effects and (re)apply their settings

How BossFx fires them: Burst(id, pos, count) plain; Burst(.., color, size, lifetime), Oriented (heading = velocity
x 0.001), Moving (velocity, size, lifetime) and Launch (velocity, color, size, lifetime). Color.white or no colour =
untinted (the gradient carries the colour); a colour multiplies a white gradient. The size is the size AT BIRTH, or
the PEAK of a curve that starts at 0 (effect_helpers.from_zero rewrites the manifest's "grow from s0" curves so the
passed size stays the final size), the lifetime replaces the emitter's ([1, 1] here).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects import EFFECTS, keys, span, punch, SPARK, P, V, T, BONE  # noqa: E402,F401
from effect_helpers import Registry, shot, sizes, alpha, from_zero  # noqa: E402

B = "app:/data/Textures/bosses"
W = "app:/data/Textures/weapons"

GROUPS = ("Telegraphs", "Bosses")
REGISTRY = Registry(GROUPS)
define = REGISTRY.define
NAMES = REGISTRY.names

GROUND = [[0.0, 1.0, 0.0, 0.0]]
DUST = [0.82, 0.76, 0.66]
SPIN_DUST = [0.85, 0.8, 0.72]
CLEAVE = [1.0, 0.8, 0.7]
DAZED = [1.0, 0.95, 0.6]
GUARD_GOLD = [1.0, 0.9, 0.5]
COIN_GOLD = [1.0, 0.85, 0.35]
SCRIPTED = {"lifetime": [1.0, 1.0]}
CONSTANT = sizes((0.0, 1.0), (1.0, 1.0))
# Oriented ground decals: the script's 0.001 m/s heading turns the texture's +V along it.
DECAL = {"render_mode": "horizontal_billboard", "align_to_direction": True, "blend_mode": "additive"}
GLOW = {"texture": f"{P}/glow.png", "render_mode": "billboard", "blend_mode": "additive", "primitive": "octagon"}
RING = {"texture": f"{V}/ring_energy.png", "blend_mode": "additive", "primitive": "octagon", "start_rotation": [0, 360]}


# ================================================================================================ telegraphs
# Lane (size = LENGTH, at the lane's centre, +V away from the boss): a 4x4 sheet played once fills it from the near end;
# frame_blending crossfades the 16 steps so the front moves smoothly.
define("Telegraphs", "TelegraphLane", shot(**DECAL,
    max_particles=32, **SCRIPTED, texture=f"{B}/lane_fill_4x4.png", texture_sheet_tiles=[4, 4], texture_sheet_cycles=1.0,
    frame_blending=True, color_intensity=1.1, size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.6), (0.9, 0.8), (0.97, 1.0), (1.0, 0.0))), -13)
define("Telegraphs", "TelegraphArc", shot(**DECAL,
    max_particles=16, **SCRIPTED, texture=f"{T}/cone_200.png", color_intensity=0.9,
    size_over_life=from_zero(span((0.1, 0.1), (1.0, 1.0))),
    color_gradient=alpha((0.0, 0.35), (0.9, 0.65), (0.97, 1.0), (1.0, 0.0))), -13)
define("Telegraphs", "TelegraphArcEdge", shot(**DECAL,
    max_particles=16, **SCRIPTED, texture=f"{B}/cone_200_edge.png", color_intensity=2.2, size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.0), (0.08, 1.0), (0.92, 1.0), (1.0, 0.0))), -12)
define("Telegraphs", "SoulNovaWarn", shot(**DECAL,
    max_particles=16, **SCRIPTED, texture=f"{B}/ring_gapped_60.png", color_intensity=2.2, size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.0), (0.1, 1.0), (0.92, 1.0), (1.0, 0.0))), -12)
# One segment of an expanding nova band: size = band thickness (V, radial), U = 1.75 x it along the ring; the script
# launches it radially at 6 m/s, so no drag and no gravity.
define("Telegraphs", "SoulNovaRing", shot(**DECAL,
    max_particles=256, **SCRIPTED, texture=f"{B}/nova_segment.png", color_intensity=2.5, size_axes=[1.75, 1, 1],
    size_over_life=CONSTANT, color_gradient=alpha((0.0, 0.0), (0.03, 0.9), (0.85, 0.8), (1.0, 0.0))), -11)

# ================================================================================================ boss effects
define("Bosses", "BossSlamDust", shot(
    max_particles=64, **SCRIPTED, texture=f"{V}/smoke_puffs_4x4.png", texture_sheet_tiles=[4, 4],
    texture_sheet_randomize=True, render_mode="horizontal_billboard", blend_mode="normal", lighting="surface",
    start_rotation=[0, 360], size_over_life=from_zero(punch((0.3, 0.3), (1.0, 1.0))),
    color_gradient=alpha((0.0, 0.0), (0.1, 0.7), (1.0, 0.0), rgb=DUST)), -8)
# Manifest 0.6 -> 1.2 of the size: written as 0.5 -> 1.0 so the passed size (3 x body radius) is the cloud's peak.
define("Bosses", "BossRiseDust", shot(
    max_particles=32, **SCRIPTED, texture=f"{V}/smoke_billow_4x4.png", texture_sheet_tiles=[4, 4],
    texture_sheet_randomize=True, render_mode="billboard", blend_mode="normal", lighting="volumetric",
    soft_fade_distance=1.0, start_rotation=[0, 360], spin_speed=[5, 12], random_spin_direction=True,
    size_over_life=sizes((0.0, 0.0), (0.02, 0.5), (1.0, 1.0)),
    color_gradient=alpha((0.0, 0.0), (0.15, 0.55), (1.0, 0.0), rgb=DUST)), -5)
define("Bosses", "BossCleaveTrail", shot(**DECAL,
    max_particles=8, **SCRIPTED, texture=f"{V}/arc_8x1.png", texture_sheet_tiles=[8, 1], texture_sheet_randomize=True,
    start_rotation=[90, 90], color_intensity=3.0, size_over_life=from_zero(span((0.8, 0.8), (1.0, 1.0))),
    color_gradient=alpha((0.0, 0.0), (0.15, 0.85), (1.0, 0.0), rgb=CLEAVE)), 1)
# Bone spikes standing on the ground: pivot [0.5, 1] puts the quad's bottom edge on the particle (as EliteBeam); the
# 2x2 sheet holds two spike groups and their mirror images, so a random frame is a random flip.
define("Bosses", "BoneSpike", shot(
    max_particles=64, **SCRIPTED, texture=f"{B}/bone_spikes_2x2.png", texture_sheet_tiles=[2, 2],
    texture_sheet_randomize=True, render_mode="vertical_billboard", pivot=[0.5, 1.0], blend_mode="normal",
    lighting="surface", primitive="cutout", size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (0.7, 1.0), (1.0, 0.0)),
    color_gradient=alpha((0.0, 1.0), (0.8, 1.0), (1.0, 0.0), rgb=BONE)), 0)
# shard.png is drawn sideways: start_rotation 90 lays its long axis along the fall, size_axes x 4 makes it a spear.
define("Bosses", "BoneRainFall", shot(
    max_particles=64, **SCRIPTED, texture=f"{P}/shard.png", render_mode="billboard", align_to_direction=True,
    start_rotation=[90, 90], size_axes=[4, 1, 1], blend_mode="normal", lighting="surface", size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.3), (0.2, 1.0), (1.0, 1.0), rgb=BONE)), 1)
define("Bosses", "BoneRainImpact", shot(**RING,
    max_particles=64, **SCRIPTED, render_mode="horizontal_billboard", color_intensity=3.0,
    size_over_life=from_zero(punch((0.4, 0.4), (1.0, 1.0))),
    color_gradient=keys((0.0, [0.9, 1.0, 0.85, 1.0]), (0.5, [0.75, 1.0, 0.72, 0.6]), (1.0, [0.6, 1.0, 0.6, 0.0]))), -9)
define("Bosses", "PhaseShockwave", shot(**RING,
    max_particles=8, **SCRIPTED, render_mode="horizontal_billboard", color_intensity=8.0,
    size_over_life=from_zero(punch((0.1, 0.1), (1.0, 1.0), 0.35, 0.85)),
    color_gradient=keys((0.0, [1.0, 1.0, 1.0, 1.0]), (0.4, [0.5, 1.0, 0.6, 0.7]), (1.0, [0.5, 1.0, 0.6, 0.0]))), -9)
define("Bosses", "BossSpinWind", shot(
    max_particles=64, **SCRIPTED, texture=f"{V}/arc_8x1.png", texture_sheet_tiles=[8, 1], texture_sheet_randomize=True,
    render_mode="horizontal_billboard", blend_mode="normal", start_rotation=[0, 360], spin_speed=[900, 1100],
    random_spin_direction=False, size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.0), (0.2, 0.5), (1.0, 0.0), rgb=SPIN_DUST)), 0)
define("Bosses", "BossDazed", shot(
    max_particles=8, **SCRIPTED, texture=f"{B}/dazed_stars.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=3.0, spin_speed=[240, 240], random_spin_direction=False,
    size_over_life=CONSTANT, color_gradient=alpha((0.0, 0.0), (0.1, 1.0), (0.85, 1.0), (1.0, 0.0), rgb=DAZED)), 3)
# The manifest's optional green ground light: the brightest live core particle lights 6 m around it.
define("Bosses", "BossCoreGlow", shot(**GLOW,
    max_particles=32, **SCRIPTED, color_intensity=5.0, soft_fade_distance=0.5, size_over_life=CONSTANT,
    light_count=1, light_range=6.0, light_intensity=6.0, light_color_from_particle=True,
    color_gradient=alpha((0.0, 0.0), (0.3, 0.9), (1.0, 0.0))), 3)
define("Bosses", "KingCrackPulse", shot(
    max_particles=8, **SCRIPTED, texture=f"{W}/crack_radial.png", render_mode="horizontal_billboard",
    blend_mode="additive", color_intensity=3.0, start_rotation=[0, 360], size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.0), (0.25, 0.8), (1.0, 0.0))), -10)
# Manifest 0.9 -> 1.05 of the size: written as 0.857 -> 1.0 (peak = the passed size).
define("Bosses", "BossGuardGlow", shot(**GLOW,
    max_particles=16, **SCRIPTED, color_intensity=2.5, size_over_life=sizes((0.0, 0.0), (0.02, 0.857), (1.0, 1.0)),
    color_gradient=alpha((0.0, 0.0), (0.3, 0.55), (1.0, 0.0))), 3)
define("Bosses", "GuardSpark", shot(**SPARK,
    max_particles=128, lifetime=[0.3, 0.3], color_intensity=8.0, size_axes=[1, 3, 1], start_speed=[4.0, 8.0], drag=3.0,
    size_over_life=sizes((0.0, 0.12, 0.2), (1.0, 0.05, 0.05)),
    color_gradient=keys((0.0, GUARD_GOLD + [1.0]), (1.0, GUARD_GOLD + [0.0]))), 2)
define("Bosses", "ArchmageSigil", shot(
    max_particles=8, **SCRIPTED, texture=f"{B}/sigil_circle.png", render_mode="horizontal_billboard",
    blend_mode="additive", primitive="octagon", color_intensity=3.0, spin_speed=[30, 30], random_spin_direction=False,
    size_over_life=from_zero(sizes((0.0, 0.6), (0.06, 1.0), (1.0, 1.0))),
    color_gradient=alpha((0.0, 0.0), (0.06, 0.9), (0.9, 0.8), (1.0, 0.0))), -12)
# The afterimage: the size is the quad's width basis (1.8 x scale); size_axes [1.2, 2] make it 2.16 x 3.6 model units
# around the chest, the frame ghost_mage.png is rendered in.
define("Bosses", "ArchmageGhost", shot(
    max_particles=4, **SCRIPTED, texture=f"{B}/ghost_mage.png", render_mode="billboard", blend_mode="additive",
    color_intensity=2.0, size_axes=[1.2, 2.0, 1], direction="up", start_speed=[0.3, 0.5],
    size_over_life=sizes((0.0, 1.0), (1.0, 1.1)), color_gradient=alpha((0.0, 0.7), (1.0, 0.0))), 2)
define("Bosses", "BlinkFlash", shot(**GLOW,
    max_particles=8, **SCRIPTED, color_intensity=6.0, size_over_life=from_zero(punch((0.3, 0.3), (1.0, 1.0))),
    color_gradient=alpha((0.0, 1.0), (1.0, 0.0))), 3)
define("Bosses", "BossBoneBurst", shot(
    max_particles=1024, lifetime=[1.6, 1.6], texture=f"{P}/shard.png", render_mode="billboard", blend_mode="normal",
    lighting="surface", shape="hemisphere", start_speed=[4.0, 12.0], gravity_scale=2.4, start_rotation=[0, 360],
    spin_speed=[360, 720], random_spin_direction=True, collision="bounce", collision_planes=GROUND, restitution=0.3,
    max_bounces=2, size_over_life=sizes((0.0, 0.25, 0.45), (1.0, 0.2, 0.35)),
    color_gradient=alpha((0.0, 1.0), (0.8, 1.0), (1.0, 0.0), rgb=BONE)), 1)
define("Bosses", "BossDeathBurst", shot(**RING,
    max_particles=8, **SCRIPTED, render_mode="billboard", color_intensity=8.0,
    size_over_life=from_zero(punch((0.2, 0.2), (1.0, 1.0))),
    color_gradient=keys((0.0, [1.0, 1.0, 0.9, 1.0]), (0.5, [1.0, 0.8, 0.38, 0.7]), (1.0, [1.0, 0.8, 0.38, 0.0]))), 3)
# Re-emitted every frame at the wave's radius: shock_ring's line sits at r 0.93 of the quad (ring_energy's at 0.83),
# so the drawn front is where the wave kills.
define("Bosses", "DeathWave", shot(**dict(RING, texture=f"{W}/shock_ring.png"),
    max_particles=256, **SCRIPTED, render_mode="horizontal_billboard", color_intensity=5.0, size_over_life=CONSTANT,
    color_gradient=alpha((0.0, 0.9), (1.0, 0.0))), -9)
define("Bosses", "GoldShower", shot(
    max_particles=128, lifetime=[1.6, 1.6], texture=f"{B}/coin.png", render_mode="billboard", blend_mode="normal",
    lighting="surface", shape="cone", cone_angle=40.0, direction="up", start_speed=[6.0, 10.0], gravity_scale=2.0,
    collision="bounce", collision_planes=GROUND, restitution=0.4, start_rotation=[0, 360], spin_speed=[360, 720],
    random_spin_direction=True, size_over_life=sizes((0.0, 0.22, 0.32), (1.0, 0.22, 0.32)),
    color_gradient=alpha((0.0, 1.0), (0.8, 1.0), (1.0, 0.0), rgb=COIN_GOLD)), 2)


if __name__ == "__main__":
    REGISTRY.main("build_effects_bosses.py")
