"""Builds the Barrowlight sound library into survivors/src/Audio (16-bit PCM WAV, 44.1 kHz).

    python -P -E build_audio.py [group ...]     groups: see GROUPS at the bottom (default: all)

Every file goes through dsp.master: DC and rumble removed, silence trimmed, 3 ms / 5 ms fades, peak -1 dBFS, then
loudness brought to its category target. records.json collects length, loudness and sources for the manifest.
"""
import json
import os
import shutil
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402
import dsp  # noqa: E402
import flac  # noqa: E402
from dsp import (db, BELL_CHIME, BELL_CHURCH, BELL_CHURCH_MAJOR, BELL_GLASS, BREAK, GEM, KIMP, KJIN, KRPG, KSF,  # noqa: E402
                 KUI, R80, RATTLE, RPG, SP01, SWISH, S, T, bell, brass, brown, chime_run, choir, crackles, env,
                 flicker, glide, hz, karplus, loopable, mix, noise_boom, normalize, osc, reverb, saturate, strings,
                 sub_boom, swell, tl, white, whoosh)

RATE = al.RATE
SURV = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(SURV, "..", "data", "Audio")
RECORDS_PATH = os.path.join(SURV, "audio_work", "records.json")
RECORDS = {}
RND = dsp.RNG
SUB_LEVEL = -9.0


MAX_SECONDS = {"impact": 0.6, "impact_heavy": 1.3, "weapon": 0.75, "enemy": 2.3, "voice": 1.0, "stinger": 2.6, "pickup": 1.2,
               "ui": 0.8, "ui_tick": 0.08, "loop": 4.0, "ambience": 9.0}
MAX_SECONDS_BY_NAME = {"bell_toll": 3.5, "last_tide": 3.2, "results_victory": 2.6, "results_defeat": 2.6, "cataclysm_impact": 2.0,
                       "elite_death": 1.4, "chest_open": 1.8, "revive": 2.2, "level_up": 2.0, "evolve": 1.6, "run_start": 2.2,
                       "victory_stinger": 1.8, "defeat_stinger": 2.0, "reel_land_evolution": 1.2, "banner": 1.3, "banner_warning": 1.6,
                       "elite_horn": 2.0, "rush_rumble": 2.4, "ring_rattle": 2.2, "holy_bell": 2.4, "boss_bar_appear": 0.9,
                       "verdict_slam": 1.2, "verdict_shockwave": 0.9, "godspear_boom": 1.0, "leap_land": 1.2, "starfall_impact": 0.9,
                       "boss_rise": 2.7, "king_rise": 3.3, "archmage_summon_sting": 2.1, "king_raise_sting": 1.6, "king_roar": 1.9,
                       "king_enrage": 2.2, "boss_death": 2.2, "king_death": 3.6, "death_wave": 1.9, "gold_shower": 1.7,
                       "soul_nova_ring": 1.2, "king_slam_hit": 1.4, "king_cleave_hit": 1.0, "phase_shockwave": 1.2,
                       "boss_chest_drop": 1.0, "king_phase_break": 1.0}

def cap(y, seconds):
    """Cuts a sound to `seconds` with a cosine fade over its last 30% (at most 0.6 s)."""
    n = T(seconds)
    if len(y) <= n:
        return y
    y = y[:n].copy()
    fade = min(int(0.3 * n), T(0.6))
    ramp = 0.5 + 0.5 * np.cos(np.linspace(0.0, np.pi, fade))
    y[-fade:] *= ramp if y.ndim == 1 else ramp[:, None]
    return y


def out(folder, name, x, category, desc, variant=None, stereo=False, keep_edges=False, highpass=28.0):
    if not stereo and x.ndim > 1:
        x = x.mean(axis=1)
    y = dsp.master(x, category, highpass=highpass, keep_edges=keep_edges)
    if not keep_edges:
        y = cap(y, MAX_SECONDS_BY_NAME.get(name, MAX_SECONDS[category]))
    file_name = f"{name}_{variant}.wav" if variant is not None else f"{name}.wav"
    rel = f"{folder}/{file_name}"
    al.write_wav(os.path.join(OUT, folder, file_name), y)
    RECORDS[rel] = {"name": name, "file": rel, "seconds": round(len(y) / RATE, 3), "category": category,
                    "desc": desc, "sources": dsp.take_current_sources(), "channels": 2 if y.ndim > 1 else 1,
                    "peak_db": round(al.peak_db(y), 2), "loudness": round(al.loudness(y), 1)}
    print(f"  {rel:42s} {RECORDS[rel]['seconds']:5.2f}s pk {RECORDS[rel]['peak_db']:6.2f} L {RECORDS[rel]['loudness']:6.1f}")


def rattle_cloud(count, start, span, gains=(-10.0, -3.0), pitches=(0.85, 1.15), pan_span=0.0):
    layers = []
    for _ in range(count):
        r = S(RATTLE.format(int(RND.integers(0, 10))), pitch=float(RND.uniform(*pitches)))
        layers.append((r, start + float(RND.uniform(0.0, span)), float(RND.uniform(*gains)),
                       float(RND.uniform(-pan_span, pan_span))))
    return layers


def sub(*args, **kwargs):
    """A sub-bass thump layer (< 80 Hz) at SUB_LEVEL under a peak-normalized mix: felt, not dominating."""
    return normalize(sub_boom(*args, **kwargs)) * db(SUB_LEVEL)


def thump(seconds, f0, f1, tau, attack=0.002):
    t = tl(seconds)
    return osc(glide(t, f0, f1, 0.03), t) * env(t, 0.0, attack, tau)


def stab(notes, seconds, tau, cutoff=1600.0):
    t = tl(seconds)
    tone = sum(osc(hz(n), t, "saw") for n in notes)
    return al.filt(tone, "low", cutoff, order=2) * env(t, 0.0, 0.008, tau)


def crack(seconds, tau=0.02, hp=400.0):
    t = tl(seconds)
    return al.filt(white(seconds), "high", hp) * env(t, 0.0, 0.0008, tau)


# ============================================================================================ combat (core)

def combat():
    breaks = [1, 6, 8, 4, 3, 2]
    for i in range(6):
        punch = S(KIMP + f"impactPunch_medium_00{i % 5}.ogg", pitch=1.1 + 0.04 * (i % 3), length=0.17, fade=0.1)
        click = S(RATTLE.format(i), hp=1200, length=0.12, fade=0.06)
        brk = S(BREAK.format(breaks[i]), length=0.08, fade=0.05, hp=700)
        x = mix([(punch, 0, 0), (click, 0, -5), (brk, 0.004, -9)])
        out("combat", "hit", x, "impact", "bone click transient + punch body (60-170 ms) + bone crack", i + 1)
    for i in range(4):
        punch = S(KIMP + f"impactPunch_heavy_00{i}.ogg", pitch=1.1, length=0.2, fade=0.1)
        tick = S(KIMP + f"impactMetal_light_00{[0, 1, 2, 4][i]}.ogg", length=0.14, fade=0.08, hp=2500)
        ring = S(R80 + f"blade_0{[1, 2, 3, 2][i]}.ogg", length=0.25, fade=0.18, hp=2500)
        click = S(RATTLE.format(i + 5), hp=1200, length=0.1)
        x = mix([(punch, 0, 0), (tick, 0, -1), (ring, 0.006, -9), (click, 0, -7)])
        out("combat", "hit_crit", x, "impact", "sharp metal tick + steel ring tail + heavy punch body + bone click", i + 1)
    for i in range(6):
        brk = S(BREAK.format(breaks[i]), length=0.5, fade=0.25)
        low = S(KIMP + f"impactSoft_medium_00{i % 5}.ogg", length=0.15)
        r1 = S(RATTLE.format(i), pitch=0.95, length=0.15)
        r2 = S(RATTLE.format((i + 3) % 10), pitch=1.05, length=0.15)
        x = mix([(brk, 0, 0), (low, 0, -5), (r1, 0.03, -8), (r2, 0.09, -10)])
        out("combat", "enemy_death", x, "impact", "bone break crunch + low thump + two bone rattles (scatter)", i + 1)
    for i in range(2):
        b1 = S(BREAK.format([1, 6][i]), pitch=0.85, length=0.8, fade=0.4)
        b2 = S(BREAK.format([8, 4][i]), pitch=0.9, length=0.6, fade=0.3)
        die = S(R80 + "creature_die_01.ogg", pitch=0.9 + 0.08 * i, length=0.9, fade=0.4)
        boom = normalize(noise_boom(1.2, 0.002, 0.35, 2500.0, 120.0, 0.1))
        thud = sub(0.9, 70.0, 38.0, 0.3)
        coins = S(R80 + "item_coins_02.ogg", length=0.6, fade=0.3, hp=2000)
        x = mix([(b1, 0, 0), (b2, 0.05, -4), (die, 0.04, -8), (boom, 0, -5), (thud, 0, -4), (coins, 0.18, -14)], length=1.5)
        out("combat", "elite_death", reverb(x, 1.2, 0.3, 0.18), "impact_heavy",
            "two bone breaks (pitched down) + creature death groan + noise boom + sub < 80 Hz + coin glint", i + 1)
    for i in range(4):
        x = mix(rattle_cloud(3 + i % 2, 0.0, 0.18))
        out("enemies", "skeleton_rattle", x, "enemy", "3-4 bone rattles", i + 1)


# ============================================================================================ hero and core events

