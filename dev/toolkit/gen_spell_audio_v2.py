"""Spell sounds, second pass: layered designs instead of single filtered noises (mono, 44.1 kHz, 16 bit,
license-free, procedural).

    python gen_spell_audio_v2.py [out_dir]

thunder.wav        charge buzz, then at 0.25 s a sharp crack with restrikes, tearing sizzle, low boom and a
                   rolling rumble (LightningSpell casts and strikes 0.25 s later)
fireball_cast.wav  ignition "fwoomp" and a flickering flame roar for the flight
explosion.wav      crack, sub boom, burning roar, debris and a rumble tail
vortex.wav         a rising, whirling drone and shimmer for 3 s that sucks in, then bursts
Also writes a spectrogram PNG per sound for checking.
"""
import os
import sys
import wave

import numpy as np
from PIL import Image

RATE = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                         "particles_src", "Audio")
RNG = np.random.default_rng(7)
PEAK = 10 ** (-1.0 / 20.0)


# ============================================================================================ DSP

def timeline(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def white(seconds):
    return RNG.standard_normal(int(seconds * RATE)) * 0.35


def brown(seconds):
    """Random walk noise (1/f^2), leaky so it stays centred."""
    out = np.zeros(int(seconds * RATE))
    steps = RNG.standard_normal(len(out)) * 0.02
    value = 0.0
    for i in range(len(out)):
        value = value * 0.9995 + steps[i]
        out[i] = value
    return out / (np.max(np.abs(out)) + 1e-9)


def biquad(signal, kind, freq, q=0.707):
    """RBJ biquad. freq may be a number or an array: a moving cutoff gets new coefficients every sample, as
    stepping them in blocks adds a buzzing, metallic "zipper" tone."""
    freqs = np.broadcast_to(np.asarray(freq, dtype=float), signal.shape)
    w0 = 2.0 * np.pi * np.minimum(freqs, RATE * 0.45) / RATE
    alpha = np.sin(w0) / (2.0 * q)
    cos = np.cos(w0)
    if kind == "low":
        b0 = b2 = (1 - cos) / 2
        b1 = 1 - cos
    elif kind == "high":
        b0 = b2 = (1 + cos) / 2
        b1 = -(1 + cos)
    else:
        b0, b1, b2 = alpha, np.zeros_like(alpha), -alpha
    a0 = 1 + alpha
    b0, b1, b2, a1, a2 = (b0 / a0).tolist(), (b1 / a0).tolist(), (b2 / a0).tolist(), \
        (-2 * cos / a0).tolist(), ((1 - alpha) / a0).tolist()
    out = [0.0] * len(signal)
    x1 = x2 = y1 = y2 = 0.0
    for i, x0 in enumerate(signal.tolist()):
        y0 = b0[i] * x0 + b1[i] * x1 + b2[i] * x2 - a1[i] * y1 - a2[i] * y2
        out[i] = y0
        x2, x1, y2, y1 = x1, x0, y1, y0
    return np.array(out)


def band(signal, low, high):
    return biquad(biquad(signal, "high", low), "low", high)


def env(t, start, attack, decay_tau, hold=0.0):
    """0 before start, linear attack, optional hold, then exponential decay with time constant decay_tau."""
    local = t - start
    rise = np.clip(local / max(attack, 1e-4), 0.0, 1.0)
    fall = np.exp(-np.clip(local - attack - hold, 0.0, None) / decay_tau)
    return np.where(local < 0.0, 0.0, rise * fall)


def sweep_sine(t, start, f0, f1, tau):
    """A sine starting at `start` whose frequency glides exponentially from f0 to f1 (time constant tau)."""
    local = np.clip(t - start, 0.0, None)
    freq = f1 + (f0 - f1) * np.exp(-local / tau)
    phase = 2.0 * np.pi * np.cumsum(freq) / RATE
    return np.where(t >= start, np.sin(phase), 0.0)


def flicker(t, low, high, count=6, depth=0.5):
    """Irregular amplitude movement: a few random sines between low and high Hz."""
    mod = np.zeros_like(t)
    for _ in range(count):
        mod += np.sin(2.0 * np.pi * RNG.uniform(low, high) * t + RNG.uniform(0, 2 * np.pi))
    mod /= count
    return 1.0 - depth * 0.5 + depth * 0.5 * mod


def crackles(t, start, end, rate, low, high, decay=0.004, density_tau=None, gain=1.0):
    """Sparse pops: short decaying noise bursts, `rate` per second (thinning with density_tau)."""
    out = np.zeros_like(t)
    length = int(0.03 * RATE)
    shape = np.exp(-np.arange(length) / (decay * RATE))
    moment = start
    while moment < end:
        moment += RNG.exponential(1.0 / rate)
        if density_tau is not None and RNG.uniform() > np.exp(-(moment - start) / density_tau):
            continue
        i = int(moment * RATE)
        if i + length >= len(out):
            break
        out[i:i + length] += RNG.standard_normal(length) * shape * RNG.uniform(0.3, 1.0)
    return band(out, low, high) * gain


def reverb(signal, seconds, decay_tau, mix, damping_hz=4000.0):
    """Convolution with a decaying, darkening noise tail."""
    t = timeline(seconds)
    ir = RNG.standard_normal(len(t)) * np.exp(-t / decay_tau)
    ir = biquad(ir, "low", damping_hz)
    ir[: int(0.012 * RATE)] *= np.linspace(0.0, 1.0, int(0.012 * RATE))
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    size = len(signal) + len(ir)
    wet = np.fft.irfft(np.fft.rfft(signal, size) * np.fft.rfft(ir, size), size)[: len(signal)]
    return signal * (1.0 - mix) + wet * mix * 3.0


def saturate(signal, drive):
    return np.tanh(signal * drive) / np.tanh(drive)


def master(signal, drive=1.6):
    """Drop what no speaker plays (below 35 Hz), glue the layers with soft saturation and bring the peak
    to -1 dBFS."""
    signal = biquad(biquad(signal - np.mean(signal), "high", 35.0), "high", 35.0)
    signal = saturate(signal / (np.max(np.abs(signal)) + 1e-9), drive)
    fade = int(0.02 * RATE)
    signal[-fade:] *= np.linspace(1.0, 0.0, fade)
    return signal / (np.max(np.abs(signal)) + 1e-9) * PEAK


# ============================================================================================ output

def write(name, samples):
    data = (np.clip(samples, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(data.tobytes())
    window = 0.4
    loudest = max(np.sqrt(np.mean(samples[i:i + int(window * RATE)] ** 2))
                  for i in range(0, max(1, len(samples) - int(window * RATE)), int(0.05 * RATE)))
    low = band(samples, 20.0, 200.0)
    print(f"wrote {name}: {len(samples) / RATE:.2f} s, loudest {window} s RMS {20 * np.log10(loudest + 1e-9):.1f} dBFS, "
          f"below 200 Hz {20 * np.log10(np.sqrt(np.mean(low ** 2)) / (np.sqrt(np.mean(samples ** 2)) + 1e-9)):.1f} dB")
    spectrogram(name.replace(".wav", "_spectrogram.png"), samples)


def spectrogram(name, samples, height=256, width=800):
    size = 2048
    hop = max(1, (len(samples) - size) // width)
    frames = [np.abs(np.fft.rfft(samples[i:i + size] * np.hanning(size)))
              for i in range(0, len(samples) - size, hop)][:width]
    spec = np.array(frames).T
    freqs = np.fft.rfftfreq(size, 1.0 / RATE)
    # Log frequency axis 20 Hz - 16 kHz.
    rows = np.geomspace(20.0, 16000.0, height)
    idx = np.clip(np.searchsorted(freqs, rows), 0, len(freqs) - 1)
    image = 20 * np.log10(spec[idx] + 1e-6)
    image = np.clip((image - image.max() + 80.0) / 80.0, 0.0, 1.0)[::-1]
    Image.fromarray((image * 255).astype(np.uint8)).save(os.path.join(OUT, name))


# ============================================================================================ sounds

def thunder():
    duration, strike = 3.8, 0.25
    t = timeline(duration)
    # Charge: a buzzing hum that rises in pitch and crackles more densely until the strike.
    charge_mask = (t < strike).astype(float)
    hum_freq = 90.0 + 110.0 * np.clip(t / strike, 0.0, 1.0) ** 2
    phase = 2.0 * np.pi * np.cumsum(hum_freq) / RATE
    buzz = sum(np.sin(k * phase) / k for k in range(1, 12))
    buzz = band(buzz * (0.6 + 0.4 * np.sign(np.sin(2.0 * np.pi * 37.0 * t))), 150.0, 3500.0)
    buzz *= charge_mask * np.clip(t / strike, 0.0, 1.0) ** 1.5 * 0.35
    charge_pops = crackles(t, 0.0, strike, 70.0, 1500.0, 7000.0, gain=0.5) * charge_mask
    # Strike and restrikes: a hard snap (click plus a short saturated burst), the loudest thing in it.
    snap = np.zeros_like(t)
    for at, gain in ((strike, 1.0), (strike + 0.08, 0.6), (strike + 0.18, 0.45)):
        burst = white(duration) * env(t, at, 0.0005, 0.022) * gain
        snap += saturate(biquad(burst, "high", 500.0) * 6.0, 2.5) * 0.5
        snap += np.where(np.abs(t - at) < 0.0015, gain * 0.9, 0.0)
    # Tearing: noise ripped by fast irregular amplitude, the sound of the air splitting close by.
    tear = band(white(duration), 900.0, 6500.0) * flicker(t, 35.0, 95.0, count=8, depth=1.6)
    tear *= env(t, strike, 0.002, 0.16) * 1.1
    # Boom: a low impact with a falling pitch, saturated so it reads on small speakers too.
    boom = saturate(sweep_sine(t, strike, 75.0, 32.0, 0.25) * env(t, strike, 0.003, 0.45), 1.8) * 0.9
    boom += biquad(white(duration), "low", 180.0) * env(t, strike, 0.004, 0.5) * 2.2
    # Rumble: a long rolling low roar that swells and recedes.
    rumble = biquad(brown(duration), "low", 160.0) * 1.6
    rumble *= env(t, strike + 0.05, 0.25, 1.1) * flicker(t, 0.6, 2.2, count=4, depth=0.8)
    # Sizzle: residual crackles of electricity thinning out.
    sizzle = crackles(t, strike, 2.0, 140.0, 2500.0, 9000.0, decay=0.002, density_tau=0.45, gain=0.55)
    dry = buzz * 2.0 + charge_pops * 2.0 + snap * 2.2 + tear * 3.2 + sizzle * 2.6
    mix = reverb(dry, 2.2, 0.55, 0.32) + reverb(boom * 0.5 + rumble * 0.45, 2.5, 0.8, 0.2, damping_hz=900.0)
    write("thunder.wav", master(mix, drive=1.8))


def noise_boom(t, start, attack, decay_tau, cutoff_from, cutoff_to, sweep_tau, duration):
    """A boom made of noise, not a tone: dark noise whose low-pass closes from cutoff_from to cutoff_to.
    A pitched, saturated thump rings like struck metal; air and fire do not."""
    local = np.clip(t - start, 0.0, None)
    cutoff = cutoff_to + (cutoff_from - cutoff_to) * np.exp(-local / sweep_tau)
    return biquad(white(duration), "low", cutoff) * env(t, start, attack, decay_tau)


def fireball_cast():
    duration = 1.15
    t = timeline(duration)
    # Ignition: a gas "fwoomp" - a dark burst of air that closes down fast.
    whoomp = noise_boom(t, 0.0, 0.005, 0.16, 2600.0, 160.0, 0.06, duration) * 4.0
    # Roar: flame noise flickering at the slow, irregular rate of fire, opening up as it flies past.
    roar = band(white(duration), 120.0, 2600.0) * flicker(t, 3.0, 11.0, count=6, depth=0.6)
    roar = biquad(roar, "low", 700.0 + 2000.0 * np.clip(t / 0.45, 0.0, 1.0))
    rumble = biquad(white(duration), "low", 180.0) * flicker(t, 2.0, 7.0, count=4, depth=0.5) * 7.5
    sustain = np.clip(t / 0.05, 0.0, 1.0) * np.clip((duration - t) / 0.35, 0.0, 1.0)
    pops = crackles(t, 0.05, duration - 0.15, 22.0, 1200.0, 5000.0, decay=0.003, gain=0.6)
    mix = whoomp + (roar * 2.6 + rumble) * sustain + pops * 1.6 * sustain
    write("fireball_cast.wav", master(reverb(mix, 1.2, 0.3, 0.15), drive=1.3))


def explosion():
    duration = 3.2
    t = timeline(duration)
    # The crack only marks the onset: as the loudest peak it would set the level and leave the body quiet.
    crack = biquad(white(duration), "high", 300.0) * env(t, 0.0, 0.0008, 0.025) * 1.6
    # The blast: a noise boom closing from bright to deep, and a long low tail - no tones that could ring.
    blast = noise_boom(t, 0.0, 0.002, 0.55, 3500.0, 100.0, 0.15, duration) * 10.0
    deep = biquad(white(duration), "low", 110.0) * env(t, 0.005, 0.008, 0.75) * 18.0
    fire = band(white(duration), 150.0, 2400.0) * flicker(t, 3.0, 10.0, count=6, depth=0.7)
    fire *= env(t, 0.04, 0.06, 1.0) * 1.6
    debris = crackles(t, 0.06, 1.8, 70.0, 600.0, 4500.0, decay=0.005, density_tau=0.55, gain=0.8)
    thuds = np.zeros_like(t)
    for _ in range(6):
        at = RNG.uniform(0.45, 1.6)
        thuds += biquad(white(duration), "low", 260.0) * env(t, at, 0.003, 0.04) * RNG.uniform(0.8, 1.6)
    rumble = biquad(brown(duration), "low", 110.0) * env(t, 0.08, 0.25, 1.3) * flicker(t, 0.4, 1.5, 4, 0.5) * 2.4
    mix = reverb(crack + blast + fire * 2.0 + debris * 2.0 + thuds, 2.4, 0.6, 0.25)
    mix += reverb(deep + rumble * 0.8, 2.4, 0.9, 0.15, damping_hz=700.0)
    write("explosion.wav", master(mix, drive=1.9))


def vortex():
    duration, burst = 5.4, 3.0
    t = timeline(duration)
    channel = np.clip(t / burst, 0.0, 1.0)
    before = (t < burst).astype(float)
    # Drone: detuned saws whose pitch climbs and whose filter opens as the vortex spins up.
    drone = np.zeros_like(t)
    for detune in (0.0, 0.8, -1.1):
        freq = 55.0 * 2.0 ** (channel ** 1.6) + detune
        phase = 2.0 * np.pi * np.cumsum(freq) / RATE
        drone += sum(np.sin(k * phase) / k for k in range(1, 9))
    drone = biquad(drone, "low", 260.0 + 2400.0 * channel ** 1.4, q=1.4) * (0.15 + 0.85 * channel ** 1.2) * 0.45
    # Swirl: air whirling round, its amplitude spinning faster (1.5 to 10 turns a second).
    spin_rate = 1.5 + 8.5 * channel ** 1.5
    spin = 0.55 + 0.45 * np.sin(2.0 * np.pi * np.cumsum(spin_rate) / RATE)
    swirl = biquad(white(duration), "band", 400.0 + 2600.0 * channel ** 1.3, q=2.0)
    swirl *= spin * (0.2 + 0.8 * channel) * 1.6
    # Shimmer: glassy partials that gather as power builds.
    shimmer = np.zeros_like(t)
    for _ in range(14):
        freq = RNG.uniform(1100.0, 3600.0)
        shimmer += np.sin(2.0 * np.pi * freq * t + RNG.uniform(0, 6.28)) * (0.6 + 0.4 * np.sin(2.0 * np.pi * RNG.uniform(3, 9) * t))
    shimmer *= channel ** 2 * 0.03
    # Suck-in: a last rising rush before the burst.
    rush = biquad(white(duration), "high", 1200.0 + 4000.0 * np.clip((t - burst + 0.4) / 0.4, 0.0, 1.0))
    rush *= np.clip((t - (burst - 0.4)) / 0.4, 0.0, 1.0) ** 2 * before * 1.2
    channel_mix = (drone * 0.8 + swirl * 2.4 + shimmer * 2.5 + rush * 1.6) * before
    # Burst: a heavy drop, a crack, a bright scatter of partials and a whoosh flying outward.
    drop = saturate(sweep_sine(t, burst, 95.0, 30.0, 0.2) * env(t, burst, 0.003, 0.6), 2.0) * 1.1
    crack = biquad(white(duration), "high", 700.0) * env(t, burst, 0.0005, 0.03) * 2.0
    sparkle = np.zeros_like(t)
    for _ in range(24):
        freq = RNG.uniform(1800.0, 6500.0)
        local = np.clip(t - burst - RNG.uniform(0.0, 0.25), 0.0, None)
        sparkle += np.sin(2.0 * np.pi * freq * local) * np.exp(-local * RNG.uniform(2.0, 5.0)) * (local > 0)
    sparkle *= 0.06
    whoosh = biquad(white(duration), "band", 3200.0 * np.exp(-np.clip(t - burst, 0.0, None) / 0.35) + 250.0, q=1.2)
    whoosh *= env(t, burst, 0.01, 0.5) * 2.2
    mix = reverb(channel_mix + crack * 1.6 + sparkle * 2.5 + whoosh * 1.8, 2.8, 0.75, 0.35) + reverb(drop * 0.55, 2.0, 0.6, 0.2, damping_hz=700.0)
    write("vortex.wav", master(mix, drive=1.7))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    targets = sys.argv[2:] or ["thunder", "fireball_cast", "explosion", "vortex"]
    for target in targets:
        globals()[target]()
