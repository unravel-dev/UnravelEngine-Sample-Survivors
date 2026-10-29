"""Checks every file of the library: format, edges (no clicks), DC, peak, silence; loop seams; sub-bass of heavy hits."""
import json
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402

SURV = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOT = os.path.join(SURV, "..", "data", "Audio")
records = json.load(open(os.path.join(SURV, "audio_work", "records.json"), encoding="utf-8"))
problems = []
count = 0
for folder, _, names in os.walk(ROOT):
    for name in sorted(names):
        path = os.path.join(folder, name)
        rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
        if not all(32 < ord(c) < 127 for c in rel):
            problems.append(f"{rel}: non-ASCII name")
        if rel not in records:
            problems.append(f"{rel}: not in records (stale file?)")
        if not name.endswith(".wav"):
            continue
        count += 1
        with wave.open(path, "rb") as w:
            if w.getframerate() != 44100 or w.getsampwidth() != 2:
                problems.append(f"{rel}: {w.getframerate()} Hz {8 * w.getsampwidth()} bit")
            channels = w.getnchannels()
            x = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").reshape(-1, channels) / 32768.0
        mono = x.mean(axis=1)
        peak = np.max(np.abs(x))
        loop = "loop" in name or "moor_wind" in name
        if peak > 10 ** (-0.95 / 20):
            problems.append(f"{rel}: peak {20 * np.log10(peak):.2f} dBFS")
        if peak < 10 ** (-30 / 20):
            problems.append(f"{rel}: nearly silent")
        if abs(np.mean(mono)) > 0.002:
            problems.append(f"{rel}: DC {np.mean(mono):.4f}")
        if not loop and (np.max(np.abs(x[:2])) > 0.01 or np.max(np.abs(x[-2:])) > 0.01):
            problems.append(f"{rel}: edge not faded ({np.max(np.abs(x[:2])):.3f} / {np.max(np.abs(x[-2:])):.3f})")
        if loop:
            seam = np.max(np.abs(x[0] - x[-1]))
            typical = np.percentile(np.max(np.abs(np.diff(x, axis=0)), axis=1), 99.5)
            print(f"loop {rel}: seam step {seam:.4f}, 99.5th pct step {typical:.4f} -> {'ok' if seam <= typical else 'CHECK'}")
            if seam > typical:
                problems.append(f"{rel}: loop seam step {seam:.4f} > {typical:.4f}")
        if records.get(rel, {}).get("category") == "impact_heavy":
            spectrum = np.abs(np.fft.rfft(mono)) ** 2
            freqs = np.fft.rfftfreq(len(mono), 1 / 44100)
            sub = 10 * np.log10(np.sum(spectrum[(freqs > 30) & (freqs < 80)]) / np.sum(spectrum) + 1e-12)
            if sub < -20:
                problems.append(f"{rel}: heavy hit with little sub-bass ({sub:.1f} dB below total)")
print(f"{count} wav files checked")
print("\n".join(problems) if problems else "no problems")