def hero():
    grunts = [1, 3, 4, 5]
    armors = ["inventory/chainmail1.wav", "inventory/armor-light.wav", "inventory/chainmail2.wav", "inventory/armor-light.wav"]
    for i in range(4):
        punch = S(KIMP + f"impactPunch_heavy_00{i}.ogg", length=0.3, fade=0.15)
        armor = S(RPG + armors[i], length=0.3, fade=0.15, hp=1500)
        grunt = S(RPG + f"NPC/giant/giant{grunts[i]}.wav", pitch=1.12, length=0.42, fade=0.2)
        x = mix([(punch, 0, -1), (armor, 0.005, -9), (grunt, 0.01, -2)])
        out("hero", "hero_hurt", x, "voice", "body punch + armor rattle + grunt (voice pitched up)", i + 1)
    x = mix([(S(SP01 + "Rise02.aif", length=1.6, fade=0.4), 0, -6),
             (normalize(choir(["D4", "F#4", "A4", "D5"], 2.2, attack=0.5, release=1.2)), 0.1, -4),
             (normalize(whoosh(0.9, 300.0, 4000.0, peak=0.85)), 0, -8),
             (normalize(chime_run(["D5", "A5", "D6"], 0.08, 1.6, decay_scale=0.8)), 0.85, -4),
             (sub(1.0, 80.0, 45.0, 0.4), 0.85, -4)], length=2.4)
    out("hero", "revive", reverb(x, 1.6, 0.5, 0.3), "stinger",
        "riser + D major choir swell + rising whoosh, then chime arpeggio and sub boom at 0.85 s")
    chime = chime_run(["D5", "F#5", "A5", "D6", "F#6"], 0.065, 1.6, pans=[-0.6, -0.3, 0.0, 0.3, 0.6], stereo=True)
    sparkle = normalize(S(GEM, mono=False))
    x = mix([(normalize(chime), 0, 0), (S(SP01 + "Rise03.aif", length=1.4, fade=0.4), 0, -10, 0.0),
             (normalize(choir(["D4", "A4", "D5", "F#5"], 1.8, attack=0.25, release=1.0)), 0.05, -8, 0.0),
             (sparkle, 0.3, -12)], length=2.0, stereo=True)
    out("hero", "level_up", reverb(x, 1.4, 0.45, 0.28), "stinger",
        "rising D major chime arpeggio panned L->R + riser + choir pad + gem sparkle", stereo=True)
    t = tl(0.5)
    beat = (osc(glide(t, 70.0, 48.0, 0.05), t) * env(t, 0.0, 0.004, 0.06)
            + 0.75 * osc(glide(t, 62.0, 42.0, 0.05), t) * env(t, 0.17, 0.004, 0.07))
    knock = al.filt(white(0.5), "low", 300.0) * (env(t, 0.0, 0.001, 0.01) + 0.7 * env(t, 0.17, 0.001, 0.01))
    out("hero", "heartbeat", saturate(beat + knock * 0.6, 1.5), "pickup", "lub-dub: two low sine thumps (70/62 Hz) with soft knocks",
        highpass=25.0)
    x = mix([(S(RPG + "battle/sword-unsheathe.wav", length=0.6, fade=0.2), 0, -3),
             (S(KIMP + "impactSoft_heavy_001.ogg"), 0.05, -4),
             (normalize(bell(hz("D3"), 2.5, BELL_CHURCH, decay_scale=0.7)), 0.05, -3),
             (sub(1.2, 70.0, 40.0, 0.45), 0.05, -5)], length=2.6)
    out("hero", "run_start", reverb(x, 1.8, 0.5, 0.25), "stinger", "sword drawn + drum + church bell (D3) + sub boom")
    grunt = S(RPG + "NPC/giant/giant2.wav", pitch=0.95, length=0.9, fade=0.4)
    x = mix([(S(KIMP + "impactPunch_heavy_002.ogg", pitch=0.75), 0, -2), (S(KIMP + "impactSoft_heavy_001.ogg"), 0, -4),
             (sub(0.7, 75.0, 40.0, 0.25), 0, -4), (grunt, 0.04, 0),
             (S(R80 + "creature_roar_01.ogg", pitch=1.2, length=0.6, fade=0.3), 0.05, -14)], length=1.0)
    out("barbarian", "fury_max", reverb(x, 0.9, 0.25, 0.15), "voice", "war drum hit + sub + war roar (voice) with a growl edge")
    x = mix([(normalize(whoosh(0.8, 400.0, 1800.0, q=0.8, peak=0.45)), 0, 0),
             (normalize(al.sweep_filter(white(0.8), "band", np.full(T(0.8), 1500.0), q=6.0) * swell(tl(0.8), 0.3, 0.4)), 0, -10)])
    out("rogue", "momentum_max", x, "weapon", "wind gust: wide band whoosh + faint whistle")
    x = chime_run(["A5", "D6"], 0.07, 0.4, decay_scale=0.4)
    out("ui", "active_ready", x, "ui", "soft two-note chime (A5, D6)")
    x = mix([(S(KUI + "maximize_009.ogg"), 0, -3), (normalize(chime_run(["D6", "A6"], 0.05, 0.6, decay_scale=0.5)), 0.03, -2),
             (normalize(whoosh(0.35, 800.0, 5000.0, peak=0.7)), 0, -8)])
    out("ui", "card_pick", x, "ui", "bright confirm blip + D6/A6 chime + upward whoosh")
    x = mix([(S(R80 + "lock_02.ogg", length=0.35), 0, -3), (S(KRPG + "metalLatch.ogg", length=0.25), 0.02, -6),
             (S(KRPG + "doorOpen_1.ogg", pitch=1.15, length=0.7, fade=0.3), 0.08, -7),
             (S(KRPG + "handleCoins.ogg", length=0.8, fade=0.3), 0.25, -8),
             (normalize(chime_run(["G5", "B5", "D6", "G6"], 0.06, 1.1, decay_scale=0.8)), 0.32, -5),
             (normalize(choir(["G4", "B4", "D5"], 1.2, attack=0.2, release=0.8)), 0.3, -12)], length=1.6)
    out("pickups", "chest_open", reverb(x, 1.2, 0.4, 0.22), "stinger",
        "lock click + latch + lid creak + coins + G major chime run + choir")
    pad = choir(["D3", "A3", "D4", "F#4", "A4"], 1.6, attack=0.08, release=1.0)
    horns = brass(["D3", "A3", "D4"], 1.4, attack=0.05, release=0.8, bright=1800.0)
    x = mix([(normalize(horns), 0, -3, -0.3), (normalize(pad), 0.02, -4, 0.3),
             (normalize(bell(hz("D4"), 1.6, BELL_CHURCH_MAJOR, decay_scale=0.6)), 0, -6, 0.0),
             (sub(0.8, 80.0, 45.0, 0.3), 0, -6, 0.0),
             (normalize(chime_run(["D5", "F#5", "A5", "D6"], 0.05, 1.4, stereo=True, pans=[-0.5, -0.2, 0.2, 0.5])), 0.1, -8)],
            length=1.8, stereo=True)
    out("hero", "evolve", reverb(x, 1.6, 0.5, 0.3), "stinger", "D major brass chord + choir burst + major bell + chime run + sub",
        stereo=True)


# ============================================================================================ pickups

def pickups():
    x = mix([(normalize(bell(hz("A5"), 0.3, BELL_GLASS, decay_scale=0.6, strike=0.1)), 0, 0),
             (normalize(bell(hz("E6"), 0.25, BELL_GLASS, decay_scale=0.4, strike=0.0)), 0, -12),
             (S(GEM, hp=5000, length=0.19), 0, -12)])
    out("pickups", "gem", x, "pickup", "glass ting at A5 (+ fifth) + gem sparkle; the code climbs a semitone per chained gem")
    coins = [R80 + "item_coins_01.ogg", R80 + "item_coins_03.ogg", RPG + "inventory/coin3.wav"]
    for i, rel in enumerate(coins):
        out("pickups", "coin", S(rel, length=0.35, fade=0.15, hp=400), "pickup", "coin clink", i + 1)
    x = mix([(normalize(chime_run(["C5", "E5", "G5", "C6"], 0.07, 0.9, decay_scale=0.7)), 0.03, 0),
             (S(RPG + "inventory/bubble2.wav", length=0.35, fade=0.15), 0, -9),
             (normalize(choir(["C4", "E4", "G4"], 0.9, attack=0.15, release=0.6, vowel="o")), 0, -12)], length=1.0)
    out("pickups", "heal", reverb(x, 0.9, 0.3, 0.2), "pickup", "warm C major chime run + gulp + soft 'oh' choir")
    x = mix([(S(KSF + "forceField_000.ogg", length=0.9, fade=0.3), 0, -3),
             (S(SP01 + "Rise03.aif", pitch=1.25, length=1.0, fade=0.3), 0, -8),
             (normalize(whoosh(1.0, 250.0, 3500.0, peak=0.8)), 0, -6)])
    out("pickups", "lodestone", x, "stinger", "magnetic hum + riser + rising whoosh")
    x = mix([(S(KIMP + "impactBell_heavy_001.ogg", pitch=0.75, length=2.2, fade=0.8), 0, -2),
             (normalize(bell(hz("D3"), 3.0, BELL_CHURCH_MAJOR, decay_scale=0.8)), 0, -3),
             (normalize(whoosh(0.7, 2500.0, 200.0, peak=0.1)), 0.02, -6),
             (sub(1.2, 65.0, 40.0, 0.5), 0, -4)], length=3.0)
    out("pickups", "holy_bell", reverb(x, 2.0, 0.7, 0.25), "stinger", "heavy bell (pitched down) + synth church bell D3 + shock whoosh + sub")
    x = mix([(normalize(whoosh(0.45, 600.0, 5000.0, peak=0.95)), 0, -6),
             (S(KUI + "glass_004.ogg", length=0.7, fade=0.3), 0.4, -2),
             (S(KIMP + "impactGlass_medium_001.ogg", length=0.5), 0.4, -5),
             (normalize(crackles(1.3, 0.05, 1.1, 120.0, 2500.0, 9000.0, decay=0.002, density_tau=0.5)), 0.42, -8),
             (normalize(chime_run(["B5", "F#6", "B6"], 0.05, 1.2, BELL_GLASS, decay_scale=1.5)), 0.45, -8),
             (sub(0.8, 60.0, 40.0, 0.3), 0.4, -8)], length=1.7)
    out("pickups", "frost_hourglass", reverb(x, 1.5, 0.5, 0.3), "stinger",
        "inhaling whoosh, then glass hit + ice crackle + glass chimes + soft sub")
    x = mix([(S(KIMP + "impactGlass_heavy_001.ogg"), 0, 0), (S(KUI + "glass_004.ogg", length=0.6, fade=0.3), 0.01, -9),
             (normalize(noise_boom(0.6, 0.004, 0.18, 3000.0, 300.0, 0.08)), 0, -6),
             (normalize(crackles(0.8, 0.05, 0.7, 40.0, 1200.0, 5000.0, density_tau=0.3)), 0.05, -12)])
    out("pickups", "lantern_break", x, "impact", "glass smash + glass ring + fire puff + crackles")


# ============================================================================================ director events

def director():
    pans = np.linspace(-1.0, 1.0, 10).tolist() + np.linspace(1.0, -1.0, 10).tolist()
    layers = []
    for k in range(20):
        r = S(RATTLE.format(k % 10), pitch=float(RND.uniform(0.8, 1.15)))
        layers.append((r, 0.07 * k + float(RND.uniform(0, 0.04)), float(RND.uniform(-8, -2)), pans[k]))
    layers.append((normalize(choir(["D2", "A2"], 1.8, attack=0.5, release=0.8, vowel="u")), 0, -8, 0.0))
    out("director", "ring_rattle", reverb(mix(layers, length=2.0, stereo=True), 1.2, 0.35, 0.25), "stinger",
        "20 bone rattles circling L->R->L + low 'oo' drone", stereo=True)
    t = tl(3.6)
    x = mix([(normalize(choir(["D3", "F3", "A3", "D4"], 3.6, attack=1.2, release=1.4)), 0, -3),
             (normalize(choir(["D2"], 3.6, attack=1.0, release=1.4, vowel="u")), 0, -8),
             (normalize(bell(hz("D2"), 3.0, BELL_CHURCH, decay_scale=1.0)), 0.9, -2),
             (sub(2.0, 55.0, 35.0, 0.9), 0.9, -4),
             (brown(3.6, 90.0) * swell(t, 1.0, 1.5), 0, -10)], length=3.8)
    out("director", "last_tide", reverb(x, 2.2, 0.7, 0.3), "stinger", "D minor choir swell + low drone + bell toll (D2) + sub + rumble")
    x = mix([(normalize(bell(hz("D2"), 4.5, BELL_CHURCH, decay_scale=1.3)), 0, 0),
             (S(KIMP + "impactBell_heavy_000.ogg", pitch=0.5, length=2.8, fade=1.0), 0, -5)], length=4.6)
    out("director", "bell_toll", reverb(x, 2.5, 0.9, 0.3), "stinger", "deep church bell (D2, synth) + heavy bell pitched down an octave")
    t = tl(2.4)
    engine = S(KSF + "spaceEngineLow_000.ogg", start=0.5, end=3.0, length=2.4, fade=0.6)
    layers = [(engine * np.clip(t[:len(engine)] / 1.6, 0.15, 1.0), 0, -2)]
    layers += [(S(KIMP + f"impactSoft_heavy_00{k}.ogg"), at, g) for k, at, g in ((0, 0.0, -6), (1, 0.55, -5), (2, 1.1, -4), (3, 1.55, -3))]
    layers += [(lay[0], lay[1], lay[2]) for lay in rattle_cloud(16, 0.2, 1.9, gains=(-14, -8))]
    out("director", "rush_rumble", mix(layers, length=2.5), "stinger", "rising low rumble + four drum hits + a crowd of bone rattles")
    t = tl(1.7)
    horn = brass(["D2", "A2", "D3"], 1.7, attack=0.12, release=0.45, bright=1300.0)
    x = mix([(normalize(horn), 0, 0), (S(R80 + "creature_roar_02.ogg", pitch=0.7, length=1.3, fade=0.5), 0.05, -10),
             (S(KIMP + "impactSoft_heavy_000.ogg"), 0, -5)], length=2.0)
    out("director", "elite_horn", reverb(x, 1.6, 0.5, 0.25), "stinger", "war horn (D2/A2/D3 brass) + low roar + drum")


# ============================================================================================ enemies

