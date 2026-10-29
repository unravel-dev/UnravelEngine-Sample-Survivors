"""Core effects of the class update: the chest beacon (light pillar, sparkle column, ground pulse, drop flash) and the
passive traits (Heartbeat, Spiked Plate, Aftershock, Epiphany, Soul Magnet, Tailwind, Overdrive). Scripts layer them
with FxComposite (Run.cs chest beacons, Weapons/PassiveTraits.cs); every layer is tinted by the script.

    python build_effects_core_v2.py                 list the effects defined here (no editor call)
    python build_effects_core_v2.py build [group]   create missing effects and (re)apply their settings

Sizes:
- Rings (circle_02) draw their line at 0.29 x the size: size = 3.4 x the wanted radius.
- The rune sigil's outer circle is at 0.46 x the size: size = 2.17 x the radius.
- The pillar stands on the ground (pivot at the bottom): its size is its height.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects_spells import P, V, K, WHITE, ADD, DECAL, SCRIPTED, light, fade  # noqa: E402,F401
from effect_helpers import Registry, shot, sizes, keys  # noqa: E402

GROUP = "CoreFx2"
REGISTRY = Registry((GROUP,))

GLOW = f"{P}/glow.png"
SIGIL = f"{P}/rune_circle.png"
SHAFT = f"{P}/light_shaft.png"
EMBER = f"{P}/ember.png"
GLINT = f"{K}/star_07.png"
MOTE = f"{K}/star_04.png"
RING = f"{K}/circle_02.png"
TWIRL = f"{K}/twirl_01.png"
STREAK = f"{K}/trace_07.png"
STAND = [0.5, 1.0]


def define(name, props, sort):
    REGISTRY.define(GROUP, name, props, sort)


# One short flash with particle lights (size = peak, metres).
define("CoreFlash", shot(**ADD, **light(4, 20.0, 7.0),
    max_particles=128, lifetime=[0.16, 0.16], texture=GLOW, render_mode="camera_facing", primitive="octagon",
    color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.15, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.35, 0.6), (1.0, 0.0))), 12)
# A ground ring that bursts out and settles (size = 3.4 x the radius).
define("CoreRing", shot(**ADD,
    max_particles=256, lifetime=[0.55, 0.55], texture=RING, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.06, 0.35), (0.4, 0.88), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.7), (1.0, 0.0))), -9)
# A slow ground ring that breathes out from the chest (size = 3.4 x the radius, birth size 0).
define("CorePulseRing", shot(**ADD,
    max_particles=128, lifetime=[1.1, 1.1], texture=RING, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.2, 0.45), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 0.9), (0.6, 0.45), (1.0, 0.0))), -9)
# A rune sigil flat on the ground that flares and fades (size = 2.17 x the radius).
define("CoreRune", shot(**ADD,
    max_particles=128, lifetime=[0.45, 0.45], texture=SIGIL, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], spin_speed=[40, 40], color_intensity=1.9, size_over_life=sizes((0.0, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.12, 1.0), (0.5, 0.6), (1.0, 0.0))), -8)
# A spinning swirl flat on the ground (twirl_01, ring at 0.42 x the size).
define("CoreTwirl", shot(**ADD,
    max_particles=64, lifetime=[0.6, 0.6], texture=TWIRL, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], spin_speed=[420, 520], color_intensity=1.7, size_over_life=sizes((0.0, 1.0), (1.0, 0.35)),
    color_gradient=fade((0.0, 0.0), (0.15, 0.9), (0.7, 0.5), (1.0, 0.0))), -7)
# Star motes thrown out that drift up and wink out (sizes absolute).
define("CoreMotes", shot(**ADD,
    max_particles=1024, lifetime=[0.8, 1.4], texture=MOTE, render_mode="camera_facing", shape="sphere",
    direction="outward", start_speed=[1.0, 3.5], drag=2.2, gravity_scale=-0.18, turbulence_strength=1.0,
    start_rotation=[0, 90], spin_speed=[60, 160], random_spin_direction=True, color_intensity=4.0,
    size_over_life=sizes((0.0, 0.0), (0.12, 0.2, 0.34), (1.0, 0.0, 0.04)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.7, 0.8), (1.0, 0.0))), 8)
# Steel shards bursting outward along the ground, stretched along their flight (the spiked plate).
define("CoreShards", shot(**ADD,
    max_particles=1024, lifetime=[0.25, 0.4], texture=EMBER, render_mode="stretched", shape="circle",
    direction="outward", start_speed=[9.0, 15.0], drag=3.0, gravity_scale=0.4, color_intensity=6.0,
    stretch_length_scale=1.2, stretch_speed_scale=0.06, size_over_life=sizes((0.0, 0.09), (1.0, 0.03)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.85), (1.0, 0.0))), 11)
# Wind streaks curling off the hero (a dodge).
define("CoreWindPuff", shot(**ADD,
    max_particles=256, lifetime=[0.22, 0.32], texture=STREAK, render_mode="stretched", shape="sphere",
    direction="outward", start_speed=[5.0, 8.0], drag=5.0, color_intensity=2.4, stretch_length_scale=2.0,
    stretch_speed_scale=0.06, size_over_life=sizes((0.0, 0.5), (1.0, 0.3)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.5, 0.6), (1.0, 0.0))), 6)
# The chest's light pillar: a tall shaft standing on the chest, re-emitted while the chest waits (size = height).
define("CoreBeam", shot(**ADD, **light(1, 10.0, 6.0),
    max_particles=256, lifetime=[0.7, 0.7], texture=SHAFT, render_mode="vertical_billboard", pivot=STAND,
    size_axes=[0.14, 1.0, 1.0], color_intensity=1.6, size_over_life=sizes((0.0, 0.92), (0.5, 1.0), (1.0, 0.96)),
    color_gradient=fade((0.0, 0.0), (0.25, 0.75), (0.75, 0.75), (1.0, 0.0))), 6)
# The drop: the pillar shoots up from the ground at once and fades (size = height, peak).
define("CoreBeamBurst", shot(**ADD, **light(2, 26.0, 9.0),
    max_particles=64, lifetime=[0.6, 0.6], texture=SHAFT, render_mode="vertical_billboard", pivot=STAND,
    size_axes=[0.3, 1.0, 1.0], color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.18, 1.0), (1.0, 0.9)),
    color_gradient=fade((0.0, 1.0), (0.4, 0.7), (1.0, 0.0))), 7)
# Glints rising up the pillar (spawned in a thin disc at the foot, sizes absolute).
define("CoreSparkle", shot(**ADD,
    max_particles=1024, lifetime=[1.2, 1.8], texture=GLINT, render_mode="camera_facing", shape="circle",
    shape_scale=[0.5, 0.5, 0.5], direction="outward", start_speed=[0.2, 0.6], gravity_scale=-0.9,
    turbulence_strength=0.6, start_rotation=[-10, 10], spin_speed=[40, 120], random_spin_direction=True,
    color_intensity=3.2, size_over_life=sizes((0.0, 0.0), (0.15, 0.3, 0.42), (1.0, 0.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.8, 0.8), (1.0, 0.0))), 9)


if __name__ == "__main__":
    REGISTRY.main("build_effects_core_v2.py")
