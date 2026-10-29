"""Knight spell effects: Oathblade Arc / Dawnbreaker, Aegis Orbit / Bastion Halo, Verdict / Final Verdict, the Shield
Rush active and the Bulwark block shockwave. Holy gold, white and pale steel; the evolutions add their own accent and
signature layers (Dawnbreaker: sunrise amber rays and golden afterimages; Bastion Halo: celestial azure halo rings;
Final Verdict: a column of light from the sky and a holy shockwave).

    python build_effects_spells_knight.py                 list the effects defined here (no editor call)
    python build_effects_spells_knight.py build [group]   create missing effects and (re)apply their settings

How the scripts fire them:
- Tinted layers keep white gradients so the script's colour sets the hue (gold, holy white, dawn amber, azure).
- A size passed by a script is the particle's BIRTH size, or its PEAK when the curve starts at 0 (FxComposite layers:
  size = layer.size x the size unit, usually the hit radius).
- Slash crescents (slash_04) lie flat and turn their top along the velocity the script gives them (EmitMoving); the
  pivot sits on the arc's centre of curvature, so the band spans 0.18-0.31 x the size around the emit point.
- Rings (circle_02) draw their line at 0.29 x the size: size = 3.4 x the wanted radius. The rune sigil's outer circle
  is at 0.46 x the size: size = 2.17 x the radius.
- Rays and columns stand on the ground (pivot at the bottom): their size is their height.
- Projectile heads (Weapons/Knight/KnightLooks.cs): one particle per frame at the projectile, birth size = head size.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects_spells import P, V, K, WHITE, ADD, DECAL, SCRIPTED, SMOKE_LIT, light, fade  # noqa: E402
from effect_helpers import Registry, shot, sizes, scalar, keys  # noqa: E402

GROUP = "KnightFx"
REGISTRY = Registry((GROUP,))

GLOW = f"{P}/glow.png"
EMBER = f"{P}/ember.png"
SIGIL = f"{P}/rune_circle.png"
SHAFT = f"{P}/light_shaft.png"
GLINT = f"{K}/star_07.png"
MOTE = f"{K}/star_04.png"
CROSS = f"{K}/magic_03.png"
SLASH = f"{K}/slash_04.png"
STREAK = f"{K}/trace_07.png"
RING = f"{K}/circle_02.png"
DOUBLE_RING = f"{K}/circle_03.png"
TWIRL = f"{K}/twirl_01.png"
BEAMS = {"texture": f"{V}/beam_4x1.png", "texture_sheet_tiles": [4, 1], "texture_sheet_randomize": True}
ADDITIVE_DECAL = dict(DECAL, blend_mode="additive")
# slash_04's arc: centre of curvature at uv (0.5, 0.604), middle radius 0.25 x the quad, 136 deg wide.
SLASH_PIVOT = [0.5, 0.604]
STAND = [0.5, 1.0]


def define(name, props, sort):
    REGISTRY.define(GROUP, name, props, sort)


# ------------------------------------------------------------------------------------------------ shared layers
# One short flash per impact, with particle lights (sized: metres, or x the hit radius in composites).
define("KnightFlash", shot(**ADD, **light(4, 22.0, 7.0),
    max_particles=128, lifetime=[0.13, 0.13], texture=GLOW, render_mode="camera_facing", primitive="octagon",
    color_intensity=1.9, size_over_life=sizes((0.0, 0.0), (0.12, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.35, 0.65), (1.0, 0.0))), 12)
# A four-point star that pops and closes: a blade catching the light, a hit, a shield appearing (size = peak).
define("KnightGlint", shot(**ADD,
    max_particles=512, lifetime=[0.2, 0.26], texture=GLINT, render_mode="camera_facing", start_rotation=[-10, 10],
    spin_speed=[30, 80], random_spin_direction=True, color_intensity=3.2,
    size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (0.45, 0.6), (1.0, 0.0)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.9), (1.0, 0.0))), 14)
# The holy compass star, upright (magic_03): the Bulwark's shield flare, a block, the Verdict's seal (size = peak).
define("KnightCrossFlare", shot(**ADD,
    max_particles=128, lifetime=[0.34, 0.34], texture=CROSS, render_mode="camera_facing", spin_speed=[18, 18],
    color_intensity=2.4, size_over_life=sizes((0.0, 0.0), (0.1, 0.8), (0.5, 0.95), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.45, 0.75), (1.0, 0.0))), 13)
# Steel sparks off a blade or a shield, stretched along their flight and falling: absolute sizes in composites, or the
# script's birth size for sparks it aims (EmitMoving). White-hot, cooling a little warmer than the tint.
define("KnightSteelSparks", shot(**ADD,
    max_particles=1024, lifetime=[0.25, 0.5], texture=EMBER, render_mode="stretched", shape="hemisphere",
    direction="outward", start_speed=[6.0, 12.0], gravity_scale=1.5, drag=2.0, color_intensity=8.0,
    stretch_length_scale=1.0, stretch_speed_scale=0.05, size_over_life=sizes((0.0, 0.07), (1.0, 0.025)),
    color_gradient=keys((0.0, WHITE + [1.0]), (0.5, [1.0, 0.92, 0.8, 0.9]), (1.0, [1.0, 0.82, 0.6, 0.0]))), 11)
# Holy motes: little stars thrown out that drift up on a breeze and wink out (the lingering layer).
define("KnightMotes", shot(**ADD,
    max_particles=1024, lifetime=[0.9, 1.5], texture=MOTE, render_mode="camera_facing", shape="sphere",
    direction="outward", start_speed=[0.8, 3.2], drag=2.2, gravity_scale=-0.12, turbulence_strength=1.0,
    start_rotation=[0, 90], spin_speed=[60, 160], random_spin_direction=True, color_intensity=5.0,
    size_over_life=sizes((0.0, 0.0), (0.12, 0.22, 0.36), (1.0, 0.0, 0.05)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.7, 0.8), (1.0, 0.0))), 8)
# Ground shockwave (circle_02, line at 0.29 x the size): bursts out and settles; 0.45 s unless the script sets it.
define("KnightShockRing", shot(**ADD,
    max_particles=128, lifetime=[0.45, 0.45], texture=RING, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=2.4, size_over_life=sizes((0.0, 0.0), (0.05, 0.3), (0.35, 0.85), (1.0, 1.0)),
    color_gradient=fade((0.0, 1.0), (0.5, 0.75), (1.0, 0.0))), -9)
# Moor dust thrown along the ground by heavy hits (lit smoke, untinted; sized x the hit radius).
define("KnightDust", shot(**SMOKE_LIT,
    max_particles=512, lifetime=[0.8, 1.2], shape="circle", direction="outward", start_speed=[4.0, 9.0], drag=4.0,
    acceleration=[0.0, 0.5, 0.0], size_over_life=sizes((0.0, 1.0), (1.0, 2.3)),
    color_gradient=keys((0.0, [0.62, 0.62, 0.62, 0.0]), (0.1, [0.6, 0.6, 0.6, 0.5]), (1.0, [0.56, 0.57, 0.6, 0.0]))), 1)
# Wind lines (stretched trace_07, the line is 8% of the quad's width): along a sweep, past a rushing knight, falling
# with a sword, streaking out of a shockwave. The script sets the width (size), velocity and lifetime.
define("KnightSweepStreak", shot(**ADD, **SCRIPTED,
    max_particles=1024, texture=STREAK, render_mode="stretched", drag=5.0, color_intensity=3.0,
    stretch_length_scale=2.2, stretch_speed_scale=0.06, size_over_life=sizes((0.0, 1.0), (1.0, 0.6)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.5, 0.65), (1.0, 0.0))), 5)

# ------------------------------------------------------------------------------------- Oathblade Arc / Dawnbreaker
# The swing's crescent: flat, turned along the swing, opening out from the knight (pivot on the arc's centre).
define("KnightSlashArc", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=SLASH, render_mode="horizontal_billboard", align_to_direction=True, pivot=SLASH_PIVOT,
    color_intensity=2.6, size_over_life=sizes((0.0, 0.0), (0.07, 0.86), (0.4, 0.96), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.05, 1.0), (0.45, 0.75), (1.0, 0.0))), 4)
# The same crescent at full size from birth, fading: the crescent wave's afterimages and the Shield Rush bow wave.
define("KnightArcGhost", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=SLASH, render_mode="horizontal_billboard", align_to_direction=True, pivot=SLASH_PIVOT,
    color_intensity=2.2, size_over_life=sizes((0.0, 1.0), (1.0, 0.97)),
    color_gradient=fade((0.0, 0.7), (0.3, 0.5), (1.0, 0.0))), 3)
# Dawnbreaker's whirl: a blade swirl (twirl_01, its bright head leading) spinning about the knight, ring at 0.42 x
# the size.
define("KnightWhirl", shot(**ADD,
    max_particles=128, lifetime=[0.26, 0.3], texture=TWIRL, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], spin_speed=[700, 820], color_intensity=1.8,
    size_over_life=sizes((0.0, 0.0), (0.1, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.08, 0.9), (0.5, 0.55), (1.0, 0.0))), 4)
# Dawnbreaker's sunrise: shafts of dawn light standing up around the sweep (light_shaft, size = height); white-gold at
# the foot, amber above, rose where they fade. Untinted.
define("KnightSunRays", shot(**ADD, **SCRIPTED,
    max_particles=256, texture=SHAFT, render_mode="vertical_billboard", pivot=STAND, size_axes=[0.3, 1.0, 1.0],
    color_intensity=1.8, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (1.0, 0.9)),
    color_gradient=keys((0.0, [1.0, 0.95, 0.8, 0.0]), (0.12, [1.0, 0.9, 0.62, 0.9]), (0.55, [1.0, 0.7, 0.36, 0.6]),
                        (1.0, [1.0, 0.55, 0.42, 0.0]))), 6)
# Dawnbreaker's afterimage: the sweep's golden ring blooms a beat after the blade and widens as it fades (circle_03,
# ring at 0.36 x the size).
define("KnightAfterimage", shot(**ADD,
    max_particles=64, lifetime=[0.5, 0.5], texture=DOUBLE_RING, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.8, size_over_life=sizes((0.0, 1.0), (1.0, 1.15)),
    color_gradient=fade((0.0, 0.0), (0.3, 0.6), (1.0, 0.0))), 3)
# The crescent wave's light on the moor: a faint glow carrying particle lights, re-emitted at the blade's middle.
define("KnightWaveGlow", shot(**ADD, **light(3, 16.0, 6.0),
    max_particles=256, lifetime=[0.08, 0.08], texture=GLOW, render_mode="camera_facing", primitive="octagon",
    color_intensity=1.4, size_over_life=sizes((0.0, 1.0), (1.0, 0.8)), color_gradient=fade((0.0, 0.35), (1.0, 0.0))), 2)

# --------------------------------------------------------------------------------------- Aegis Orbit / Bastion Halo
# The shields' wake (head layer): a short gold comet tail that begins behind the shield - the newest particle is born
# at size 0 and peaks once the shield has moved on - so the shield mesh stays clear. Its particles light the moor.
define("KnightShieldWake", shot(**ADD, **light(6, 9.0, 3.5),
    max_particles=1024, lifetime=[0.13, 0.13], texture=GLOW, render_mode="camera_facing", primitive="octagon",
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.45, 1.0), (1.0, 0.3)),
    color_gradient=fade((0.0, 0.0), (0.3, 0.6), (1.0, 0.0))), 3)
# Bastion Halo: the two orbits drawn as thin rings of light at shield height, re-emitted every few frames so they
# follow the knight and breathe with the shields (size = 3.4 x the orbit radius).
define("KnightHaloRing", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=RING, render_mode="horizontal_billboard", primitive="octagon", color_intensity=1.2,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.0)), color_gradient=fade((0.0, 0.0), (0.35, 0.8), (0.65, 0.8), (1.0, 0.0))), 2)

# ------------------------------------------------------------------------------------------ Verdict / Final Verdict
# The holy seal under a coming sword (rune circle): opens fast, turns and holds through the telegraph.
define("KnightSigil", shot(**ADDITIVE_DECAL, **SCRIPTED,
    max_particles=64, texture=SIGIL, spin_speed=[45, 45], color_intensity=1.6,
    size_over_life=sizes((0.0, 0.0), (0.15, 0.94), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.12, 0.85), (0.85, 0.75), (1.0, 0.0))), -15)
# The seal burning under the embedded sword after the slam: flares, then cools away.
define("KnightSigilGlow", shot(**ADDITIVE_DECAL, **SCRIPTED,
    max_particles=64, texture=SIGIL, spin_speed=[20, 20], color_intensity=1.4,
    intensity_over_life=scalar((0.0, 1.6), (0.15, 1.0), (1.0, 0.6)), size_over_life=sizes((0.0, 1.0), (1.0, 1.06)),
    color_gradient=fade((0.0, 0.9), (0.3, 0.6), (1.0, 0.0))), -14)
# Rays of holy light standing up from an impact and spreading out (beam_4x1 tile kept at its 1:4 shape; size = height).
define("KnightRays", shot(**ADD, **BEAMS,
    max_particles=512, lifetime=[0.35, 0.5], render_mode="vertical_billboard", pivot=STAND, size_axes=[0.25, 1.0, 1.0],
    shape="circle", direction="outward", start_speed=[1.5, 5.0], drag=5.0, color_intensity=2.6,
    size_over_life=sizes((0.0, 0.0), (0.12, 1.0), (1.0, 0.75)),
    color_gradient=fade((0.0, 1.0), (0.3, 0.8), (1.0, 0.0))), 6)
# Final Verdict: the column of light from the sky over the mark (size = height). The script gives it the telegraph
# plus 0.2 s, so the impact falls at 64% of its life: it narrows and brightens onto the slam, then flares and goes.
define("KnightColumn", shot(**ADD, **SCRIPTED,
    max_particles=64, texture=SHAFT, render_mode="vertical_billboard", pivot=STAND, size_axes=[0.3, 1.0, 1.0],
    color_intensity=1.6, size_over_life=sizes((0.0, 1.0), (0.64, 0.75), (1.0, 1.1)),
    color_gradient=fade((0.0, 0.0), (0.25, 0.45), (0.64, 0.9), (1.0, 0.0))), 7)
# Its bright core, lighting the ground under it.
define("KnightColumnCore", shot(**ADD, **SCRIPTED, **light(2, 18.0, 8.0),
    max_particles=64, texture=SHAFT, render_mode="vertical_billboard", pivot=STAND, size_axes=[0.09, 1.0, 1.0],
    color_intensity=3.0, size_over_life=sizes((0.0, 1.0), (0.64, 0.8), (1.0, 1.15)),
    color_gradient=fade((0.0, 0.0), (0.3, 0.5), (0.64, 1.0), (1.0, 0.0))), 8)
# The sword's aura while it hovers, falls and stands embedded: a mote per frame thrown out of the hilt (head layer).
define("KnightSwordAura", shot(**ADD,
    max_particles=1024, lifetime=[0.5, 0.8], texture=MOTE, render_mode="camera_facing", shape="sphere",
    direction="outward", start_speed=[0.4, 1.6], drag=1.5, acceleration=[0.0, 1.2, 0.0], start_rotation=[0, 90],
    spin_speed=[60, 160], random_spin_direction=True, color_intensity=4.5, size_over_life=sizes((0.0, 1.0), (1.0, 0.15)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.8), (1.0, 0.0))), 7)
# Light streaming off a dissolving sword: launched mostly up and down the blade (a tall box), drifting up.
define("KnightBladeMotes", shot(**ADD,
    max_particles=512, lifetime=[0.6, 1.0], texture=MOTE, render_mode="camera_facing", shape="box",
    shape_scale=[0.12, 1.0, 0.12], direction="outward", start_speed=[2.0, 6.5], drag=4.5, acceleration=[0.0, 2.5, 0.0],
    start_rotation=[0, 90], spin_speed=[90, 200], random_spin_direction=True, color_intensity=5.0,
    size_over_life=sizes((0.0, 0.0), (0.1, 0.28, 0.42), (1.0, 0.04, 0.08)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (1.0, 0.0))), 7)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_knight.py")