def enemies():
    t = tl(0.85)
    hiss = S(KSF + "thrusterFire_002.ogg", start=1.0, end=2.0, length=0.85, fade=0.1, hp=2500)
    hiss = hiss * (t[:len(hiss)] / 0.85) ** 0.7
    whistle = osc(np.linspace(600.0, 1700.0, len(t)), t) * swell(t, 0.6, 0.05) * 0.15
    x = mix([(hiss, 0, 0), (normalize(crackles(0.85, 0.0, 0.8, 90.0, 1500.0, 7000.0, decay=0.002)), 0, -6), (whistle, 0, -6)])
    out("enemies", "cinder_fuse", x, "enemy", "fuse sizzle growing over 0.8 s + crackles + rising whistle")
    for i in range(3):
        x = mix([(S(KSF + f"explosionCrunch_00{i}.ogg", length=0.9, fade=0.4), 0, -2),
                 (normalize(noise_boom(0.9, 0.002, 0.25, 3000.0, 150.0, 0.08)), 0, -3),
                 (sub(0.5, 75.0, 40.0, 0.18), 0, -4),
                 (normalize(crackles(0.9, 0.05, 0.8, 50.0, 1200.0, 6000.0, density_tau=0.3)), 0.05, -10)])
        out("enemies", "cinder_blast", x, "impact", "explosion crunch + noise boom + sub + fire crackles", i + 1)
    for i in range(2):
        x = mix([(S(R80 + ["stones_03.ogg", "stones_01.ogg"][i], length=0.6, fade=0.25), 0, -2),
                 (S(KIMP + f"impactSoft_heavy_00{i + 2}.ogg", length=0.4), 0, -2),
                 (normalize(whoosh(0.5, 200.0, 1500.0, peak=0.2)), 0, -8)] + [lay[:3] for lay in rattle_cloud(3, 0.04, 0.15)])
        out("enemies", "grave_burst", x, "enemy", "earth burst: stones + ground thud + dark whoosh + rattles", i + 1)
    for i in range(2):
        x = mix([(S(R80 + "spell_02.ogg", pitch=0.7 + 0.08 * i, length=0.6, fade=0.25), 0, 0),
                 (normalize(whoosh(0.5, 1500.0, 300.0, peak=0.25)), 0, -6)])
        out("enemies", "hex_orb_cast", x, "enemy", "dark spell (pitched down) + falling whoosh", i + 1)
    for i in range(2):
        x = mix([(S(KRPG + "creak3.ogg", pitch=1.2 + 0.1 * i), 0, 0), (S(KRPG + "cloth2.ogg", length=0.3), 0, -12)])
        out("enemies", "bolt_draw", x, "enemy", "bow string creak + cloth", i + 1)
    for i in range(3):
        twang = karplus(hz("G2") * [1.0, 1.06, 0.94][i], 0.3, damping=0.992, brightness=0.6)
        x = mix([(normalize(twang), 0, -4), (S(SWISH.format([3, 7, 9][i]), hp=600), 0.005, 0)])
        out("enemies", "bolt_fire", x, "enemy", "bow string twang (plucked string) + arrow swish", i + 1)
    t = tl(2.2)
    x = mix([(S(RPG + "NPC/shade/shade9.wav", pitch=0.75, length=1.9, fade=0.5), 0, -5),
             (normalize(choir(["D3", "F3", "G#3"], 2.2, attack=1.6, release=0.4, vowel="u")), 0, -3),
             (normalize(whoosh(2.1, 200.0, 1800.0, peak=0.92, sharpness=2.5)), 0, -6)])
    out("enemies", "hex_cast", x, "enemy", "ghostly voice (pitched down) + diminished 'oo' choir crescendo + rising whoosh into the 2.1 s hit")
    x = mix([(S(RPG + "NPC/shade/shade12.wav", pitch=0.7, length=1.6, fade=0.4), 0, -4),
             (normalize(choir(["A2", "C3", "E3"], 1.7, attack=0.6, release=0.6, vowel="o")), 0, -4)]
            + [lay[:3] for lay in rattle_cloud(5, 1.15, 0.45)])
    out("enemies", "raise_chant", x, "enemy", "ghostly voice + low A minor 'oh' chant + rattles as the dead rise")
    for i in range(2):
        x = mix([(S(R80 + ["stones_02.ogg", "stones_04.ogg"][i], length=0.5, fade=0.2), 0, -3),
                 (S(KIMP + f"impactSoft_heavy_00{i}.ogg", length=0.4), 0, -3),
                 (S(KSF + "forceField_003.ogg", pitch=0.6, length=0.8, fade=0.3), 0, -10),
                 (normalize(whoosh(0.6, 250.0, 2000.0, peak=0.3)), 0, -8)] + [lay[:3] for lay in rattle_cloud(4, 0.05, 0.3)])
        out("enemies", "summon", x, "enemy", "dirt burst + thud + low magic hum + rising whoosh + rattles", i + 1)
    x = mix([(S(KSF + "forceField_002.ogg", length=0.8, fade=0.3), 0, -2),
             (normalize(bell(hz("G#5"), 0.8, BELL_GLASS, decay_scale=2.0) + bell(hz("A5"), 0.8, BELL_GLASS, decay_scale=2.0)), 0, -9)])
    out("enemies", "ward", x, "enemy", "force field hum + dissonant glass dyad (G#5 + A5)")


# ============================================================================================ boss

