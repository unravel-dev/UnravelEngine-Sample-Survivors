"""Loop checks for the three music tracks: edges, seam jump, silence at the ends, self-similarity."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402

TRACKS = {
    "battle": "music_battle/determined_pursuit_loop.wav",
    "boss": "music_boss/heavy_boss_battle_1_bpm200_0.ogg",
    "menu": "music_menu/qubodup-yd-DarkShrineLoop-OpenGameArt.ogg",
}


def env_db(x, hop):
    frames = len(x) // hop
    blocks = x[:frames * hop].reshape(frames, hop)
    return 20 * np.log10(np.sqrt(np.mean(blocks ** 2, axis=1)) + 1e-9)


for name, rel in TRACKS.items():
    data, rate = al.load(os.path.join(al.DOWNLOADS, rel))
    mono = data.mean(axis=1)
    print(f"== {name}: {data.shape[0] / rate:.3f} s, {data.shape[0]} frames, {rate} Hz, {data.shape[1]} ch, "
          f"peak {al.peak_db(data):.2f} dBFS")
    print("   first samples", np.round(data[:3], 4).tolist(), " last", np.round(data[-3:], 4).tolist())
    # Seam: the jump from the last sample to the first against the typical sample-to-sample step nearby.
    steps = np.abs(np.diff(data, axis=0))
    typical = np.percentile(steps[-rate // 10:], 99, axis=0)
    seam = np.abs(data[0] - data[-1])
    print(f"   seam jump {np.round(seam, 4).tolist()} vs 99th pct step near end {np.round(typical, 4).tolist()}")
    hop = rate // 100
    e = env_db(mono, hop)
    print(f"   level first 0.1 s {np.round(e[:10], 1).tolist()}")
    print(f"   level last 0.1 s  {np.round(e[-10:], 1).tolist()}")
    # Self-similarity of the 50 ms level envelope: candidate repeat periods (seconds).
    hop = rate // 20
    e = env_db(mono, hop)
    e = e - e.mean()
    best = []
    for lag in range(int(8 * 20), len(e) // 2 + 1):
        a, b = e[:-lag], e[lag:]
        corr = float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-9))
        best.append((corr, lag / 20.0))
    best.sort(reverse=True)
    print("   envelope repeat candidates:", [(round(c, 3), s) for c, s in best[:6]])
