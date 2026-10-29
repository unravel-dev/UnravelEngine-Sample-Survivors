"""Melodic contour of the Kenney jingles: dominant pitch per note onset (harmonic product spectrum)."""
import glob
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402

NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]


def note(freq):
    midi = int(round(69 + 12 * np.log2(freq / 440.0)))
    return f"{NAMES[midi % 12]}{midi // 12 - 1}", midi


def contour(x):
    size, hop = 4096, 1024
    window = np.hanning(size)
    freqs = np.fft.rfftfreq(size, 1.0 / al.RATE)
    out = []
    levels = []
    for i in range(0, len(x) - size, hop):
        frame = x[i:i + size] * window
        spec = np.abs(np.fft.rfft(frame))
        levels.append(np.sqrt(np.mean(frame ** 2)))
        hps = spec.copy()
        for k in (2, 3):
            dec = spec[::k]
            hps[:len(dec)] *= dec
            hps[len(dec):] = 0
        band = (freqs > 70) & (freqs < 1500)
        idx = np.argmax(np.where(band, hps, 0))
        out.append(freqs[idx])
    levels = np.array(levels)
    loud = levels > levels.max() * 0.25
    notes = []
    for f, ok in zip(out, loud):
        if ok and f > 0:
            name, midi = note(f)
            if not notes or notes[-1][1] != midi:
                notes.append((name, midi))
    return notes


pattern = sys.argv[1] if len(sys.argv) > 1 else "HIT"
for path in sorted(glob.glob(os.path.join(al.DOWNLOADS, "kenney_music_jingles", "extracted", "Audio", "*", f"jingles_{pattern}*.ogg"))):
    x = al.src(os.path.relpath(path, al.DOWNLOADS))
    notes = contour(x)
    seq = " ".join(n for n, _ in notes[:12])
    trend = notes[-1][1] - notes[0][1] if notes else 0
    print(f"{os.path.basename(path):22s} {len(x) / al.RATE:4.2f}s trend {trend:+3d}  {seq}")