def boss():
    """manifest_bosses.md section 2: every boss sound is flat, big and readable; stings are unmistakable."""
    t = tl(2.7)
    x = mix([(normalize(brown(2.7, 70.0) * swell(t, 1.4, 0.8)), 0, 0),
             (S(BREAK.format(1), pitch=0.8, length=0.5, fade=0.2), 0.6, -4), (S(BREAK.format(6), pitch=0.75, length=0.5, fade=0.2), 1.1, -3),
             (S(BREAK.format(8), pitch=0.85, length=0.5, fade=0.2), 1.6, -5), (S(R80 + "stones_03.ogg", pitch=0.8, length=1.0, fade=0.4), 0.9, -6),
             (al.filt(S(RPG + "NPC/shade/shade12.wav", pitch=0.6, length=1.6, fade=0.6), "low", 1500.0), 0.8, -6),
             (sub(1.0, 50.0, 30.0, 0.45), 1.6, -5)], length=2.7)
    out("boss", "boss_rise", reverb(x, 1.4, 0.5, 0.3), "stinger", "deep ground rumble crescendo + three bone cracks + stones + distant moan (ghost voice, low-passed) + sub")
    t = tl(3.3)
    layers = [(normalize(brown(3.3, 60.0) * swell(t, 2.6, 0.5)), 0, 0),
              (normalize(choir(["D2", "A2", "D3"], 3.3, attack=2.4, release=0.6, vowel="u")), 0, -4),
              (S(KIMP + "impactMining_000.ogg", pitch=0.6), 1.2, -5), (S(KIMP + "impactMining_003.ogg", pitch=0.55), 2.0, -3),
              (S(KIMP + "impactMining_001.ogg", pitch=0.5), 2.7, 0), (S(R80 + "stones_03.ogg", pitch=0.7, length=1.2, fade=0.5), 1.5, -6),
              (sub(0.8, 55.0, 30.0, 0.35), 2.7, -3)]
    layers += [lay[:3] for lay in rattle_cloud(18, 0.5, 2.6, gains=(-16.0, -9.0), pitches=(0.5, 0.7))]
    out("boss", "king_rise", reverb(mix(layers, length=3.4), 1.6, 0.6, 0.3), "stinger",
        "rumble crescendo + stone cracking (pitched down) + low 'oo' choir swell + bone grinding (slow rattles) + sub")
    for i in range(2):
        t = tl(0.8)
        grind = al.band(white(0.8), 400.0, 2500.0) * swell(t, 0.2, 0.2) * flicker(t, 15.0, 40.0, 5, 0.8)
        x = mix([(S(RPG + f"battle/sword-unsheathe{[2, 4][i]}.wav", pitch=0.45, length=0.8, fade=0.25), 0, 0), (normalize(grind), 0, -8),
                 (S(R80 + f"creature_monster_0{[3, 4][i]}.ogg", pitch=0.7, length=0.8, fade=0.3), 0.05, -3)])
        out("boss", "warden_charge_sting", x, "stinger", "shield scraping on stone (steel slowed + grind noise) + low war growl", i + 1)
    layers = [(S(KRPG + f"footstep0{n}.ogg", pitch=0.7), at, -2) for n, at in ((0, 0.0), (3, 0.11), (7, 0.22))]
    layers += [(S(KIMP + "impactSoft_heavy_001.ogg", length=0.3, fade=0.15), 0, -4), (normalize(whoosh(0.5, 300.0, 1500.0, peak=0.35)), 0, -4)]
    out("boss", "warden_charge_go", mix(layers), "stinger", "burst of heavy footfalls (slowed) + ground thud + whoosh")
    for i in range(2):
        x = mix([(S(KIMP + f"impactPlate_heavy_00{[0, 2][i]}.ogg"), 0, 0), (S(KIMP + f"impactPunch_heavy_00{[1, 3][i]}.ogg"), 0, -2),
                 (sub(0.5, 85.0, 42.0, 0.18), 0, -3)], length=0.55)
        out("boss", "warden_charge_hit", x, "impact_heavy", "metal shield clang + body thud + sub", i + 1)
    x = mix([(normalize(noise_boom(0.7, 0.003, 0.2, 2500.0, 150.0, 0.08)), 0, 0), (sub(0.6, 70.0, 36.0, 0.25), 0, -3),
             (S(R80 + "stones_04.ogg", length=0.6, fade=0.25), 0, -4), (S(R80 + "stones_02.ogg", length=0.6, fade=0.25), 0.08, -7)], length=0.75)
    out("boss", "warden_charge_stop", x, "impact_heavy", "boom + sub + gravel")
    for i in range(2):
        x = mix([(S(RPG + f"battle/sword-unsheathe{[4, 5][i]}.wav", pitch=0.8, length=0.6, fade=0.2), 0, 0),
                 (normalize(whoosh(0.6, 400.0, 3000.0, peak=0.9)), 0, -3)])
        out("boss", "warden_cleave_sting", x, "stinger", "blade scrape + whoosh rising into the swing", i + 1)
    for i in range(2):
        x = mix([(S(KIMP + f"impactPunch_heavy_00{[0, 4][i]}.ogg"), 0, 0), (S(R80 + f"stones_0{[1, 3][i]}.ogg", length=0.4, fade=0.2), 0.01, -4),
                 (S(KIMP + f"impactMetal_medium_00{[3, 0][i]}.ogg", length=0.4, fade=0.2), 0, -6), (sub(0.4, 75.0, 40.0, 0.15), 0, -4)])
        out("boss", "warden_cleave_hit", x, "impact_heavy", "thud + earth crunch + metal ring + sub", i + 1)
    x = mix([(S(KIMP + "impactPlate_medium_002.ogg"), 0, 0), (S(KRPG + "creak1.ogg", pitch=0.8, length=0.4, fade=0.2), 0.03, -6),
             (S(RPG + "inventory/chainmail2.wav", length=0.3, fade=0.15), 0, -10)])
    out("boss", "warden_guard_up", x, "stinger", "metal clank + bone creak + chain rattle")
    for i in range(4):
        out("boss", "guard_tink", S(KIMP + f"impactMetal_light_00{[0, 1, 3, 4][i]}.ogg", pitch=1.6, length=0.15, fade=0.08), "impact",
            "bright metal tink (light metal hit pitched up)", i + 1)
    t = tl(1.0)
    ring = (np.sin(2 * np.pi * 3150.0 * t) + 0.4 * np.sin(2 * np.pi * 4420.0 * t)) * env(t, 0.0, 0.02, 0.45)
    chirps = sum(osc(glide(t, 4200.0, 2500.0, 0.03, at), t) * env(t, at, 0.003, 0.03) for at in (0.15, 0.4, 0.62))
    x = mix([(normalize(ring), 0, -2), (normalize(chirps), 0, -4), (normalize(bell(hz("A6"), 1.0, BELL_CHIME, decay_scale=0.8)), 0, -8)])
    out("boss", "boss_dazed", x, "enemy", "ringing tinnitus tone (3.15 kHz) + three falling chirps + small chime")
    t = tl(0.7)
    hum = al.sweep_filter(osc(hz("D3"), t, "saw") + osc(hz("G#3"), t, "saw"), "low", 600.0 + 1600.0 * t / 0.7) * swell(t, 0.55, 0.08)
    whisper = al.band(white(0.7), 1500.0, 6000.0) * flicker(t, 8.0, 16.0, 5, 0.9) * swell(t, 0.3, 0.15)
    x = mix([(normalize(hum), 0, 0), (S(RPG + "NPC/shade/shade3.wav", pitch=1.4, length=0.6, fade=0.2), 0.05, -6), (normalize(whisper), 0, -8)])
    out("boss", "archmage_spiral_sting", x, "stinger", "violet hum swelling (D3 + G#3 saws, filter opening) + whispered ghost syllables")
    for i in range(3):
        t = tl(0.25)
        pop = osc(glide(t, [700.0, 800.0, 600.0][i], 280.0, 0.02), t) * env(t, 0.0, 0.002, 0.05)
        x = mix([(normalize(pop), 0, 0), (normalize(noise_boom(0.25, 0.003, 0.05, 4000.0, 500.0, 0.03)), 0, -6),
                 (normalize(bell(hz(["F#6", "G6", "A6"][i]), 0.25, BELL_GLASS, decay_scale=0.6)), 0, -12)])
        out("boss", "archmage_orb", x, "enemy", "soft magic pop (falling sine) + small air puff + faint glass tone", i + 1)
    t = tl(0.8)
    x = mix([(normalize(al.band(white(0.8), 1500.0, 6000.0) * flicker(t, 6.0, 14.0, 5, 0.9) * swell(t, 0.3, 0.1)), 0, -3),
             (normalize(whoosh(0.8, 1500.0, 6000.0, peak=0.95)), 0, 0), (normalize(brown(0.8, 80.0) * swell(t, 0.4, 0.1)), 0, -6)])
    out("boss", "archmage_field_sting", x, "stinger", "earth whispers (flickering breath) + grave hiss rising + low rumble")
    t = tl(2.0)
    chant = choir(["A2", "C3", "E3"], 2.0, attack=0.4, release=0.5, vowel="o") * (0.55 + 0.45 * np.abs(np.sin(2 * np.pi * 1.1 * t)))
    x = mix([(normalize(chant), 0, 0), (normalize(choir(["A1", "A2"], 2.0, attack=0.6, release=0.5, vowel="u")), 0, -6),
             (S(RPG + "NPC/shade/shade12.wav", pitch=0.6, length=1.8, fade=0.6), 0.1, -8)])
    out("boss", "archmage_summon_sting", reverb(x, 1.2, 0.4, 0.25), "stinger", "dark low chant (A minor 'oh' voices pulsing in syllables) + bass voices + ghost voice")
    for i in range(2):
        x = mix([(normalize(whoosh(0.4, 4000.0, 500.0, peak=0.3)), 0, 0),
                 (normalize(bell(hz(["C#7", "D7"][i]), 0.6, BELL_GLASS, decay_scale=1.2)), 0, -4), (S(KUI + f"glass_00{[2, 5][i]}.ogg"), 0, -12)])
        out("boss", "archmage_blink", x, "enemy", "falling whoosh + crystalline chime", i + 1)
    for i in range(2):
        t = tl(0.8)
        grind = al.band(white(0.8), 300.0, 1800.0) * swell(t, 0.25, 0.2) * flicker(t, 10.0, 30.0, 5, 0.8)
        x = mix([(S(RPG + f"battle/sword-unsheathe{["3", ""][i]}.wav", pitch=0.35, length=0.8, fade=0.25), 0, 0),
                 (S(R80 + f"chain_0{[3, 1][i]}.ogg", pitch=0.5, length=0.7, fade=0.3), 0.05, -8), (normalize(grind), 0, -6)])
        out("boss", "king_cleave_sting", x, "stinger", "huge axe scraped across stone: steel slowed far down + chains + stone grind", i + 1)
    for i in range(2):
        x = mix([(normalize(whoosh(0.45, 1200.0, 250.0, q=0.8, peak=0.5)), 0, -2), (S(KIMP + f"impactPunch_heavy_00{[2, 0][i]}.ogg", pitch=0.7), 0.3, 0),
                 (sub(0.5, 60.0, 32.0, 0.25), 0.3, -2), (S(R80 + "stones_02.ogg", length=0.4, fade=0.2), 0.32, -8)], length=0.9)
        out("boss", "king_cleave_hit", x, "impact_heavy", "giant whoosh, then impact thud + sub < 80 Hz + grit", i + 1)
    t = tl(0.9)
    x = mix([(normalize(whoosh(0.9, 250.0, 1600.0, q=1.5, peak=0.9, sharpness=1.5)), 0, 0),
             (S(R80 + "creature_monster_03.ogg", pitch=0.55, length=0.9, fade=0.3), 0, -10)]
            + [lay[:3] for lay in rattle_cloud(10, 0.1, 0.7, gains=(-14.0, -8.0), pitches=(0.6, 0.8))])
    out("boss", "king_slam_sting", x, "stinger", "deep giant inhale (rising band noise) + low growl + rattling ribs")
    x = mix([(S(KIMP + "impactPunch_heavy_000.ogg", pitch=0.7), 0, 0), (S(KIMP + "impactMining_000.ogg", pitch=0.8), 0.005, -4),
             (S(R80 + "stones_03.ogg", length=1.0, fade=0.4), 0.05, -8), (normalize(noise_boom(1.3, 0.002, 0.4, 2500.0, 100.0, 0.12)), 0, -3),
             (sub(1.1, 55.0, 30.0, 0.45), 0, -2)], length=1.3)
    out("boss", "king_slam_hit", reverb(x, 1.2, 0.4, 0.18), "impact_heavy", "heavy punch + stone crack + air boom + sub < 60 Hz + debris tail")
    for i in range(3):
        x = mix([(S(BREAK.format([2, 4, 9][i]), length=0.15, fade=0.08, hp=600), 0, 0), (normalize(crack(0.3, 0.01)), 0, -4),
                 (normalize(crackles(0.3, 0.0, 0.12, 200.0, 1500.0, 8000.0, decay=0.002)), 0, -6), (S(R80 + "stones_04.ogg", length=0.15, fade=0.08), 0, -10)])
        out("boss", "bone_spike", x, "impact", "bone snap + crack + crackle + stone tick", i + 1)
    t = tl(0.9)
    whistle = (osc(np.geomspace(2400.0, 900.0, len(t)), t) * 0.5 + al.sweep_filter(white(0.9), "band", np.geomspace(2400.0, 900.0, len(t)), q=3.0) * 2.0) * swell(t, 0.3, 0.1)
    x = mix([(normalize(whistle), 0, -5)] + [lay[:3] for lay in rattle_cloud(16, 0.0, 0.75, gains=(-12.0, -4.0))])
    out("boss", "king_rain_sting", x, "stinger", "many bones clattering + falling whistle")
    breaks = [3, 5, 7, 10]
    for i in range(4):
        x = mix([(S(BREAK.format(breaks[i]), length=0.3, fade=0.15), 0, 0), (S(KIMP + f"impactSoft_medium_00{i}.ogg", length=0.15), 0, -6)]
                + [lay[:3] for lay in rattle_cloud(2, 0.03, 0.08, gains=(-8.0, -5.0))])
        out("boss", "bone_rain_impact", x, "impact", "bone shatter + soft thud + scattering rattles", i + 1)
    x = mix([(normalize(choir(["D2", "A2", "D3"], 1.5, attack=0.6, release=0.5, vowel="u")), 0, 0),
             (S(R80 + "creature_monster_04.ogg", pitch=0.5, length=1.4, fade=0.5), 0.05, -6), (S(R80 + "stones_01.ogg", pitch=0.7, length=1.0, fade=0.4), 0.3, -10)])
    out("boss", "king_raise_sting", reverb(x, 1.0, 0.35, 0.2), "stinger", "low 'oo' choir + earth groan (growl far down) + shifting stones")
    t = tl(0.7)
    rate = np.cumsum(4.0 + 10.0 * t / 0.7) / RATE
    spin = 0.35 + 0.65 * np.abs(np.sin(2 * np.pi * rate))
    x = al.sweep_filter(white(0.7), "band", 300.0 * (2500.0 / 300.0) ** (t / 0.7), q=1.2) * spin * swell(t, 0.5, 0.06)
    out("boss", "king_spin_sting", x, "stinger", "whoosh rising in pitch while its spin speeds up (4 to 14 passes per second)")
    for i in range(3):
        x = mix([(S(SWISH.format([3, 7, 9][i]), pitch=0.5), 0, 0), (normalize(whoosh(0.45, 900.0, 200.0, q=0.8, peak=0.45)), 0, -2)])
        out("boss", "king_spin_whoosh", x, "weapon", "heavy axe whoosh (swish an octave down + low air)", i + 1)
    t = tl(1.0)
    shimmer = sum(np.sin(2 * np.pi * f * t) for f in (1760.0, 2217.0, 2637.0)) * (t / 1.0) ** 2 * 0.05
    x = mix([(normalize(choir(["D3", "F3", "A3", "C#4"], 1.0, attack=0.8, release=0.12)), 0, 0), (shimmer, 0, -4)])
    out("boss", "king_nova_sting", x, "stinger", "D harmonic-minor choir swell + rising high shimmer")
    for i in range(2):
        t = tl(1.0)
        wail = al.band(osc(glide(t, [900.0, 820.0][i], [600.0, 560.0][i], 0.4) * (1 + 0.01 * np.sin(2 * np.pi * 5.5 * t)), t, "saw"), 400.0, 2500.0) * swell(t, 0.15, 0.6)
        x = mix([(sub(0.9, 70.0, 38.0, 0.3), 0, 0), (normalize(noise_boom(1.0, 0.01, 0.25, 1500.0, 200.0, 0.1)), 0, -3),
                 (normalize(wail), 0.05, -8), (S(RPG + f"NPC/shade/shade{[1, 10][i]}.wav", pitch=0.9, length=0.9, fade=0.4), 0.05, -8)])
        out("boss", "soul_nova_ring", reverb(x, 1.0, 0.35, 0.25), "stinger", "deep whoom pulse (sub + dark air) + ghostly wail (gliding down) + ghost voice", i + 1)
    x = mix([(S(BREAK.format(1), pitch=0.8, length=0.5, fade=0.2), 0, 0), (S(KIMP + "impactGlass_heavy_000.ogg", pitch=0.8), 0.02, -3),
             (S(KIMP + "impactSoft_heavy_002.ogg", pitch=0.7), 0.12, -2), (sub(0.6, 60.0, 34.0, 0.25), 0.12, -4)], length=0.9)
    out("boss", "king_phase_break", x, "stinger", "bone crack + glassy shatter + heavy kneel thud + sub")
    x = mix([(saturate(S(R80 + "creature_roar_03.ogg", pitch=0.65, length=1.4, fade=0.4), 2.5), 0, 0),
             (saturate(S(R80 + "creature_roar_01.ogg", pitch=0.55, length=1.2, fade=0.4), 2.5), 0.04, -3),
             (saturate(S(R80 + "creature_roar_02.ogg", pitch=0.85, length=1.2, fade=0.4, hp=300.0), 3.0), 0.02, -5),
             (sub(1.3, 55.0, 35.0, 0.5, attack=0.08), 0, -5)], length=1.5)
    out("boss", "king_roar", reverb(x, 1.8, 0.6, 0.35), "stinger", "two creature roars pitched far down, distorted, + sub, long reverb")
    x = mix([(normalize(noise_boom(1.0, 0.003, 0.3, 3000.0, 120.0, 0.1)), 0, 0), (sub(0.8, 60.0, 32.0, 0.35), 0, -2),
             (normalize(whoosh(1.0, 2500.0, 250.0, peak=0.12)), 0, -4)])
    out("boss", "phase_shockwave", x, "impact_heavy", "air boom + sub + outward falling whoosh")
    x = mix([(saturate(S(R80 + "creature_roar_03.ogg", pitch=0.6, length=1.6, fade=0.5), 2.0), 0, 0),
             (saturate(S(R80 + "creature_roar_01.ogg", pitch=0.9, length=1.2, fade=0.4, hp=300.0), 3.0), 0.02, -6),
             (normalize(bell(hz("D2"), 2.0, BELL_CHURCH, decay_scale=0.9)), 0.1, -2),
             (normalize(choir(["D3", "G#3", "D4"], 1.2, attack=0.03, release=0.8)), 0.1, -4), (sub(1.0, 60.0, 32.0, 0.4), 0.1, -4)], length=2.1)
    out("boss", "king_enrage", reverb(x, 1.4, 0.5, 0.25), "stinger", "distorted roar + bell toll (D2) + dissonant choir stab (D/G#) + sub")
    layers = [(S(R80 + "creature_die_01.ogg", pitch=0.75, length=1.6, fade=0.8), 0.05, -2), (S(BREAK.format(6), length=0.5, fade=0.2), 0.1, -3),
              (S(KIMP + "impactPlate_medium_003.ogg"), 0.5, -5), (S(R80 + "metal_01.ogg", length=0.5, fade=0.2), 0.55, -9),
              (S(KIMP + "impactWood_medium_002.ogg"), 0.75, -6)]
    layers += [lay[:3] for lay in rattle_cloud(14, 0.0, 1.0, gains=(-10.0, -3.0), pitches=(0.7, 1.0))]
    out("boss", "boss_death", reverb(mix(layers, length=2.1), 1.0, 0.35, 0.2), "stinger",
        "fading groan + bone break + shield clang and staff clatter + 14 bones clattering")
    layers = [(saturate(S(R80 + "creature_roar_03.ogg", pitch=0.55, length=2.0, fade=1.2), 1.8), 0, 0),
              (normalize(choir(["D3", "F#3", "A3", "D4"], 2.6, attack=0.8, release=1.4)), 0.9, -3),
              (normalize(bell(hz("D2"), 3.0, BELL_CHURCH_MAJOR, decay_scale=1.0)), 1.0, -6), (sub(1.6, 50.0, 28.0, 0.7), 0, -4)]
    layers += [lay[:3] for lay in rattle_cloud(22, 0.9, 2.4, gains=(-14.0, -6.0))]
    out("boss", "king_death", reverb(mix(layers, length=3.6), 2.0, 0.7, 0.3), "stinger",
        "long roar fading into a D major choir chord + major bell + sub + bones raining")
    t = tl(1.8)
    wind = al.sweep_filter(white(1.8), "low", 400.0 + 2500.0 * swell(t, 0.9, 0.7)) * swell(t, 0.5, 0.8)
    x = mix([(normalize(wind), 0, 0), (normalize(whoosh(1.8, 200.0, 3000.0, peak=0.7)), 0, -2),
             (normalize(choir(["A4", "D5"], 1.8, attack=0.6, release=0.8, vowel="u")), 0, -10)])
    out("boss", "death_wave", reverb(x, 1.2, 0.4, 0.25), "stinger", "spectral wind + rising whoosh + airy 'oo' voices")
    x = mix([(S(R80 + "item_coins_04.ogg", length=1.5, fade=0.5), 0, 0), (S(KRPG + "handleCoins.ogg"), 0.2, -4),
             (S(KRPG + "handleCoins.ogg", pitch=1.15), 0.6, -6), (normalize(chime_run(["D6", "F#6", "A6", "D7"], 0.12, 1.4, decay_scale=0.5)), 0.1, -12)])
    out("boss", "gold_shower", x, "pickup", "coin cascade (coin pour + two handfuls) + light chime run")
    x = mix([(S(KIMP + "impactWood_heavy_004.ogg", pitch=0.7), 0, 0), (S(KIMP + "impactSoft_heavy_003.ogg"), 0, -3),
             (S(KRPG + "metalLatch.ogg"), 0.05, -8), (sub(0.4, 70.0, 40.0, 0.15), 0, -6),
             (normalize(chime_run(["D6", "A6"], 0.06, 0.65, decay_scale=0.6)), 0.15, -6)], length=0.85)
    out("boss", "boss_chest_drop", x, "stinger", "heavy chest thud + latch rattle + sub + sparkle chime")


