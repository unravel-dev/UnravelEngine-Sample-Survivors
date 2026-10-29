"""Synthesis, layering and mastering helpers for the Barrowlight sound library (mono float64 at 44.1 kHz unless a
function says stereo: shape (n, 2))."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402

RATE = al.RATE
RNG = np.random.default_rng(2026)
PEAK = 10.0 ** (-1.0 / 20.0)

# Pack prefixes (relative to the downloads folder).
R80 = "sfx_80_rpg/extracted/"
RPG = "sfx_rpg_sound_pack/extracted/RPG Sound Pack/"
SWISH = "sfx_swishes/extracted/swishes/swish-{}.wav"
BREAK = "sfx_bone_breaks/extracted/Deep Break {}.wav"
RATTLE = "sfx_bones_rattle/extracted/{}.ogg"
GEM = "sfx_gem/gem-gather-stereo.wav"
SP01 = "sfx_soundpack01/extracted/"
KIMP = "kenney_impact_sounds/extracted/Audio/"
KUI = "kenney_interface_sounds/extracted/Audio/"
KSF = "kenney_scifi_sounds/extracted/Audio/"
KJIN = "kenney_music_jingles/extracted/Audio/"
KRPG = "kenney_rpg_audio/extracted/Audio/"

NOTE = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}


def hz(name):
    """Frequency of a note name like 'D3' or 'F#5'."""
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2.0 ** ((NOTE[pitch] + 12 * (octave - 4)) / 12.0)


def T(seconds):
    return int(round(seconds * RATE))


def tl(seconds):
    return np.arange(T(seconds)) / RATE


def db(gain_db):
    return 10.0 ** (gain_db / 20.0)


# ============================================================================================ sources

_SOURCES_USED = set()
_CURRENT = set()


def used_sources():
    return sorted(_SOURCES_USED)


def take_current_sources():
    """Pack files loaded since the last call (for the manifest's 'made of' column)."""
    current = sorted(_CURRENT)
    _CURRENT.clear()
    return current


def onset_trim(x, rel_db=-40.0, pre=0.002):
    level = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    peak = level.max()
    if peak <= 0:
        return x
    index = int(np.argmax(level > peak * db(rel_db)))
    return x[max(0, index - T(pre)):]


def normalize(x, kind="peak", level_db=0.0):
    if kind == "peak":
        return x / (np.max(np.abs(x)) + 1e-12) * db(level_db)
    return x * db(level_db - al.loudness(x))


def repitch(x, ratio):
    """Plays x `ratio` times faster (pitch and speed together)."""
    if ratio == 1.0:
        return x
    return al.resample(x, RATE * ratio, RATE)


def cut(x, length, fade=0.03, curve=2.0):
    n = T(length)
    if len(x) > n:
        x = x[:n].copy()
    f = min(T(fade), len(x))
    if f > 0:
        ramp = np.linspace(1.0, 0.0, f) ** curve
        x[-f:] *= ramp if x.ndim == 1 else ramp[:, None]
    return x


def S(rel, start=None, end=None, pitch=1.0, length=None, fade=0.03, trim=True, hp=None, lp=None, norm="peak",
      mono=True, reverse=False):
    """A pack file as a layer: cut, trimmed to its onset, re-pitched, filtered, normalized (peak 0 dBFS)."""
    _SOURCES_USED.add(rel.split("/")[0])
    _CURRENT.add(rel.split("/")[0] + ":" + rel.split("/")[-1])
    x = al.src(rel, mono=mono, start=start, end=end)
    if trim:
        x = onset_trim(x)
    if pitch != 1.0:
        x = repitch(x, pitch)
    if hp:
        x = al.filt(x, "high", hp)
    if lp:
        x = al.filt(x, "low", lp)
    if length:
        x = cut(x, length, fade)
    if reverse:
        x = x[::-1].copy()
    return normalize(x, norm)


def mix(layers, length=None, stereo=False):
    """layers: (signal, at_seconds, gain_db[, pan -1..1]). Mono layers are panned (constant power) in stereo."""
    end = max(T(at) + len(sig) for sig, at, *_ in layers)
    n = T(length) if length else end
    out = np.zeros((n, 2)) if stereo else np.zeros(n)
    for layer in layers:
        sig, at, gain = layer[0], layer[1], layer[2]
        pan = layer[3] if len(layer) > 3 else 0.0
        a = T(at)
        if a >= n:
            continue
        seg = sig[:n - a] * db(gain)
        if stereo:
            if seg.ndim == 1:
                angle = (pan + 1.0) * np.pi / 4.0
                out[a:a + len(seg), 0] += seg * np.cos(angle) * np.sqrt(2.0)
                out[a:a + len(seg), 1] += seg * np.sin(angle) * np.sqrt(2.0)
            else:
                out[a:a + len(seg)] += seg
        else:
            out[a:a + len(seg)] += seg if seg.ndim == 1 else seg.mean(axis=1)
    return out


# ============================================================================================ synthesis

def white(seconds):
    return RNG.standard_normal(T(seconds)) * 0.35


def brown(seconds, corner=60.0):
    x = al.filt(RNG.standard_normal(T(seconds) + T(0.5)), "low", corner, order=2)[T(0.5):]
    return x / (np.max(np.abs(x)) + 1e-12)


def env(t, start, attack, tau, hold=0.0):
    local = t - start
    rise = np.clip(local / max(attack, 1e-4), 0.0, 1.0)
    fall = np.exp(-np.clip(local - attack - hold, 0.0, None) / tau)
    return np.where(local < 0.0, 0.0, rise * fall)


def swell(t, attack, release, total=None, curve=2.0):
    """Smooth rise over `attack`, smooth fall over the last `release` seconds."""
    total = total if total is not None else t[-1]
    up = np.clip(t / max(attack, 1e-4), 0.0, 1.0)
    down = np.clip((total - t) / max(release, 1e-4), 0.0, 1.0)
    return (0.5 - 0.5 * np.cos(np.pi * up)) * (0.5 - 0.5 * np.cos(np.pi * down)) ** (curve / 2.0)


def glide(t, f0, f1, tau, start=0.0):
    local = np.clip(t - start, 0.0, None)
    return f1 + (f0 - f1) * np.exp(-local / tau)


def osc(freq, t, kind="sine", harmonics=30):
    freq = np.broadcast_to(np.asarray(freq, dtype=float), t.shape)
    phase = 2.0 * np.pi * np.cumsum(freq) / RATE
    if kind == "sine":
        return np.sin(phase)
    top = float(np.max(freq))
    count = max(1, min(harmonics, int(9000.0 / max(top, 1.0))))
    out = np.zeros_like(t)
    for k in range(1, count + 1):
        if kind == "saw":
            out += np.sin(k * phase) / k
        elif kind == "square" and k % 2 == 1:
            out += np.sin(k * phase) / k
    return out


def flicker(t, low, high, count=6, depth=0.5):
    mod = np.zeros_like(t)
    for _ in range(count):
        mod += np.sin(2.0 * np.pi * RNG.uniform(low, high) * t + RNG.uniform(0, 2 * np.pi))
    mod /= count
    return 1.0 - depth * 0.5 + depth * 0.5 * mod


def crackles(seconds, start, end, rate, low, high, decay=0.004, density_tau=None):
    out = np.zeros(T(seconds))
    length = T(0.03)
    shape = np.exp(-np.arange(length) / (decay * RATE))
    moment = start
    while True:
        moment += RNG.exponential(1.0 / rate)
        if moment >= end:
            break
        if density_tau is not None and RNG.uniform() > np.exp(-(moment - start) / density_tau):
            continue
        i = T(moment)
        if i + length >= len(out):
            break
        out[i:i + length] += RNG.standard_normal(length) * shape * RNG.uniform(0.3, 1.0)
    return al.band(out, low, high)


def reverb(x, seconds=1.2, tau=0.35, wet=0.25, damping=4000.0, seed=None):
    """Convolution with a decaying, darkening noise tail. Stereo input gets a different tail per channel."""
    rng = np.random.default_rng(seed) if seed is not None else RNG
    t = tl(seconds)

    def tail():
        ir = rng.standard_normal(len(t)) * np.exp(-t / tau)
        ir = al.filt(ir, "low", damping)
        pre = T(0.012)
        ir[:pre] *= np.linspace(0.0, 1.0, pre)
        return ir / (np.sqrt(np.sum(ir ** 2)) + 1e-12)

    def convolve(sig, ir):
        size = len(sig) + len(ir)
        padded = np.concatenate([sig, np.zeros(len(ir))])
        return np.fft.irfft(np.fft.rfft(padded, size) * np.fft.rfft(ir, size), size)[:len(padded)]

    if x.ndim == 1:
        dry = np.concatenate([x, np.zeros(len(t))])
        return dry * (1.0 - wet) + convolve(x, tail()) * wet * 3.0
    channels = [np.concatenate([x[:, c], np.zeros(len(t))]) * (1.0 - wet) + convolve(x[:, c], tail()) * wet * 3.0
                for c in range(2)]
    return np.stack(channels, axis=1)


def saturate(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)


def noise_boom(seconds, attack, tau, f_from, f_to, sweep_tau, start=0.0):
    """Dark noise whose low-pass closes from f_from to f_to: an air blast, not a tone."""
    t = tl(seconds)
    cutoff = glide(t, f_from, f_to, sweep_tau, start)
    return al.sweep_filter(white(seconds), "low", cutoff) * env(t, start, attack, tau)


def sub_boom(seconds, f0, f1, tau, attack=0.004, glide_tau=0.12, drive=1.5):
    """Sub-bass thump below 80 Hz: a falling sine, lightly saturated so small speakers still hint at it."""
    t = tl(seconds)
    return saturate(osc(glide(t, f0, f1, glide_tau), t) * env(t, 0.0, attack, tau), drive)


def whoosh(seconds, f0, f1, q=1.2, peak=0.5, sharpness=2.0, gain=1.0):
    """Band-passed noise whose centre moves from f0 to f1 (exponential path) under a bump envelope."""
    t = tl(seconds)
    x = t / max(t[-1], 1e-6)
    centre = f0 * (f1 / f0) ** x
    bump = np.where(x < peak, (x / max(peak, 1e-4)) ** sharpness, ((1.0 - x) / max(1.0 - peak, 1e-4)) ** (sharpness * 0.7))
    return al.sweep_filter(white(seconds), "band", centre, q) * bump * gain * 4.0


BELL_CHURCH = [(0.5, 0.55, 3.5), (1.0, 1.0, 2.4), (1.19, 0.55, 1.8), (1.5, 0.35, 1.4), (2.0, 0.65, 1.2),
               (2.52, 0.3, 0.9), (3.0, 0.22, 0.7), (4.07, 0.16, 0.5), (5.43, 0.1, 0.3)]
BELL_CHURCH_MAJOR = [(r if r != 1.19 else 1.26, a, d) for r, a, d in BELL_CHURCH]
BELL_CHIME = [(1.0, 1.0, 1.0), (2.76, 0.45, 0.45), (5.40, 0.22, 0.2), (8.93, 0.08, 0.1)]
BELL_GLASS = [(1.0, 1.0, 0.35), (2.32, 0.35, 0.18), (4.25, 0.18, 0.08), (6.63, 0.08, 0.05)]


def bell(f0, seconds, partials=BELL_CHIME, decay_scale=1.0, strike=0.3, beat=0.6):
    """Additive bell: inharmonic partials with their own decays, a slow beat on each and a noisy strike."""
    t = tl(seconds)
    out = np.zeros_like(t)
    for ratio, amp, decay in partials:
        f = f0 * ratio
        if f > RATE * 0.45:
            continue
        phase = RNG.uniform(0, 2 * np.pi)
        tone = np.sin(2 * np.pi * f * t + phase) + 0.5 * np.sin(2 * np.pi * (f + beat) * t + phase)
        out += tone * amp * np.exp(-t / (decay * decay_scale))
    out *= np.clip(t / 0.002, 0.0, 1.0)
    if strike > 0:
        hit = al.band(white(seconds), f0 * 1.5, min(f0 * 12, 16000.0)) * env(t, 0.0, 0.0005, 0.012)
        out += hit * strike * 6.0
    return out


def choir(notes, seconds, attack=0.5, release=1.0, vowel="a", detune=8.0, vibrato=0.004, breath=0.08, seed=None):
    """Voices: three detuned saws per note with their own vibrato, shaped by vowel formants."""
    rng = np.random.default_rng(seed) if seed is not None else RNG
    t = tl(seconds)
    source = np.zeros_like(t)
    for name in notes:
        f = hz(name) if isinstance(name, str) else name
        for cents in (-detune, 0.0, detune * 0.8):
            rate = rng.uniform(4.6, 5.8)
            mod = 1.0 + vibrato * np.sin(2 * np.pi * rate * t + rng.uniform(0, 2 * np.pi)) * np.clip(t / 0.6, 0, 1)
            source += osc(f * 2 ** (cents / 1200.0) * mod, t, "saw")
    formants = {"a": [(700, 1.0, 9), (1150, 0.55, 10), (2650, 0.3, 16), (3500, 0.12, 18)],
                "o": [(450, 1.0, 8), (800, 0.6, 9), (2830, 0.12, 16)],
                "u": [(325, 1.0, 7), (700, 0.35, 8), (2530, 0.06, 16)],
                "e": [(400, 1.0, 8), (2100, 0.4, 14), (2900, 0.2, 16)]}[vowel]
    voiced = np.zeros_like(t)
    for f, g, q in formants:
        voiced += al.filt(source, "band", f, q=q) * g
    air = al.band(white(seconds), 600.0, 5000.0) * breath * 4.0
    out = (voiced / (np.max(np.abs(voiced)) + 1e-12) + air * 0.3) * swell(t, attack, release)
    return out


def brass(notes, seconds, attack=0.12, release=0.4, bright=1400.0, vibrato=0.005, seed=None):
    """Horn section: stacked saws per note whose low-pass opens with the attack (the brassy "blat")."""
    rng = np.random.default_rng(seed) if seed is not None else RNG
    t = tl(seconds)
    source = np.zeros_like(t)
    for name in notes:
        f = hz(name) if isinstance(name, str) else name
        for cents in (-5.0, 4.0):
            mod = 1.0 + vibrato * np.sin(2 * np.pi * rng.uniform(4.5, 5.5) * t + rng.uniform(0, 6.28)) * np.clip((t - 0.2) / 0.4, 0, 1)
            source += osc(f * 2 ** (cents / 1200.0) * mod, t, "saw")
    opening = np.clip(t / max(attack * 1.5, 1e-3), 0.0, 1.0) ** 0.7
    cutoff = 250.0 + bright * opening * (0.75 + 0.25 * np.exp(-t / 0.5))
    shaped = al.sweep_filter(source / max(len(notes), 1), "low", cutoff, q=1.1)
    return saturate(shaped * 0.6, 1.4) * swell(t, attack, release)


def strings(notes, seconds, attack=0.4, release=1.0, cutoff=1800.0, seed=None):
    """Bowed string section: many detuned saws, gentle vibrato, low-passed and slightly hollow."""
    rng = np.random.default_rng(seed) if seed is not None else RNG
    t = tl(seconds)
    source = np.zeros_like(t)
    for name in notes:
        f = hz(name) if isinstance(name, str) else name
        for cents in (-9.0, -3.0, 4.0, 10.0):
            mod = 1.0 + 0.0035 * np.sin(2 * np.pi * rng.uniform(4.8, 6.0) * t + rng.uniform(0, 6.28))
            source += osc(f * 2 ** (cents / 1200.0) * mod, t, "saw")
    shaped = al.filt(al.filt(source, "low", cutoff, order=2), "peak", 900.0, q=0.8, gain_db=-4.0)
    bow = al.band(white(seconds), 1500.0, 6000.0) * 0.02
    return (shaped / (np.max(np.abs(shaped)) + 1e-12) + bow) * swell(t, attack, release)


def karplus(f0, seconds, damping=0.994, brightness=0.5, pick=None):
    """Plucked string (Karplus-Strong)."""
    n = T(seconds)
    period = max(2, int(RATE / f0))
    buf = (pick if pick is not None else RNG.uniform(-1, 1, period)).tolist()
    out = [0.0] * n
    previous = 0.0
    for i in range(n):
        value = buf[i % period]
        out[i] = value
        new = damping * (brightness * value + (1.0 - brightness) * previous)
        previous = value
        buf[i % period] = new
    return np.array(out)


def chime_run(notes, step, seconds, partials=BELL_CHIME, decay_scale=0.6, pans=None, stereo=False):
    layers = []
    for i, name in enumerate(notes):
        tone = bell(hz(name), seconds - i * step, partials, decay_scale=decay_scale, strike=0.15)
        pan = pans[i] if pans else 0.0
        layers.append((tone, i * step, 0.0, pan))
    return mix(layers, length=seconds, stereo=stereo)


def loopable(x, overlap):
    """Makes x loop seamlessly: its last `overlap` seconds fade into its first ones (equal power). The result
    starts right after the head and ends with the tail-to-head crossfade, so the wrap continues the head."""
    n = T(overlap)
    head, body, tail = x[:n], x[n:len(x) - n], x[len(x) - n:]
    fade = np.linspace(0.0, np.pi / 2, n)
    shape = (lambda v: v) if x.ndim == 1 else (lambda v: v[:, None])
    joined = tail * shape(np.cos(fade)) + head * shape(np.sin(fade))
    return np.concatenate([body, joined])


# ============================================================================================ mastering

TARGETS = {
    # category: (momentary loudness target, most gain a limiter may add to reach it)
    "impact_heavy": (-9.0, 6.0),
    "impact": (-11.0, 6.0),
    "weapon": (-12.0, 5.0),
    "enemy": (-13.0, 4.0),
    "voice": (-12.0, 4.0),
    "stinger": (-11.0, 3.0),
    "pickup": (-15.0, 3.0),
    "ui": (-17.0, 2.0),
    "ui_tick": (-20.0, 2.0),
    "loop": (-15.0, 3.0),
    "ambience": (-26.0, 0.0),
}


def limit(x, ceiling=PEAK, release=0.06, lookahead=0.0015):
    level = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    need = np.minimum(1.0, ceiling / np.maximum(level, 1e-12))
    la = max(1, T(lookahead))
    gain = need.copy()
    for k in range(1, la + 1):
        gain[:-k] = np.minimum(gain[:-k], need[k:])
    coef = float(np.exp(-1.0 / (release * RATE)))
    smooth = []
    current = 1.0
    for v in gain.tolist():
        current = v if v < current else v + (current - v) * coef
        smooth.append(current)
    smooth = np.convolve(np.array(smooth), np.ones(la) / la, mode="full")[:len(gain)]
    smooth = np.minimum(smooth, 1.0)
    return x * (smooth if x.ndim == 1 else smooth[:, None])


def trim_silence(x, start_db=-60.0, end_db=-42.0, pre=0.0005, post=0.01):
    """Cuts leading samples under start_db (sample peak) and the tail once its 10 ms RMS stays under end_db,
    both relative to the peak; a tail that was cut fades over 10% of the length (5-250 ms)."""
    level = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    idx = np.nonzero(level > level.max() * db(start_db))[0]
    if len(idx) == 0:
        return x
    x = x[max(0, idx[0] - T(pre)):]
    level = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    size = T(0.01)
    running = np.concatenate([[0.0], np.cumsum(level ** 2)])
    rms = np.sqrt(np.maximum((running[size:] - running[:-size]) / size, 0.0))
    loud = np.nonzero(rms > level.max() * db(end_db))[0]
    end = min(len(x), int(loud[-1]) + size + T(post)) if len(loud) else len(x)
    if end >= len(x):
        return x
    x = x[:end].copy()
    fade = int(min(max(0.1 * end, T(0.005)), T(0.25)))
    ramp = 0.5 + 0.5 * np.cos(np.linspace(0.0, np.pi, fade))
    x[-fade:] *= ramp if x.ndim == 1 else ramp[:, None]
    return x


def fades(x, fade_in=0.003, fade_out=0.005):
    x = x.copy()
    a, b = T(fade_in), T(fade_out)
    up = 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, a))
    down = 0.5 + 0.5 * np.cos(np.linspace(0.0, np.pi, b))
    if x.ndim == 1:
        x[:a] *= up
        x[-b:] *= down
    else:
        x[:a] *= up[:, None]
        x[-b:] *= down[:, None]
    return x


def master(x, category, highpass=28.0, keep_edges=False):
    """DC and rumble out, silence trimmed, 3 ms in / 5 ms out fades, peak -1 dBFS, then loudness to the
    category target (attenuate when louder; limit up to the category's push when quieter)."""
    x = x - (np.mean(x) if x.ndim == 1 else np.mean(x, axis=0))
    if not keep_edges:
        # A seamless loop skips the high-pass: its start-up transient would break the seam.
        x = al.filt(x, "high", highpass, order=2)
    if not keep_edges:
        x = trim_silence(x)
        x = fades(x)
    x = x / (np.max(np.abs(x)) + 1e-12) * PEAK
    target, push = TARGETS[category]
    loud = al.loudness(x)
    if loud > target:
        x = x * db(target - loud)
    elif push > 0.0 and loud < target:
        boost = min(target - loud, push)
        x = limit(x * db(boost))
        if np.max(np.abs(x)) > PEAK:
            x = x / np.max(np.abs(x)) * PEAK
    if not keep_edges:
        x = fades(x, 0.001, 0.002)
    return x
