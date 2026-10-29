"""Knight class weapons, second set: Consecration / Eternal Sanctum and Dawn Hammer / Skybreaker. Holy gold and white
with pale steel; Eternal Sanctum adds a paler white-gold and a crown of candle flames, Skybreaker a white-azure storm.

    python build_effects_spells_knight_v2.py                 list the effects defined here (no editor call)
    python build_effects_spells_knight_v2.py build [group]   create missing effects and (re)apply their settings

Also fired by these weapons (defined elsewhere): KnightFlash, KnightGlint, KnightCrossFlare, KnightShockRing,
KnightRays, KnightMotes, KnightDust, KnightSteelSparks, KnightSweepStreak, KnightColumn, KnightColumnCore,
KnightSwordAura (build_effects_spells_knight.py), ScorchMark, MeteorRocks (build_effects_spells.py), SharedStormBolt,
SharedStormBoltGlow (build_effects_spells_shared.py).

How the scripts fire them:
- Tinted layers keep white gradients so the script's colour sets the hue (gold, holy white, white-azure).
- A size passed by a script is the particle's BIRTH size, or its PEAK when the curve starts at 0.
- Glyph decals live as long as the glyph (scripted lifetime): rune circle (outer ring at 0.46 x the size: size = 2.17 x
  the radius), compass star (size = 1.15 x the radius), ring edge (circle_02, line at 0.29 x the size: size = 3.4 x
  the radius), a faint light lifting the moor.
- Upright layers (shafts, flares, candles, bolts) stand on the ground (pivot at the bottom): their size is their height.
- Flat quads with align_to_direction turn their top along the (slow) velocity the script gives them: the hammer's
  whirl follows its spin that way.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_effects_spells import A, P, V, K, W, WHITE, ADD, DECAL, SCRIPTED, light, fade  # noqa: E402
from effect_helpers import Registry, shot, sizes, scalar, keys  # noqa: E402

GROUP = "KnightFx2"
REGISTRY = Registry((GROUP,))

GLOW = f"{P}/glow.png"
EMBER = f"{P}/ember.png"
SIGIL = f"{P}/rune_circle.png"
SHAFT = f"{P}/light_shaft.png"
MOTE = f"{K}/star_04.png"
COMPASS = f"{K}/magic_03.png"
RING = f"{K}/circle_02.png"
THIN_RING = f"{V}/ring_energy.png"
DOUBLE_RING = f"{K}/circle_03.png"
CANDLE_FLAME = f"{K}/flame_05.png"
WHIRL = f"{K}/twirl_01.png"
DIZZY = f"{K}/twirl_02.png"
SHOCK = f"{W}/shock_ring.png"
RUNES = {"texture": f"{V}/glyphs_4x4.png", "texture_sheet_tiles": [4, 4], "texture_sheet_randomize": True}
BOLTS = {"texture": f"{A}/bolts_4x1.png", "texture_sheet_tiles": [4, 1], "texture_sheet_randomize": True}
WEBS = {"texture": f"{A}/sparks_2x2.png", "texture_sheet_tiles": [2, 2], "texture_sheet_randomize": True}
ADDITIVE_DECAL = dict(DECAL, blend_mode="additive")
STAND = [0.5, 1.0]
UPRIGHT = {"render_mode": "vertical_billboard", "pivot": STAND}
TURNED_FLAT = {"render_mode": "horizontal_billboard", "align_to_direction": True, "start_rotation": [0, 0]}
EXACT = sizes((0.0, 1.0), (1.0, 1.0))
# A glyph decal opens in the first 5% of its life and holds; its light fades in its last tenth.
UNFOLD = sizes((0.0, 0.0), (0.05, 1.0), (1.0, 1.0))
FLICKER = scalar((0.0, 1.6), (0.25, 0.5), (0.45, 1.3), (0.7, 0.4), (1.0, 0.8))
CANDLE_FLICKER = scalar((0.0, 1.2), (0.3, 0.85), (0.55, 1.1), (0.8, 0.9), (1.0, 1.0))


def define(name, props, sort):
    REGISTRY.define(GROUP, name, props, sort)


# --------------------------------------------------------------------------------- Consecration / Eternal Sanctum
# The glyph's rune circle on the ground, turning slowly; it unfolds when planted and fades at the end of its life.
define("KnightConsecrateGlyph", shot(**ADDITIVE_DECAL, **SCRIPTED,
    max_particles=32, texture=SIGIL, spin_speed=[8, 8], color_intensity=1.5, size_over_life=UNFOLD,
    color_gradient=fade((0.0, 0.0), (0.04, 0.95), (0.15, 0.72), (0.88, 0.66), (1.0, 0.0))), -16)
# The holy compass star under the knight (magic_03 lying flat), turning the other way.
define("KnightConsecrateStar", shot(**ADDITIVE_DECAL, **SCRIPTED,
    max_particles=32, texture=COMPASS, spin_speed=[-6, -6], color_intensity=1.3, size_over_life=UNFOLD,
    color_gradient=fade((0.0, 0.0), (0.05, 0.75), (0.2, 0.5), (0.88, 0.45), (1.0, 0.0))), -15)
# The glyph's edge: a thin white-gold ring of light (ring_energy, line at 0.42 x the size) that bursts out to the
# radius and holds.
define("KnightConsecrateEdge", shot(**ADD, **SCRIPTED,
    max_particles=32, texture=THIN_RING, render_mode="horizontal_billboard", primitive="octagon", start_rotation=[0, 360],
    spin_speed=[10, 10], color_intensity=1.4, size_over_life=sizes((0.0, 0.0), (0.02, 0.4), (0.06, 1.0), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.03, 1.0), (0.2, 0.8), (0.9, 0.75), (1.0, 0.0))), -14)
# The glyph's light on the moor: a faint glow over its middle that carries a particle light.
define("KnightConsecrateLight", shot(**ADD, **SCRIPTED, **light(2, 8.0, 6.0),
    max_particles=32, texture=GLOW, render_mode="camera_facing", primitive="octagon", color_intensity=1.0,
    size_over_life=UNFOLD, color_gradient=fade((0.0, 0.0), (0.05, 0.14), (0.9, 0.12), (1.0, 0.0))), 1)
# Every tick the glyph breathes: a faint double ring opening to its edge.
define("KnightConsecratePulse", shot(**ADD,
    max_particles=64, lifetime=[0.45, 0.45], texture=DOUBLE_RING, render_mode="horizontal_billboard", primitive="octagon",
    start_rotation=[0, 360], color_intensity=1.2, size_over_life=sizes((0.0, 0.0), (0.2, 0.75), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.15, 0.45), (1.0, 0.0))), -13)
# Gold motes rising from the glyph (and around the knight standing on it), twinkling as they drift up.
define("KnightConsecrateMotes", shot(**ADD,
    max_particles=1024, lifetime=[1.2, 2.0], texture=MOTE, render_mode="camera_facing", shape="sphere", direction="up",
    direction_spread=20.0, start_speed=[0.5, 1.3], drag=0.5, acceleration=[0.0, 0.6, 0.0], turbulence_strength=0.8,
    start_rotation=[0, 90], spin_speed=[60, 160], random_spin_direction=True, color_intensity=4.0,
    size_over_life=sizes((0.0, 0.0), (0.15, 0.13, 0.22), (0.7, 0.1, 0.17), (1.0, 0.0)),
    color_gradient=fade((0.0, 0.0), (0.1, 1.0), (0.7, 0.8), (1.0, 0.0))), 8)
# Shafts of light standing up from the glyph and lifting away.
define("KnightConsecrateShaft", shot(**ADD, **UPRIGHT,
    max_particles=512, lifetime=[0.9, 1.3], texture=SHAFT, size_axes=[0.22, 1.0, 1.0], shape="sphere", direction="up",
    direction_spread=0.0, start_speed=[0.3, 0.7], color_intensity=2.0,
    size_over_life=sizes((0.0, 0.0), (0.25, 1.3, 2.0), (1.0, 1.5, 2.3)),
    color_gradient=fade((0.0, 0.0), (0.3, 0.7), (1.0, 0.0))), 6)
# Runes rising out of the glyph and fading.
define("KnightConsecrateRune", shot(**ADD, **RUNES,
    max_particles=256, lifetime=[1.2, 1.7], render_mode="camera_facing", shape="sphere", direction="up",
    direction_spread=10.0, start_speed=[0.5, 0.9], drag=0.3, start_rotation=[-12, 12], color_intensity=3.0,
    size_over_life=sizes((0.0, 0.0), (0.2, 0.42, 0.58), (1.0, 0.36, 0.5)),
    color_gradient=fade((0.0, 0.0), (0.2, 0.95), (0.7, 0.7), (1.0, 0.0))), 9)
# Judgement: a flicker of light rising from each enemy a tick hits (size = peak height).
define("KnightJudgeFlare", shot(**ADD, **UPRIGHT,
    max_particles=1024, lifetime=[0.3, 0.4], texture=SHAFT, size_axes=[0.16, 1.0, 1.0], color_intensity=2.2,
    size_over_life=sizes((0.0, 0.0), (0.2, 1.0), (1.0, 0.8)), color_gradient=fade((0.0, 0.0), (0.15, 0.9), (1.0, 0.0))), 7)
# Eternal Sanctum's candles along the edge: white-gold flames with amber tips, re-lit before they go out (size = peak
# height). Untinted.
define("KnightSanctumCandle", shot(**ADD, **UPRIGHT,
    max_particles=1024, lifetime=[0.3, 0.4], texture=CANDLE_FLAME, size_axes=[0.8, 1.0, 1.0], start_rotation=[-6, 6],
    color_intensity=2.0, intensity_over_life=CANDLE_FLICKER, size_over_life=sizes((0.0, 0.0), (0.15, 1.0), (0.6, 0.9), (1.0, 0.45)),
    color_gradient=keys((0.0, [1.0, 0.9, 0.62, 0.0]), (0.12, [1.0, 0.86, 0.55, 1.0]), (0.6, [1.0, 0.74, 0.4, 0.8]),
                        (1.0, [1.0, 0.64, 0.32, 0.0]))), 6)
# Eternal Sanctum's pulse: a wave opening from the middle to the edge (shock_ring, line at 0.47 x the size).
define("KnightSanctumWave", shot(**ADD, **SCRIPTED,
    max_particles=32, texture=SHOCK, render_mode="horizontal_billboard", primitive="octagon", start_rotation=[0, 360],
    color_intensity=2.0, size_over_life=sizes((0.0, 0.0), (0.1, 0.35), (0.6, 0.9), (1.0, 1.0)),
    color_gradient=fade((0.0, 0.0), (0.06, 1.0), (0.6, 0.7), (1.0, 0.0))), -12)

# ------------------------------------------------------------------------------------- Dawn Hammer / Skybreaker
# The golden whirl trailing the spin (twirl_01 lying flat, turned with the haft), re-drawn every frame.
define("KnightHammerSwirl", shot(**ADD, **SCRIPTED, **TURNED_FLAT,
    max_particles=512, texture=WHIRL, color_intensity=1.4, size_over_life=sizes((0.0, 1.0), (1.0, 1.05)),
    color_gradient=fade((0.0, 0.3), (1.0, 0.0))), 4)
# Lightning crackling over a Skybreaker head (head layer) and over the spot a thunderclap is coming down on.
define("KnightHammerCrackle", shot(**ADD, **WEBS,
    max_particles=1024, lifetime=[0.08, 0.12], render_mode="camera_facing", start_rotation=[0, 360], color_intensity=2.8,
    size_over_life=sizes((0.0, 1.0), (1.0, 1.15)), intensity_over_life=scalar((0.0, 1.4), (0.35, 0.5), (0.6, 1.2), (1.0, 0.4)),
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), 10)
# The daze over a stunned enemy: a small golden swirl turning over its head for the stun.
define("KnightDaze", shot(**ADD, **SCRIPTED,
    max_particles=512, texture=DIZZY, render_mode="horizontal_billboard", start_rotation=[0, 360], spin_speed=[540, 540],
    color_intensity=2.2, size_over_life=sizes((0.0, 0.0), (0.1, 1.0), (0.85, 1.0), (1.0, 0.6)),
    color_gradient=fade((0.0, 0.0), (0.1, 0.9), (0.8, 0.8), (1.0, 0.0))), 9)
# A thunderclap is coming: a ring closing in on the spot (birth = the clap's radius).
define("KnightThunderMark", shot(**ADD, **SCRIPTED,
    max_particles=128, texture=RING, render_mode="horizontal_billboard", primitive="octagon", start_rotation=[0, 360],
    color_intensity=2.4, size_over_life=sizes((0.0, 1.0), (1.0, 0.25)),
    color_gradient=fade((0.0, 0.2), (0.7, 1.0), (1.0, 0.9))), -11)
# The bolt from the sky: lightning strands standing on the clap (size = height), flickering.
define("KnightThunderBolt", shot(**ADD, **BOLTS, **UPRIGHT,
    max_particles=256, lifetime=[0.24, 0.3], size_axes=[0.3, 1.0, 1.0], start_rotation=[-5, 5], color_intensity=3.4,
    size_over_life=EXACT, intensity_over_life=FLICKER, color_gradient=fade((0.0, 1.0), (0.7, 0.85), (1.0, 0.0))), 12)
# The bolt's glowing column, lighting the moor around the clap.
define("KnightThunderGlow", shot(**ADD, **UPRIGHT, **light(2, 26.0, 9.0),
    max_particles=128, lifetime=[0.3, 0.3], texture=SHAFT, size_axes=[0.12, 1.0, 1.0], color_intensity=1.4,
    size_over_life=sizes((0.0, 1.0), (1.0, 0.9)), color_gradient=fade((0.0, 0.7), (0.3, 0.45), (1.0, 0.0))), 11)
# Lightning crawling over the ground where the clap struck.
define("KnightThunderWeb", shot(**ADD, **WEBS,
    max_particles=512, lifetime=[0.25, 0.35], render_mode="horizontal_billboard", start_rotation=[0, 360],
    color_intensity=2.0, size_over_life=sizes((0.0, 1.0), (1.0, 1.1)), intensity_over_life=FLICKER,
    color_gradient=fade((0.0, 1.0), (1.0, 0.0))), -8)
# Electric sparks thrown out of the clap, stretched along their flight.
define("KnightThunderSparks", shot(**ADD,
    max_particles=1024, lifetime=[0.2, 0.4], texture=EMBER, render_mode="stretched", shape="hemisphere",
    direction="outward", start_speed=[9.0, 17.0], gravity_scale=1.2, drag=2.5, color_intensity=7.0,
    stretch_length_scale=1.2, stretch_speed_scale=0.05, size_over_life=sizes((0.0, 0.06), (1.0, 0.02)),
    color_gradient=fade((0.0, 1.0), (0.6, 0.9), (1.0, 0.0))), 11)


if __name__ == "__main__":
    REGISTRY.main("build_effects_spells_knight_v2.py")