# ============================================================================================ UI (manifest_ui.md section 3)

def ui():
    for i in range(2):
        out("ui", "ui_hover", S(KIMP + f"impactWood_light_00{[0, 2][i]}.ogg", length=0.045, fade=0.03, hp=800), "ui_tick",
            "soft wooden tick", i + 1)
    for i in range(2):
        x = mix([(S(KUI + f"click_00{[3, 5][i]}.ogg"), 0, 0), (S(KIMP + f"impactSoft_medium_00{[1, 3][i]}.ogg", length=0.08, fade=0.05), 0, -6)])
        out("ui", "ui_click", x, "ui", "click + low thump", i + 1)
    x = mix([(S(KUI + "click_002.ogg"), 0, -2), (normalize(chime_run(["E6", "B6"], 0.04, 0.25, decay_scale=0.3)), 0.01, 0)])
    out("ui", "ui_confirm", x, "ui", "click + bright E6/B6 chime")
    soft = S(KIMP + "impactSoft_medium_002.ogg", length=0.11, fade=0.08, hp=300, reverse=True)
    x = mix([(soft, 0, 0), (S(KUI + "click_004.ogg"), len(soft) / RATE - 0.004, -10)])
    out("ui", "ui_back", x, "ui", "reversed soft thump into a tiny click")
    knock = S(KIMP + "impactWood_medium_001.ogg", length=0.08, fade=0.05, lp=1800)
    out("ui", "ui_error", mix([(knock, 0, 0), (knock, 0.1, -2)]), "ui", "dull double wooden knock")
    x = mix([(S(KIMP + "impactMetal_light_003.ogg", length=0.2, fade=0.12), 0, 0), (S(R80 + "blade_03.ogg", length=0.2, fade=0.12), 0, -10)])
    out("ui", "ui_select", x, "ui", "short metal ring + blade shimmer")
    out("ui", "ui_toggle", S(KRPG + "metalLatch.ogg", length=0.08, fade=0.04), "ui", "latch click")
    out("ui", "ui_tick", S(KUI + "tick_002.ogg", length=0.03, fade=0.015), "ui_tick", "tiny tick")
    for i in range(3):
        x = mix([(S(KRPG + "bookFlip3.ogg", length=0.15, fade=0.08), 0, 0), (S(SWISH.format([10, 12, 13][i]), hp=1200), 0, -6)])
        out("ui", "card_flip", x, "ui", "paper flip + air swish", i + 1)
    out("ui", "card_select", mix([(normalize(whoosh(0.3, 600.0, 4500.0, peak=0.75)), 0, 0), (S(KUI + "select_007.ogg"), 0.24, -8)]),
        "ui", "upward whoosh + tick")
    layers = [(S(KRPG + "bookFlip1.ogg", length=0.4, fade=0.12), 0, 0)]
    layers += [(S(SWISH.format(13), pitch=1.5, hp=2000), 0.05 + 0.07 * k, -12) for k in range(4)]
    out("ui", "card_reroll", mix(layers), "ui", "card riffle + four quick paper flicks")
    x = mix([(S(KUI + "scratch_005.ogg", length=0.25, fade=0.1), 0, -2),
             (normalize(noise_boom(0.4, 0.02, 0.15, 1200.0, 200.0, 0.1)), 0.02, -4),
             (normalize(crackles(0.4, 0.05, 0.35, 40.0, 1500.0, 6000.0)), 0.05, -12)])
    out("ui", "card_banish", x, "ui", "torn paper scratch + low burn whoomp + crackle")
    out("ui", "card_skip", S(RPG + "inventory/coin3.wav", length=0.3, fade=0.12), "ui", "coin drop")
    for i in range(3):
        out("ui", "reel_tick", S(KIMP + f"impactWood_light_00{[1, 3, 4][i]}.ogg", length=0.03, fade=0.015, hp=1500), "ui_tick",
            "dry wooden click", i + 1)
    for i in range(2):
        x = mix([(S(KIMP + f"impactWood_heavy_00{[1, 3][i]}.ogg", length=0.25, fade=0.12), 0, 0),
                 (normalize(bell(hz(["G5", "A5"][i]), 0.3, BELL_CHIME, decay_scale=0.4)), 0.01, -5)])
        out("ui", "reel_land", x, "ui", "wooden thunk + small chime", i + 1)
    x = mix([(S(KIMP + "impactWood_heavy_001.ogg", length=0.25, fade=0.12), 0, 0),
             (normalize(choir(["D4", "F#4", "A4", "D5"], 0.95, attack=0.45, release=0.35)), 0.02, -3),
             (normalize(bell(hz("D6"), 0.5, BELL_CHIME, decay_scale=0.5)), 0.5, -6)], length=1.0)
    out("ui", "reel_land_evolution", reverb(x, 0.9, 0.3, 0.2), "stinger", "wooden thunk + rising D major choir chord + chime")
    x = mix([(S(R80 + "item_coins_04.ogg", length=0.6, fade=0.2), 0, 0), (S(KRPG + "handleCoins.ogg", length=0.5, fade=0.2), 0.05, -6)])
    out("ui", "chest_gold", x, "pickup", "coin cascade")
    for i in range(2):
        x = mix([(S(KIMP + f"impactMetal_heavy_00{[1, 3][i]}.ogg"), 0, 0),
                 (normalize(bell(hz(["A5", "G5"][i]), 0.5, BELL_CHIME, decay_scale=0.5)), 0, -6),
                 (S(R80 + f"item_coins_0{[1, 3][i]}.ogg", length=0.4, fade=0.15), 0.05, -5)])
        out("ui", "buy", x, "ui", "anvil strike (heavy metal hit + ring) + coins", i + 1)
    x = mix([(S(R80 + "item_coins_04.ogg", length=0.8, fade=0.3), 0, 0), (S(KRPG + "handleCoins.ogg", length=0.6, fade=0.2), 0.15, -4)])
    out("ui", "refund", x, "ui", "coin pour")
    horn = brass(["D3", "A3"], 1.2, attack=0.25, release=0.5, bright=900.0)
    out("ui", "banner", reverb(al.filt(horn, "low", 2500.0), 1.5, 0.6, 0.45), "ui", "distant horn call (D3/A3, wet hall)")
    x = mix([(normalize(brass(["D2", "A2", "D#3"], 1.5, attack=0.7, release=0.5, bright=800.0)), 0, 0),
             (normalize(bell(hz("D3"), 1.2, BELL_CHURCH, decay_scale=0.6)), 0.6, -4)], length=1.6)
    out("ui", "banner_warning", reverb(x, 1.4, 0.5, 0.3), "stinger", "low dissonant brass swell + church bell D3 at 0.6 s")
    t = tl(1.5)
    x = mix([(brown(1.5, 110.0) * swell(t, 1.2, 0.25), 0, 0), (osc(hz("D1") * 2.0, t) * swell(t, 1.2, 0.25), 0, -10),
             (normalize(whoosh(1.5, 150.0, 900.0, peak=0.85, sharpness=2.0)), 0, -6),
             (normalize(stab(["D3", "G#3"], 0.4, 0.12)), 1.2, -8)])
    out("ui", "danger_warning", x, "stinger", "rumble rising over 1.2 s + low drone + rising dark whoosh + tritone stab")
    x = mix([(S(KIMP + "impactPunch_heavy_002.ogg", pitch=0.6), 0, 0), (S(KIMP + "impactSoft_heavy_001.ogg", pitch=0.8), 0, -3),
             (sub(0.8, 65.0, 38.0, 0.3), 0, -4)], length=0.9)
    out("ui", "boss_bar_appear", reverb(x, 1.0, 0.35, 0.2), "stinger", "deep drum hit + sub")
    x = mix([(S(KIMP + "impactGlass_heavy_000.ogg", pitch=0.85), 0, 0), (S(BREAK.format(1), length=0.5, fade=0.2), 0, -3),
             (sub(0.6, 70.0, 38.0, 0.2), 0, -4),
             (normalize(crackles(0.7, 0.03, 0.6, 60.0, 2500.0, 9000.0, density_tau=0.25)), 0.02, -10)], length=0.75)
    out("ui", "boss_bar_shatter", x, "stinger", "glass smash + bone break + sub + shard crackles")
    pad_l = choir(["D3", "A3", "D4", "F#4"], 3.0, attack=0.15, release=1.8, seed=11)
    pad_r = choir(["D3", "A3", "D4", "A4"], 3.0, attack=0.15, release=1.8, seed=12)
    horns = brass(["D3", "F#3", "A3", "D4"], 1.6, attack=0.06, release=0.9, bright=1900.0)
    x = mix([(normalize(pad_l), 0, -3, -0.7), (normalize(pad_r), 0, -3, 0.7), (normalize(horns), 0, -2, 0.0),
             (normalize(bell(hz("D4"), 2.8, BELL_CHURCH_MAJOR, decay_scale=0.8)), 0, -7, 0.2),
             (sub(1.2, 75.0, 40.0, 0.4), 0, -6, 0.0)], length=3.0, stereo=True)
    out("ui", "results_victory", reverb(x, 2.0, 0.7, 0.3), "stinger", "D major choir (two voicings summed) + brass sting + major bell + sub")
    low_l = strings(["D2", "A2", "D3", "F3"], 3.0, attack=0.5, release=1.6, seed=21)
    low_r = strings(["D2", "A2", "F3", "A3"], 3.0, attack=0.5, release=1.6, seed=22)
    x = mix([(normalize(low_l), 0, -2, -0.6), (normalize(low_r), 0, -2, 0.6),
             (normalize(bell(hz("D3"), 3.0, BELL_CHURCH, decay_scale=1.0)), 0.1, -4, 0.0),
             (sub(1.5, 55.0, 32.0, 0.6), 0.1, -8, 0.0)], length=3.2, stereo=True)
    out("ui", "results_defeat", reverb(x, 2.0, 0.7, 0.3), "stinger", "low D minor strings (two voicings summed) + church bell D3 + sub")
    x = mix([(S(R80 + "item_coins_01.ogg", length=0.3, fade=0.12), 0, 0), (normalize(bell(hz("D6"), 0.4, BELL_CHIME, decay_scale=0.5)), 0.05, -4)])
    out("ui", "results_tally", x, "ui", "coins + ding (D6)")


# ============================================================================================ weapons: Knight

