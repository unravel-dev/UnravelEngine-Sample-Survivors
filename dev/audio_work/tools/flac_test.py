"""Round-trips samples through flac.encode and the engine's dr_flac (flacdec.exe); exits 1 on any mismatch.
    python flac_test.py            synthetic + 10 s of the battle loop
    python flac_test.py file.flac source.wav    verify an encoded file against its 16-bit WAV source"""
import os
import subprocess
import sys
import time
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402
import flac  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
TMP = os.path.join(os.path.dirname(HERE), "cache")


def decode(path):
    raw = path + ".raw"
    subprocess.run([os.path.join(HERE, "flacdec.exe"), path, raw], check=True)
    with open(raw, "rb") as f:
        head = f.read(16)
        channels, rate, frames = np.frombuffer(head[4:], dtype="<i4")
        data = np.frombuffer(f.read(), dtype="<i2").reshape(-1, channels)
    return data, rate


def round_trip(name, samples, rate=44100):
    path = os.path.join(TMP, name + ".flac")
    start = time.time()
    size = flac.encode(path, samples, rate)
    decoded, got_rate = decode(path)
    ok = got_rate == rate and decoded.shape == samples.shape and np.array_equal(decoded.astype(np.int64), samples.astype(np.int64))
    print(f"{name}: {size} bytes ({size / (samples.size * 2):.1%} of PCM) in {time.time() - start:.1f} s -> {'OK' if ok else 'MISMATCH'}")
    return ok


if len(sys.argv) == 3:
    decoded, rate = decode(sys.argv[1])
    with wave.open(sys.argv[2], "rb") as w:
        src = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").reshape(-1, w.getnchannels())
    ok = decoded.shape == src.shape and np.array_equal(decoded, src)
    print(f"{os.path.basename(sys.argv[1])}: {'OK bit-exact' if ok else 'MISMATCH'} {decoded.shape} vs {src.shape}")
    sys.exit(0 if ok else 1)

rng = np.random.default_rng(1)
n = 44100 * 3 + 1234
t = np.arange(n) / 44100.0
synth = np.zeros((n, 2), dtype=np.int64)
synth[5000:, 0] = np.round(20000 * np.sin(2 * np.pi * 440 * t[5000:]))
synth[5000:, 1] = np.round(rng.normal(0, 3000, n - 5000)).clip(-32768, 32767)
synth[-300:] = 0
synth[100000:100010, 0] = [32767, -32768] * 5
ok = round_trip("synth", synth)
data, rate = al.load(os.path.join(al.DOWNLOADS, "music_battle/determined_pursuit_loop.wav"))
battle = np.round(data[:44100 * 10] * 32768.0).astype(np.int64)
ok = round_trip("battle10", battle) and ok
sys.exit(0 if ok else 1)
