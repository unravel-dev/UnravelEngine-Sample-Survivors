"""Band shares (sub < 80, low 80-300, mid 300-3k, high > 3k Hz, % of energy) and centroid of chosen files."""
import os
import sys
import wave

import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "..", "data", "Audio")
for rel in sys.argv[1:]:
    with wave.open(os.path.join(ROOT, rel), "rb") as w:
        x = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").reshape(-1, w.getnchannels()).mean(axis=1) / 32768.0
    spec = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / 44100)
    total = spec.sum() + 1e-12
    shares = [spec[(f >= a) & (f < b)].sum() / total * 100 for a, b in ((0, 80), (80, 300), (300, 3000), (3000, 22050))]
    centroid = (spec * f).sum() / total
    env = np.sqrt(np.convolve(x ** 2, np.ones(441) / 441, mode="same"))
    attack_ms = np.argmax(env > env.max() * 0.5) / 44.1
    print(f"{rel:36s} sub {shares[0]:4.1f} low {shares[1]:4.1f} mid {shares[2]:4.1f} high {shares[3]:4.1f}  c {centroid:6.0f} Hz  half-level at {attack_ms:5.0f} ms")
