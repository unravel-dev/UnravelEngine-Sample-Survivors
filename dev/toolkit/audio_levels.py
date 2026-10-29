"""Loudness per band of the generated spell sounds, so a new mix can be held against the ones that work.
Prints the loudest 0.4 s RMS overall and in three bands, and the crest factor (peak over that RMS)."""
import sys
import wave

import numpy as np

RATE = 44100
WINDOW = 0.4
BANDS = {"low": (35.0, 200.0), "mid": (200.0, 2000.0), "high": (2000.0, 16000.0)}


def load(path):
    w = wave.open(path)
    return np.frombuffer(w.readframes(w.getnframes()), "<i2") / 32768.0


def band_limit(x, low, high):
    spectrum = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1.0 / RATE)
    spectrum[(freqs < low) | (freqs > high)] = 0.0
    return np.fft.irfft(spectrum, len(x))


def loudest_rms(x):
    size = int(WINDOW * RATE)
    hop = int(0.05 * RATE)
    return max(np.sqrt(np.mean(x[i:i + size] ** 2)) for i in range(0, max(1, len(x) - size), hop))


def db(value):
    return 20.0 * np.log10(value + 1e-9)


for path in sys.argv[1:]:
    x = load(path)
    overall = loudest_rms(x)
    bands = "  ".join(f"{name} {db(loudest_rms(band_limit(x, *edges))):6.1f}" for name, edges in BANDS.items())
    print(f"{path.split('/')[-1]:20s} RMS {db(overall):6.1f}  {bands}  crest {db(np.max(np.abs(x)) / overall):5.1f} dB")