def knight():
    for i in range(3):
        x = mix([(S(SWISH.format([3, 7, 9][i]), pitch=0.92), 0, 0), (S(R80 + f"blade_0{i + 1}.ogg", hp=2000, length=0.4, fade=0.25), 0.03, -6)])
        out("knight", "oathblade_swing", x, "weapon", "air swish + steel 'shing' ring tail (2-4 kHz)", i + 1)
    for i in range(3):
        x = mix([(normalize(thump(0.3, 160.0, 110.0, 0.045)), 0, -2), (S(KIMP + f"impactPunch_medium_00{i}.ogg", length=0.25, fade=0.12), 0, -4),
                 (S(BREAK.format([1, 6, 8][i]), length=0.12, fade=0.06, hp=900), 0.003, -3)])
        out("knight", "oathblade_hit", x, "impact", "low thump (160->110 Hz) + punch + bone crunch transient", i + 1)
    for i in range(2):
        x = mix([(normalize(whoosh(0.6, 400.0, 3500.0, peak=0.6)), 0, 0),
                 (normalize(chime_run([["D6", "A6"], ["A5", "E6"]][i], 0.05, 0.6, decay_scale=0.6)), 0.15, -5),
                 (normalize(choir(["D4", "A4"], 0.6, attack=0.3, release=0.25)), 0, -10)])
        out("knight", "dawn_crescent", x, "weapon", "rising holy whoosh + bell shimmer + faint choir", i + 1)
    for i in range(2):
        t = tl(0.6)
        base = [880.0, 784.0][i]
        tone = (osc(base, t) + 0.7 * osc(base * 1.5, t) + 0.25 * osc(base * 3.0, t)) * (1.0 + 0.15 * np.sin(2 * np.pi * 6.0 * t))
        x = mix([(normalize(tone * swell(t, 0.3, 0.25)), 0, 0), (S(KIMP + "impactPlate_light_000.ogg", length=0.3, fade=0.2), 0.0, -14)])
        out("knight", "aegis_summon", x, "weapon", f"glass/bell tone ({int(base)} + {int(base * 1.5)} Hz partials) swelling in", i + 1)
    for i in range(4):
        x = mix([(S(KIMP + f"impactPlate_medium_00{[0, 1, 3, 4][i]}.ogg", length=0.25, fade=0.15), 0, 0),
                 (S(KIMP + f"impactWood_medium_00{i}.ogg", length=0.08, fade=0.05), 0, -5)])
        out("knight", "aegis_bonk", x, "impact", "metal-wood 'bonk' (shield plate + wood) with a short ring", i + 1)
    for i in range(3):
        x = mix([(S(KIMP + f"impactMetal_light_00{[1, 3, 4][i]}.ogg", pitch=1.2, length=0.25, fade=0.12), 0, 0),
                 (normalize(crackles(0.25, 0.0, 0.15, 150.0, 3000.0, 10000.0, decay=0.0015)), 0.005, -8)])
        out("knight", "aegis_block", x, "impact", "crisp metallic ping + spark crackle", i + 1)
    for i in range(2):
        x = mix([(normalize(choir([["D4", "A4", "D5"], ["C4", "G4", "C5"]][i], 0.4, attack=0.3, release=0.06)), 0, 0),
                 (normalize(whoosh(0.35, 500.0, 3000.0, peak=0.95)), 0, -6)], length=0.4)
        out("knight", "verdict_summon", x, "weapon", "short choir swell + rising whoosh into the slam", i + 1)
    for i in range(3):
        x = mix([(S(KIMP + f"impactPunch_heavy_00{[0, 2, 4][i]}.ogg"), 0, -3),
                 (S(R80 + f"stones_0{[2, 4, 1][i]}.ogg", length=0.3, fade=0.12), 0, -1),
                 (S(KIMP + f"impactMining_00{i}.ogg", length=0.4, fade=0.2), 0.005, -2),
                 (sub(0.8, 75.0, 38.0, 0.3), 0, -2),
                 (normalize(bell(hz("C2"), 1.3, BELL_CHURCH, decay_scale=0.45)), 0, -9),
                 (normalize(bell(hz("C4"), 1.0, BELL_CHURCH, decay_scale=0.35)), 0, -10)], length=1.2)
        out("knight", "verdict_slam", reverb(x, 1.0, 0.35, 0.15), "impact_heavy",
            "deep bell (C2) + stone crack transient + punch + sub < 80 Hz thump, bell tail", i + 1)
    for i in range(2):
        x = mix([(normalize(noise_boom(0.8, 0.005, 0.25, 4000.0, 300.0, 0.1)), 0, 0),
                 (normalize(whoosh(0.8, 3000.0, 300.0, peak=0.15)), 0, -4),
                 (normalize(bell(hz(["D5", "A4"][i]), 0.8, BELL_CHIME, decay_scale=0.6)), 0, -10),
                 (sub(0.6, 80.0, 45.0, 0.2), 0, -6)])
        out("knight", "verdict_shockwave", reverb(x, 1.0, 0.35, 0.2), "impact_heavy", "airy holy boom + outward whoosh + chime + sub", i + 1)
    for i in range(2):
        x = mix([(normalize(whoosh(0.35, 300.0, 1200.0, peak=0.4)), 0, 0), (S(RPG + f"inventory/chainmail{i + 1}.wav", length=0.35, fade=0.15), 0, -5)])
        out("knight", "shield_rush", x, "weapon", "heavy whoosh + armor rattle", i + 1)
    for i in range(2):
        x = mix([(S(KIMP + f"impactPlate_heavy_00{[1, 3][i]}.ogg"), 0, 0), (S(KIMP + f"impactPunch_heavy_00{[2, 4][i]}.ogg"), 0, -3),
                 (sub(0.5, 90.0, 45.0, 0.15), 0, -4)], length=0.6)
        out("knight", "shield_rush_boom", x, "impact_heavy", "shield plate impact + low punch + sub thump", i + 1)
    for i in range(3):
        clang = S(KIMP + f"impactMetal_medium_00{[0, 3, 1][i]}.ogg", length=0.4, fade=0.2)
        x = mix([(al.band(clang, 800.0, 4500.0), 0, 0), (normalize(thump(0.3, 120.0, 100.0, 0.15)), 0, -3),
                 (S(KIMP + "impactBell_heavy_002.ogg", length=0.5, fade=0.25), 0, -10)])
        out("knight", "bulwark_clang", x, "impact", "metal clang (1-4 kHz) + low thump (100 Hz, 150 ms) + bell ring", i + 1)


# ============================================================================================ weapons: Barbarian

def barbarian():
    for i in range(3):
        x = mix([(S(SWISH.format([3, 7, 8][i]), pitch=0.72), 0, 0), (normalize(whoosh(0.35, 900.0, 250.0, q=0.9, peak=0.45)), 0, -3),
                 (S(KRPG + "cloth1.ogg", length=0.3, fade=0.15), 0, -12)])
        out("barbarian", "ravager_chop", x, "weapon", "heavy swish (slowed) + low air whoosh + cloth", i + 1)
    for i in range(4):
        x = mix([(S(KRPG + "chop.ogg", pitch=[1.0, 0.92, 1.06, 0.97][i]), 0, 0), (S(KIMP + f"impactPunch_heavy_00{[1, 3, 4, 0][i]}.ogg", length=0.3, fade=0.15), 0, -3),
                 (S(BREAK.format([1, 6, 8, 4][i]), length=0.15, fade=0.08), 0.004, -5), (sub(0.3, 80.0, 45.0, 0.08), 0, -5)])
        out("barbarian", "ravager_hit", x, "impact_heavy", "wet axe chop + heavy punch + bone crack + sub", i + 1)
    for i in range(2):
        t = tl(0.6)
        x = mix([(S(KIMP + f"impactMining_00{[0, 3][i]}.ogg", length=0.6, fade=0.25), 0, 0), (S(R80 + "stones_03.ogg", length=0.6, fade=0.25), 0.02, -5),
                 (al.band(white(0.6), 3000.0, 9000.0) * env(t, 0.05, 0.05, 0.25), 0, -10),
                 (normalize(crackles(0.6, 0.05, 0.5, 60.0, 800.0, 5000.0)), 0.03, -9), (sub(0.6, 60.0, 35.0, 0.25), 0, -3)])
        out("barbarian", "skullsplitter_crack", x, "impact_heavy", "rock tearing crack + stones + magma hiss + crackle + sub", i + 1)
    seconds = 3.75
    t = tl(seconds)
    spin = 0.5 + 0.5 * np.sin(2 * np.pi * 4.5 * t)
    centre = 650.0 + 1100.0 * spin ** 2
    blades = al.sweep_filter(white(seconds), "band", centre, q=1.4) * (0.25 + 0.75 * spin ** 3)
    whum = al.filt(white(seconds), "low", 260.0) * (0.5 + 0.5 * spin) * 3.0
    ring = sum(np.sin(2 * np.pi * f * t) for f in (2350.0, 3410.0)) * 0.02 * spin
    loop = loopable(normalize(blades) + normalize(whum) * 0.5 + ring, 0.25)
    out("barbarian", "whirlwind_loop", loop, "loop", "spinning blade whoosh: band noise swept per pass at 4.5 Hz + low whum + faint steel ring; seam crossfaded")
    for i in range(2):
        x = mix([(S(R80 + f"metal_0{[2, 3][i]}.ogg", length=0.5, fade=0.25), 0, -4), (S(KRPG + f"footstep0{[0, 3][i]}.ogg", length=0.25, fade=0.1), 0.02, -4),
                 (S(RPG + f"NPC/giant/giant{[3, 5][i]}.wav", pitch=1.15, length=0.35, fade=0.15), 0.04, -5)])
        out("barbarian", "whirlwind_stop", x, "weapon", "axe ring + boot scuff + grunt", i + 1)
    for i in range(2):
        t = tl(0.4)
        dust = al.band(white(0.4), 800.0, 4000.0) * env(t, 0.01, 0.02, 0.12)
        x = mix([(normalize(thump(0.4, 130.0, 80.0, 0.12)), 0, 0), (S(KIMP + f"impactSoft_heavy_00{[0, 3][i]}.ogg", length=0.35, fade=0.15), 0, -2),
                 (normalize(dust), 0.01, -10)])
        out("barbarian", "fault_stomp", x, "impact_heavy", "low ground thump (130->80 Hz) + boot impact + dust", i + 1)
    for i in range(2):
        layers = [(S(R80 + f"stones_0{[1, 3][i]}.ogg", length=0.6, fade=0.25), 0, -4), (normalize(brown(0.6, 120.0) * swell(tl(0.6), 0.05, 0.3)), 0, -6)]
        for k, (lo, hi) in enumerate(((600.0, 2500.0), (1000.0, 4000.0), (1600.0, 6500.0))):
            layers.append((normalize(crackles(0.6, 0.0, 0.25, 140.0, lo, hi, decay=0.003)), 0.15 * k, -4 - k))
        out("barbarian", "fault_crack", mix(layers), "impact", "rolling crackle climbing in pitch (travels) + stones + rumble", i + 1)
    for i in range(2):
        x = mix([(S(RPG + f"NPC/giant/giant{[1, 5][i]}.wav", pitch=1.12, length=0.35, fade=0.15), 0, 0), (normalize(whoosh(0.4, 300.0, 1500.0, peak=0.4)), 0, -4)])
        out("barbarian", "leap_takeoff", x, "voice", "grunt + rising whoosh", i + 1)
    for i in range(2):
        x = mix([(S(KSF + f"lowFrequency_explosion_00{[1, 0][i]}.ogg", length=1.0, fade=0.4), 0, -2), (sub(1.0, 60.0, 32.0, 0.4), 0, -2),
                 (S(KIMP + f"impactMining_00{[4, 2][i]}.ogg"), 0, 0), (S(R80 + f"stones_0{[1, 2][i]}.ogg", length=1.0, fade=0.4), 0.08, -3),
                 (normalize(noise_boom(1.2, 0.003, 0.3, 2500.0, 150.0, 0.1)), 0, -4)], length=1.25)
        out("barbarian", "leap_land", x, "impact_heavy", "low boom + sub < 60 Hz + rock crack + crumbling stones tail", i + 1)


# ============================================================================================ weapons: Mage

def fire_whoomp(seconds, variant):
    t = tl(seconds)
    whoomp = noise_boom(seconds, 0.004, seconds * 0.3, 2600.0, 300.0, 0.05)
    roar = al.band(white(seconds), 200.0, 2500.0) * flicker(t, 3.0, 11.0, count=6, depth=0.6) * swell(t, 0.03, seconds * 0.6)
    return normalize(normalize(whoomp) + normalize(roar) * 0.6)


