"""Map event effects: the steady ring and floor glow of an event's ground and the bone pyres' flames. Scripts tint every
piece (Events/MapEvent.cs EventFx) and layer them with the core effects (CoreBeam, CorePulseRing, CoreRune, CoreMotes).

    python build_effects_events.py                 list the effects defined here (no editor call)
    python build_effects_events.py build [group]   create missing effects and (re)apply their settings

Sizes:
- The ring (circle_02) draws its line at 0.29 x the size: size = 3.4 x the wanted radius.
- The floor glow fades out before its edge: size = 2.6 x the radius of the ground it fills.
- Steady pieces live 0.6 s and are re-emitted every 0.3 s; their alpha ramps in and out so the overlap stays even.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects_spells import P, ADD, FLIPBOOK_FIRE, fade  # noqa: E402
from effect_helpers import Registry, shot, sizes  # noqa: E402

GROUP = "EventFx"
REGISTRY = Registry((GROUP,))

GLOW = f"{P}/glow.png"
RING = "app:/data/Textures/Kenney/circle_02.png"
STEADY = fade((0.0, 0.0), (0.25, 1.0), (0.75, 1.0), (1.0, 0.0))


def define(name, props, sort):
    REGISTRY.define(GROUP, name, props, sort)


# The steady ring on the edge of an event's ground (size = 3.4 x the radius).
define("EventZoneRing", shot(**ADD,
    max_particles=128, lifetime=[0.6, 0.6], texture=RING, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.5, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=STEADY), -9)
# The soft glow over an event's ground (size = 2.6 x the radius); the script dims it by the event's state.
define("EventZoneFloor", shot(**ADD,
    max_particles=128, lifetime=[0.6, 0.6], texture=GLOW, render_mode="horizontal_billboard", primitive="octagon",
    color_intensity=0.9, size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=STEADY), -10)
# A tongue of white fire licking up off a pyre's skull, tinted by the script (sizes absolute).
define("EventFlame", shot(**ADD, **FLIPBOOK_FIRE,
    max_particles=512, lifetime=[0.5, 0.75], render_mode="camera_facing", shape="circle", shape_scale=[0.3, 0.05, 0.3],
    direction="up", direction_spread=10.0, start_speed=[1.6, 2.6], drag=1.2, start_rotation=[-12, 12], color_intensity=2.4,
    size_over_life=sizes((0.0, 0.6, 0.75), (0.35, 1.0, 1.25), (1.0, 0.2, 0.3)),
    color_gradient=fade((0.0, 0.0), (0.12, 1.0), (0.6, 0.7), (1.0, 0.0))), 5)


if __name__ == "__main__":
    REGISTRY.main("build_effects_events.py")
