"""Battle: is 54..108 s a repeat of 0..54 s? Menu: how long are the silent/fading edges?"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402

data, rate = al.load(os.path.join(al.DOWNLOADS, "music_battle/determined_pursuit_loop.wav"))
half = data.shape[0] // 2
print("battle half frames", half, half / rate)
for start in (0.0, 30.0, 35.0, 52.0):
    a = data[int(start * rate):int(start * rate) + rate * 2]
    b = data[half + int(start * rate):half + int(start * rate) + rate * 2]
    corr = float(np.sum(a * b) / (np.sqrt(np.sum(a * a) * np.sum(b * b)) + 1e-12))
    diff_db = 20 * np.log10(np.sqrt(np.mean((a - b) ** 2)) / (np.sqrt(np.mean(a ** 2)) + 1e-12) + 1e-12)
    print(f"  t={start:5.1f}s corr {corr:.4f} residual {diff_db:.1f} dB")
# Best alignment of the second half (lag search around 54 s on a 4 s window at 10 s).
a = data[10 * rate:14 * rate].mean(axis=1)
best = (0, 0)
for lag in range(half - 20, half + 20):
    b = data[10 * rate + lag:14 * rate + lag].mean(axis=1)
    corr = float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b)))
    if best is None or corr > best[0]:
        best = (corr, lag)
print("  best lag", best, best[1] / rate)

data, rate = al.load(os.path.join(al.DOWNLOADS, "music_menu/qubodup-yd-DarkShrineLoop-OpenGameArt.ogg"))
mono = data.mean(axis=1)
hop = rate // 4
levels = [20 * np.log10(np.sqrt(np.mean(mono[i:i + hop] ** 2)) + 1e-9) for i in range(0, len(mono) - hop, hop)]
print("menu 0.25 s levels first 3 s", np.round(levels[:12], 1).tolist())
print("menu 0.25 s levels last 4 s", np.round(levels[-16:], 1).tolist())
nz = np.nonzero(np.abs(mono) > 1e-4)[0]
print("menu first/last sample above -80 dB", nz[0] / rate, nz[-1] / rate, "of", len(mono) / rate)