def mage():
    for i in range(3):
        x = mix([(fire_whoomp(0.3, i), 0, 0), (S(R80 + f"spell_fire_0{[6, 1, 7][i]}.ogg", length=0.3, fade=0.12), 0, -8)])
        out("mage", "ember_whoosh", x, "weapon", "fire 'fwoomp' (closing noise burst + flickering roar) + fire spell texture", i + 1)
    for i in range(4):
        x = mix([(normalize(crack(0.5)), 0, -2), (normalize(noise_boom(0.5, 0.002, 0.15, 3000.0, 150.0, 0.06)), 0, 0),
                 (S(KSF + f"explosionCrunch_00{i}.ogg", length=0.5, fade=0.2), 0, -4),
                 (normalize(crackles(0.5, 0.06, 0.45, 70.0, 1500.0, 6000.0, density_tau=0.2)), 0.04, -9),
                 (sub(0.4, 80.0, 45.0, 0.12), 0, -5)])
        out("mage", "ember_boom", x, "impact", "crack transient + 150 ms body boom + explosion crunch + crackle tail + sub", i + 1)
    for i in range(2):
        t = tl(0.6)
        f = np.geomspace([900.0, 800.0][i], [2200.0, 2000.0][i], len(t))
        tone = osc(f, t) * 0.6 + al.sweep_filter(white(0.6), "band", f, q=4.0) * 2.0
        out("mage", "starfall_whistle", tone * swell(t, 0.45, 0.06), "weapon", "whistle rising over 0.6 s (sine + band noise)", i + 1)
    for i in range(3):
        x = mix([(normalize(noise_boom(1.0, 0.002, 0.3, 3500.0, 120.0, 0.1)), 0, 0), (sub(0.8, 70.0, 35.0, 0.3), 0, -2),
                 (S(KIMP + f"impactMining_00{[1, 3, 4][i]}.ogg"), 0, -4), (S(R80 + f"stones_0{[1, 2, 4][i]}.ogg", length=0.8, fade=0.3), 0.05, -8),
                 (normalize(crackles(1.0, 0.05, 0.8, 80.0, 1000.0, 6000.0, density_tau=0.3)), 0.05, -10)])
        out("mage", "starfall_impact", x, "impact_heavy", "air boom + sub + rock crack + debris + crackles", i + 1)
    t = tl(0.9)
    f = np.geomspace(500.0, 1300.0, len(t))
    x = (osc(f, t) * 0.5 + al.sweep_filter(white(0.9), "band", f, q=3.0) * 2.0) * swell(t, 0.7, 0.06) + brown(0.9, 90.0) * swell(t, 0.8, 0.05) * 0.6
    out("mage", "cataclysm_whistle", x, "weapon", "deep whistle rising over 0.9 s + rumble")
    t = tl(2.0)
    x = mix([(normalize(crack(2.0, 0.03, 300.0)), 0, -3), (normalize(noise_boom(2.0, 0.002, 0.55, 3500.0, 100.0, 0.15)), 0, 0),
             (normalize(al.filt(white(2.0), "low", 110.0) * env(t, 0.005, 0.008, 0.75)), 0, -3),
             (S(KSF + "lowFrequency_explosion_000.ogg", length=2.0, fade=0.8), 0, -4),
             (normalize(al.band(white(2.0), 150.0, 2400.0) * flicker(t, 3.0, 10.0, 6, 0.7) * env(t, 0.04, 0.06, 1.0)), 0, -8),
             (normalize(crackles(2.0, 0.06, 1.8, 70.0, 600.0, 4500.0, decay=0.005, density_tau=0.55)), 0, -8),
             (sub(1.6, 55.0, 28.0, 0.7), 0, -2)], length=2.0)
    out("mage", "cataclysm_impact", reverb(x, 1.6, 0.6, 0.2), "impact_heavy",
        "massive: crack + closing air blast + deep boom + low explosion + fire roar + debris rain + sub < 60 Hz")
    for i in range(3):
        x = mix([(normalize(crackles(0.5, 0.0, 0.4, 260.0, 2500.0, 10000.0, decay=0.0015)), 0, 0),
                 (normalize(whoosh(0.5, 1500.0, 6000.0, peak=0.35)), 0, -5),
                 (normalize(chime_run([["E6", "B6", "E7"], ["F#6", "C#7", "F#7"], ["D6", "A6", "D7"]][i], 0.04, 0.5, BELL_GLASS, decay_scale=1.2)), 0.02, -6),
                 (S(KUI + f"glass_00{[2, 3, 5][i]}.ogg"), 0, -8)])
        out("mage", "rime_nova", x, "impact", "glassy crackle sweep (ice crystallizing) + airy whoosh + high glass shimmer", i + 1)
    for i in range(4):
        x = mix([(S(KIMP + f"impactGlass_heavy_00{[0, 2, 3, 1][i]}.ogg", length=0.3, fade=0.15), 0, 0),
                 (S(KIMP + f"impactGlass_light_00{i}.ogg", length=0.25, fade=0.12), 0.01, -5),
                 (normalize(crackles(0.3, 0.0, 0.2, 200.0, 4000.0, 12000.0, decay=0.001)), 0, -10)])
        out("mage", "frost_shatter", x, "impact", "ice shatter (glass hit + tinkle) + fine crackle", i + 1)
    for i in range(2):
        swell_tone = bell(hz(["D6", "A5"][i]), 0.33, BELL_CHIME, decay_scale=0.4)[::-1].copy()
        x = mix([(normalize(swell_tone), 0, 0), (S(KUI + "click_003.ogg"), 0.325, -6)])
        out("mage", "arcane_echo", x, "weapon", "reversed chime swelling into a click", i + 1)
    for i in range(2):
        t = tl(0.25)
        pop = osc(glide(t, 900.0, 300.0, 0.01, 0.21), t) * env(t, 0.21, 0.001, 0.015)
        x = mix([(normalize(whoosh(0.22, [5000.0, 4000.0][i], 600.0, peak=0.95, sharpness=2.5)), 0, 0), (normalize(pop), 0, -3)])
        out("mage", "blink", x, "weapon", "implosion: reverse-shaped whoosh into a pop", i + 1)
    for i in range(2):
        x = mix([(normalize(noise_boom(0.7, 0.003, 0.2, 2500.0, 200.0, 0.08)), 0, 0), (sub(0.5, 85.0, 45.0, 0.15), 0, -4),
                 (normalize(bell(hz(["F#6", "G6"][i]), 0.7, BELL_GLASS, decay_scale=1.5) + bell(hz(["C#7", "D7"][i]), 0.7, BELL_GLASS, decay_scale=1.2)), 0, -7),
                 (S(R80 + "spell_02.ogg", pitch=0.8, length=0.6, fade=0.25), 0, -8)])
        out("mage", "blink_rift", x, "impact", "violet boom + sub + glassy overtone dyad + spell texture", i + 1)


# ============================================================================================ weapons: Rogue

def rogue():
    for i in range(4):
        t = tl(0.2)
        twang = karplus(hz("A2") * [1.0, 1.05, 0.95, 1.1][i], 0.2, damping=0.985, brightness=0.7)
        hiss = al.band(white(0.2), 3000.0, 9000.0) * env(t, 0.005, 0.01, 0.06)
        x = mix([(normalize(twang), 0, 0), (normalize(hiss), 0, -11), (S(KRPG + "metalLatch.ogg", length=0.06, fade=0.03), 0, -10)])
        out("rogue", "crossbow_twang", x, "weapon", "tight string twang (plucked, A2) + bolt hiss + trigger click", i + 1)
    for i in range(3):
        t = tl(0.25)
        whizz = osc(np.geomspace(3200.0, 1800.0, len(t)), t) * env(t, 0.0, 0.01, 0.08)
        x = mix([(S(KIMP + f"impactMetal_light_00{[1, 2, 4][i]}.ogg", pitch=1.5, length=0.25, fade=0.12), 0, 0), (normalize(whizz), 0.01, -9)])
        out("rogue", "widow_ricochet", x, "weapon", "high metal ping + falling ricochet whizz", i + 1)
    picks = [[11, 12, 13], [10, 12, 2], [13, 5, 11]]
    for i in range(3):
        layers = [(S(SWISH.format(n), pitch=float(RND.uniform(1.2, 1.35)), hp=900), 0.035 * k, -k * 1.5) for k, n in enumerate(picks[i] + [picks[i][0]])]
        layers.append((S(KRPG + "knifeSlice2.ogg", length=0.3, fade=0.15), 0, -10))
        out("rogue", "knife_fan", mix(layers), "weapon", "four quick blade swishes 35 ms apart + knife slice", i + 1)
    for i in range(2):
        t = tl(0.6)
        whisper = al.band(white(0.6), 1500.0, 6000.0) * flicker(t, 6.0, 14.0, 5, 0.8) * swell(t, 0.15, 0.3)
        beat = osc(glide(t, 70.0, 50.0, 0.04), t) * env(t, 0.12, 0.003, 0.06)
        x = mix([(normalize(whisper), 0, -4), (S(RPG + f"NPC/shade/shade{[1, 14][i]}.wav", pitch=1.3, length=0.5, fade=0.2), 0, -6),
                 (normalize(beat), 0, -2)])
        out("rogue", "mark_apply", x, "weapon", "whisper (shaped breath + ghost voice) + heartbeat thump", i + 1)
    for i in range(3):
        x = mix([(S(KRPG + ["knifeSlice.ogg", "knifeSlice2.ogg", "knifeSlice.ogg"][i], pitch=[1.0, 1.0, 1.1][i], length=0.2, fade=0.1), 0, 0),
                 (S(KIMP + f"impactPunch_medium_00{i}.ogg", length=0.12, fade=0.06), 0, -8), (normalize(whoosh(0.25, 2500.0, 600.0, peak=0.3)), 0, -6)])
        out("rogue", "mark_stab", x, "impact", "clean blade stab + small punch + smoky whoosh", i + 1)
    for i in range(2):
        t = tl(0.5)
        f = np.geomspace([6000.0, 5000.0][i], 2500.0, len(t))
        x = mix([(normalize(al.sweep_filter(white(0.5), "band", f, q=1.5) * swell(t, 0.05, 0.3)), 0, 0), (S(SWISH.format(12), pitch=1.3), 0.02, -8)])
        out("rogue", "mark_daggers", x, "weapon", "falling hiss + blade swish", i + 1)
    for i in range(2):
        x = mix([(S(KRPG + "knifeSlice2.ogg", length=0.4, fade=0.2), 0, -2), (S(SWISH.format(7), pitch=0.8), 0, -3),
                 (normalize(bell(hz(["D3", "C#3"][i]), 0.6, BELL_CHURCH, decay_scale=0.3)), 0.03, -4),
                 (S(BREAK.format(6), length=0.3, fade=0.15), 0.02, -6), (sub(0.4, 80.0, 40.0, 0.15), 0, -5)], length=0.6)
        out("rogue", "execute", x, "impact_heavy", "heavy slash + bell toll cut short + bone break + sub", i + 1)
    for i in range(2):
        t = tl(0.4)
        x = mix([(normalize(noise_boom(0.4, 0.01, 0.12, 1800.0, 300.0, 0.06)), 0, 0),
                 (normalize(al.band(white(0.4), 2000.0, 7000.0) * env(t, 0.01, 0.02, 0.12)), 0, -10),
                 (S(KRPG + f"cloth{[1, 3][i]}.ogg", length=0.3, fade=0.15), 0, -10)])
        out("rogue", "smoke_poof", x, "weapon", "soft low 'poof' + hiss + cloth", i + 1)
    for i in range(2):
        x = mix([(S(KRPG + f"cloth{[1, 3][i]}.ogg", length=0.35, fade=0.15), 0, 0), (normalize(whoosh(0.35, 600.0, 2000.0, peak=0.5)), 0, -3)])
        out("rogue", "roll_whoosh", x, "weapon", "cloth rustle + whoosh", i + 1)


# ============================================================================================ weapons: shared

def shared():
    notes = ["D5", "E5", "A5", "B5"]
    for i in range(4):
        pluck = karplus(hz(notes[i]), 0.25, damping=0.996, brightness=0.45)
        t = tl(0.25)
        air = al.band(white(0.25), 2000.0, 8000.0) * env(t, 0.0, 0.01, 0.05)
        out("shared", "spirit_dart", mix([(normalize(pluck), 0, 0), (normalize(air), 0, -14)]), "weapon",
            f"soft harp-like pluck ({notes[i]}, pentatonic set) + air", i + 1)
    for i in range(2):
        t = tl(0.3)
        whirr = al.band(white(0.3), 700.0, 3500.0) * (0.4 + 0.6 * np.abs(np.sin(2 * np.pi * [18.0, 22.0][i] * t))) * swell(t, 0.05, 0.15)
        out("shared", "disc_throw", mix([(normalize(whirr), 0, 0), (normalize(whoosh(0.3, 1200.0, 500.0, peak=0.3)), 0, -6)]), "weapon",
            "whirring throw: band noise chopped at 18-22 Hz + whoosh", i + 1)
    for i in range(2):
        x = mix([(normalize(bell(hz(["E6", "D6"][i]), 0.25, BELL_CHIME, decay_scale=0.25)), 0, 0),
                 (S(KIMP + "impactMetal_light_002.ogg", pitch=1.4, length=0.1, fade=0.05), 0, -8)])
        out("shared", "disc_ping", x, "impact", "tuned metallic ping (E6/D6 chime) + metal tick; the script climbs a semitone per bounce", i + 1)
    for i in range(3):
        x = mix([(S(KIMP + f"footstep_grass_00{[0, 2, 4][i]}.ogg", length=0.5, fade=0.2), 0, 0),
                 (normalize(crackles(0.5, 0.0, 0.4, 50.0, 1500.0, 6000.0, decay=0.002)), 0, -8), (S(R80 + "wood_04.ogg", length=0.3), 0.05, -14)])
        out("shared", "bramble_rustle", x, "weapon", "dry grass rustle + twig crackles + wood creak", i + 1)
    for i in range(2):
        x = mix([(S(SWISH.format([7, 9][i]), pitch=0.6), 0, 0), (normalize(whoosh(0.5, 1500.0, 300.0, peak=0.3)), 0, -3)])
        out("shared", "javelin_throw", x, "weapon", "heavy swish (slowed) + falling whoosh", i + 1)
    for i in range(2):
        x = mix([(normalize(noise_boom(0.9, 0.003, 0.25, 4000.0, 200.0, 0.1)), 0, 0), (sub(0.7, 75.0, 40.0, 0.25), 0, -3),
                 (normalize(choir([["D4", "A4", "D5"], ["E4", "B4", "E5"]][i], 0.9, attack=0.03, release=0.6)), 0, -8),
                 (normalize(bell(hz(["D5", "E5"][i]), 0.9, BELL_CHURCH_MAJOR, decay_scale=0.4)), 0, -8)])
        out("shared", "godspear_boom", reverb(x, 1.0, 0.35, 0.2), "impact_heavy", "holy explosion: air boom + sub + choir burst + major bell", i + 1)
    for i in range(2):
        x = mix([(S(SWISH.format([5, 6][i])), 0, 0), (S(KUI + f"glass_00{[5, 6][i]}.ogg", length=0.1, fade=0.05), 0, -10),
                 (S(RPG + "inventory/bottle.wav", length=0.2, fade=0.1), 0, -8)])
        out("shared", "flask_throw", x, "weapon", "swish + glass clink + bottle slosh", i + 1)
    for i in range(3):
        x = mix([(S(KIMP + f"impactGlass_heavy_00{[4, 1, 2][i]}.ogg"), 0, 0), (normalize(noise_boom(0.7, 0.01, 0.2, 2500.0, 250.0, 0.08)), 0.03, -2),
                 (normalize(crackles(0.7, 0.08, 0.65, 50.0, 1200.0, 5000.0)), 0.05, -10)])
        out("shared", "flask_shatter", x, "impact", "glass shatter + 'whoomp' ignition + fire crackle", i + 1)
    for i in range(2):
        t = tl(0.4)
        hum = (al.band(white(0.4), 2000.0, 7000.0) * (np.abs(np.sin(2 * np.pi * 60.0 * t)) ** 4) + osc(120.0, t, "saw") * 0.15) * swell(t, 0.15, 0.1)
        x = mix([(S(KIMP + f"impactWood_heavy_00{[0, 2][i]}.ogg"), 0, 0), (normalize(hum), 0.05, -8)])
        out("shared", "totem_place", x, "weapon", "wooden thunk + static buzz hum starting", i + 1)
    for i in range(4):
        t = tl(0.25)
        snap = al.filt(white(0.25), "high", 1500.0) * env(t, 0.0, 0.0005, 0.008)
        buzz = osc(glide(t, [180.0, 220.0, 160.0, 200.0][i], 90.0, 0.05), t, "saw") * env(t, 0.0, 0.002, 0.06)
        sizzle = crackles(0.25, 0.0, 0.18, 300.0, 2500.0, 9000.0, decay=0.001)
        out("shared", "totem_zap", mix([(normalize(snap), 0, 0), (normalize(buzz), 0, -6), (normalize(sizzle), 0, -4)]), "weapon",
            "electric snap + falling buzz + sizzle crackle", i + 1)
    for i in range(3):
        t = tl(0.4)
        f = np.geomspace([2600.0, 2400.0, 2800.0][i], 1400.0, len(t))
        x = (osc(f, t) * 0.5 + al.sweep_filter(white(0.4), "band", f, q=3.0) * 2.0) * swell(t, 0.1, 0.2)
        out("shared", "wisp_whistle", x, "weapon", "falling whistle (sine + band noise)", i + 1)
    for i in range(3):
        x = mix([(normalize(bell(hz(["A6", "E6", "C#7"][i]), 0.4, BELL_GLASS, decay_scale=1.2)), 0, 0),
                 (normalize(noise_boom(0.4, 0.005, 0.08, 5000.0, 800.0, 0.05)), 0, -6)])
        out("shared", "wisp_splash", x, "impact", "airy glass chime + soft bright burst", i + 1)


# ============================================================================================ ambience

def ambience():
    seconds, overlap = 5.5, 0.5
    t = tl(seconds)
    gust = 0.55 + 0.25 * np.sin(2 * np.pi * 0.13 * t + 1.0) + 0.2 * np.sin(2 * np.pi * 0.31 * t)
    gust = gust * flicker(t, 0.05, 0.4, 4, 0.4)
    wind = al.sweep_filter(white(seconds), "low", 300.0 + 600.0 * gust, q=0.8) * gust
    howl = al.sweep_filter(white(seconds), "band", 700.0 + 250.0 * gust, q=9.0) * gust ** 2 * 0.6
    x = al.filt(normalize(wind) + normalize(howl) * 0.25, "high", 60.0, order=2)
    out("ambience", "moor_wind", loopable(x, overlap), "ambience", "moor wind bed: gusting low-passed noise + faint howl; 5 s seamless loop",
        keep_edges=True)


# ============================================================================================ music

def verify_flac(path, ints):
    """Decodes the file with the engine's dr_flac (flacdec.exe) and stops the build unless it is bit-exact."""
    raw = os.path.join(SURV, "audio_work", "cache", os.path.basename(path) + ".raw")
    subprocess.run([os.path.join(os.path.dirname(os.path.abspath(__file__)), "flacdec.exe"), path, raw], check=True)
    with open(raw, "rb") as f:
        head = f.read(16)
        channels = int(np.frombuffer(head[4:8], dtype="<i4")[0])
        decoded = np.frombuffer(f.read(), dtype="<i2").reshape(-1, channels).astype(np.int64)
    if decoded.shape != ints.shape or not np.array_equal(decoded, ints):
        raise RuntimeError(f"FLAC round trip failed for {path}")
    print(f"  {os.path.relpath(path, OUT)} verified bit-exact ({os.path.getsize(path) / 1e6:.2f} MB)")


def music():
    folder = os.path.join(OUT, "music")
    os.makedirs(folder, exist_ok=True)
    data, rate = al.load(os.path.join(al.DOWNLOADS, "music_battle/determined_pursuit_loop.wav"))
    half = data.shape[0] // 2
    loop = data[:half]
    ints = np.clip(np.round(loop * 32768.0), -32768, 32767).astype(np.int64)
    flac.encode(os.path.join(folder, "battle_loop.flac"), ints, rate)
    verify_flac(os.path.join(folder, "battle_loop.flac"), ints)
    RECORDS["music/battle_loop.flac"] = {"name": "music_battle", "file": "music/battle_loop.flac", "seconds": round(half / rate, 3),
                                        "category": "music", "desc": "first 54.0 s of the 108 s loop, lossless FLAC (bit-exact through the engine's dr_flac); its last 14 s equal the track's end, so the wrap is the composer's own seam",
                                        "sources": ["music_battle:determined_pursuit_loop.wav"], "channels": 2,
                                        "peak_db": round(al.peak_db(loop), 2), "loudness": round(al.loudness(loop, 3.0), 1)}
    shutil.copyfile(os.path.join(al.DOWNLOADS, "music_boss/heavy_boss_battle_1_bpm200_0.ogg"), os.path.join(folder, "boss_loop.ogg"))
    boss, _ = al.load(os.path.join(al.DOWNLOADS, "music_boss/heavy_boss_battle_1_bpm200_0.ogg"))
    RECORDS["music/boss_loop.ogg"] = {"name": "music_boss", "file": "music/boss_loop.ogg", "seconds": round(boss.shape[0] / 44100.0, 3),
                                      "category": "music", "desc": "original Ogg Vorbis copied unchanged (seam step 0.026 is within the music's own sample steps)",
                                      "sources": ["music_boss:heavy_boss_battle_1_bpm200_0.ogg"], "channels": 2,
                                      "peak_db": round(al.peak_db(boss), 2), "loudness": round(al.loudness(boss, 3.0), 1)}
    menu, rate = al.load(os.path.join(al.DOWNLOADS, "music_menu/qubodup-yd-DarkShrineLoop-OpenGameArt.ogg"))
    tail = int(3.5 * rate)
    end = int(np.nonzero(np.max(np.abs(menu), axis=1) > 1e-5)[0][-1]) + 1
    body = menu[:end - tail].copy()
    body[:tail] += menu[end - tail:end]
    body = body / np.max(np.abs(body)) * dsp.PEAK
    dither = np.random.default_rng(5).uniform(-0.5, 0.5, body.shape) + np.random.default_rng(6).uniform(-0.5, 0.5, body.shape)
    ints = np.clip(np.round(body * 32767.0 + dither), -32768, 32767).astype(np.int64)
    flac.encode(os.path.join(folder, "menu_loop.flac"), ints, rate)
    verify_flac(os.path.join(folder, "menu_loop.flac"), ints)
    RECORDS["music/menu_loop.flac"] = {"name": "music_menu", "file": "music/menu_loop.flac", "seconds": round(len(body) / rate, 3),
                                      "category": "music", "desc": "decoded; trailing digital silence cut, the 3.5 s fade-out tail overlapped onto the fade-in head (seamless), peak -1 dBFS, 16-bit dithered, lossless FLAC",
                                      "sources": ["music_menu:qubodup-yd-DarkShrineLoop-OpenGameArt.ogg"], "channels": 2,
                                      "peak_db": round(al.peak_db(body), 2), "loudness": round(al.loudness(body, 3.0), 1)}
    jingle = normalize(S(KJIN + "Hit jingles/jingles_HIT15.ogg", mono=False))
    pad = choir(["D3", "A3", "D4", "F#4"], 2.4, attack=0.1, release=1.6, seed=31)
    x = mix([(jingle, 0, 0), (normalize(pad), 0.05, -6, -0.4), (normalize(choir(["A3", "D4", "A4"], 2.4, attack=0.1, release=1.6, seed=32)), 0.05, -7, 0.4),
             (normalize(bell(hz("D4"), 2.2, BELL_CHURCH_MAJOR, decay_scale=0.7)), 0.0, -6, 0.0),
             (sub(1.2, 75.0, 40.0, 0.4), 0, -6, 0.0)], length=2.4, stereo=True)
    out("music", "victory_stinger", reverb(x, 1.6, 0.55, 0.28), "stinger", "Kenney orchestral hit jingle (HIT15) + D major choir (wide) + major bell + sub",
        stereo=True)
    jingle = normalize(S(KJIN + "Pizzicato jingles/jingles_PIZZI07.ogg", mono=False))
    x = mix([(jingle, 0, -2), (normalize(strings(["D2", "A2", "D3", "F3"], 2.6, attack=0.35, release=1.5, seed=41)), 0.0, -4, -0.3),
             (normalize(strings(["A#1", "F2", "D3"], 1.6, attack=0.4, release=1.0, seed=42)), 1.0, -6, 0.3),
             (normalize(bell(hz("D2"), 2.6, BELL_CHURCH, decay_scale=1.0)), 1.15, -4, 0.0),
             (sub(1.4, 50.0, 30.0, 0.6), 1.15, -8, 0.0)], length=2.8, stereo=True)
    out("music", "defeat_stinger", reverb(x, 1.8, 0.6, 0.3), "stinger",
        "Kenney descending pizzicato jingle (PIZZI07, F-E-D#-D) + low D minor strings falling to Bb + bell toll D2 + sub", stereo=True)


GROUPS = {"combat": combat, "hero": hero, "pickups": pickups, "director": director, "enemies": enemies, "boss": boss, "ui": ui,
          "knight": knight, "barbarian": barbarian, "mage": mage, "rogue": rogue, "shared": shared,
          "ambience": ambience, "music": music}

if __name__ == "__main__":
    if os.path.exists(RECORDS_PATH):
        with open(RECORDS_PATH, "r", encoding="utf-8") as f:
            RECORDS.update(json.load(f))
    names = sys.argv[1:] or list(GROUPS)
    for name in names:
        print(f"== {name}")
        GROUPS[name]()
    with open(RECORDS_PATH, "w", encoding="utf-8") as f:
        json.dump(RECORDS, f, indent=1)
    print("sources used:", ", ".join(dsp.used_sources()))
