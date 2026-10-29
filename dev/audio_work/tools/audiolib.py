"""Audio IO and DSP for the Barrowlight sound library (numpy only).

Readers: WAV (PCM 8/16/24/32, float 32/64, extensible), AIFF (PCM 16/24/32), Ogg Vorbis (through oggdec.exe,
the engine's stb_vorbis, cached as raw float files). Writer: 16-bit PCM WAV with TPDF dither.
"""
import hashlib
import os
import struct
import subprocess
import wave

import numpy as np

RATE = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.dirname(HERE)
CACHE = os.path.join(WORK, "cache")
OGGDEC = os.path.join(HERE, "oggdec.exe")
DOWNLOADS = r"C:\Workspace\github\UnravelEngine\_downloads\survivors"
PEAK_1DB = 10.0 ** (-1.0 / 20.0)


# ============================================================================================ reading

def _cache_path(path):
    digest = hashlib.sha1(os.path.abspath(path).lower().encode("utf-8")).hexdigest()[:16]
    return os.path.join(CACHE, digest + ".raw")


def _read_raw(path):
    with open(path, "rb") as f:
        head = f.read(16)
        if head[:4] != b"RAW1":
            raise ValueError("bad raw " + path)
        channels, rate, frames = struct.unpack("<iii", head[4:16])
        data = np.frombuffer(f.read(), dtype="<f4")
    return data.reshape(-1, channels).astype(np.float64), rate


def predecode(paths):
    """Decodes every Ogg in paths not yet cached, in one oggdec run."""
    todo = [p for p in paths if p.lower().endswith(".ogg") and not os.path.exists(_cache_path(p))]
    if not todo:
        return
    os.makedirs(CACHE, exist_ok=True)
    listing = os.path.join(CACHE, "_list.txt")
    with open(listing, "w", encoding="utf-8", newline="\n") as f:
        for p in todo:
            f.write(p + "|" + _cache_path(p) + "\n")
    subprocess.run([OGGDEC, listing], check=False)


def _read_wav(path):
    with open(path, "rb") as f:
        blob = f.read()
    if blob[:4] != b"RIFF" or blob[8:12] != b"WAVE":
        raise ValueError("not a wav " + path)
    pos = 12
    fmt = None
    data = None
    while pos + 8 <= len(blob):
        cid = blob[pos:pos + 4]
        size = struct.unpack("<I", blob[pos + 4:pos + 8])[0]
        body = blob[pos + 8:pos + 8 + size]
        if cid == b"fmt ":
            fmt = body
        elif cid == b"data":
            data = body
        pos += 8 + size + (size & 1)
    tag, channels, rate = struct.unpack("<HHI", fmt[:8])
    bits = struct.unpack("<H", fmt[14:16])[0]
    if tag == 0xFFFE:
        tag = struct.unpack("<H", fmt[24:26])[0]
    width = bits // 8
    usable = len(data) - len(data) % (width * channels)
    data = data[:usable]
    if tag == 3:
        samples = np.frombuffer(data, dtype="<f4" if bits == 32 else "<f8").astype(np.float64)
    elif bits == 8:
        samples = (np.frombuffer(data, dtype=np.uint8).astype(np.float64) - 128.0) / 128.0
    elif bits == 16:
        samples = np.frombuffer(data, dtype="<i2") / 32768.0
    elif bits == 24:
        raw = np.frombuffer(data, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        value = raw[:, 0] | (raw[:, 1] << 8) | (raw[:, 2] << 16)
        value = np.where(value >= 1 << 23, value - (1 << 24), value)
        samples = value / float(1 << 23)
    elif bits == 32:
        samples = np.frombuffer(data, dtype="<i4") / float(1 << 31)
    else:
        raise ValueError(f"unsupported wav bits {bits} in {path}")
    return samples.reshape(-1, channels), rate


def _extended_to_float(b):
    exponent = ((b[0] & 0x7F) << 8) | b[1]
    mantissa = int.from_bytes(b[2:10], "big")
    sign = -1.0 if b[0] & 0x80 else 1.0
    return sign * mantissa * 2.0 ** (exponent - 16383 - 63)


def _read_aiff(path):
    with open(path, "rb") as f:
        blob = f.read()
    if blob[:4] != b"FORM" or blob[8:12] not in (b"AIFF", b"AIFC"):
        raise ValueError("not aiff " + path)
    pos = 12
    channels = frames = bits = rate = None
    sound = None
    while pos + 8 <= len(blob):
        cid = blob[pos:pos + 4]
        size = struct.unpack(">I", blob[pos + 4:pos + 8])[0]
        body = blob[pos + 8:pos + 8 + size]
        if cid == b"COMM":
            channels, frames, bits = struct.unpack(">hIh", body[:8])
            rate = _extended_to_float(body[8:18])
        elif cid == b"SSND":
            offset = struct.unpack(">I", body[:4])[0]
            sound = body[8 + offset:]
        pos += 8 + size + (size & 1)
    width = bits // 8
    sound = sound[:frames * channels * width]
    if bits == 16:
        samples = np.frombuffer(sound, dtype=">i2") / 32768.0
    elif bits == 24:
        raw = np.frombuffer(sound, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        value = (raw[:, 0] << 16) | (raw[:, 1] << 8) | raw[:, 2]
        value = np.where(value >= 1 << 23, value - (1 << 24), value)
        samples = value / float(1 << 23)
    elif bits == 32:
        samples = np.frombuffer(sound, dtype=">i4") / float(1 << 31)
    else:
        raise ValueError(f"unsupported aiff bits {bits}")
    return samples.reshape(-1, channels), int(round(rate))


def load(path):
    """(samples[frames, channels] float64, rate) of any supported file."""
    lower = path.lower()
    if lower.endswith(".ogg"):
        cached = _cache_path(path)
        if not os.path.exists(cached):
            predecode([path])
        return _read_raw(cached)
    if lower.endswith(".wav"):
        return _read_wav(path)
    if lower.endswith(".aif") or lower.endswith(".aiff"):
        return _read_aiff(path)
    raise ValueError("unsupported " + path)


def resample(x, rate_from, rate_to=RATE):
    """Band-limited resampling through the FFT, zero padded so the ends do not wrap."""
    if rate_from == rate_to:
        return x
    single = x.ndim == 1
    if single:
        x = x[:, None]
    pad = int(0.05 * rate_from)
    padded = np.vstack([np.zeros((pad, x.shape[1])), x, np.zeros((pad, x.shape[1]))])
    n_in = padded.shape[0]
    n_out = int(round(n_in * rate_to / rate_from))
    spectrum = np.fft.rfft(padded, axis=0)
    keep = min(spectrum.shape[0], n_out // 2 + 1)
    out_spectrum = np.zeros((n_out // 2 + 1, x.shape[1]), dtype=complex)
    out_spectrum[:keep] = spectrum[:keep]
    out = np.fft.irfft(out_spectrum, n_out, axis=0) * (n_out / n_in)
    out_pad = int(round(pad * rate_to / rate_from))
    length = int(round(x.shape[0] * rate_to / rate_from))
    out = out[out_pad:out_pad + length]
    return out[:, 0] if single else out


def src(rel, mono=True, start=None, end=None):
    """A download file (path relative to the downloads folder) at 44.1 kHz, mono by default, cut to seconds."""
    data, rate = load(os.path.join(DOWNLOADS, rel))
    if start is not None or end is not None:
        a = int((start or 0.0) * rate)
        b = int(end * rate) if end is not None else data.shape[0]
        data = data[a:b]
    data = resample(data, rate)
    if mono:
        return data.mean(axis=1)
    return data


# ============================================================================================ writing

_DITHER = np.random.default_rng(12345)


def write_wav(path, x, rate=RATE):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.asarray(x, dtype=np.float64)
    channels = 1 if x.ndim == 1 else x.shape[1]
    dither = (_DITHER.uniform(-0.5, 0.5, x.shape) + _DITHER.uniform(-0.5, 0.5, x.shape))
    ints = np.clip(np.round(x * 32767.0 + dither), -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(ints.tobytes())


# ============================================================================================ filters

def _biquad_coefficients(kind, freq, q=0.707, gain_db=0.0, rate=RATE):
    w0 = 2.0 * np.pi * min(freq, rate * 0.45) / rate
    alpha = np.sin(w0) / (2.0 * q)
    cos = np.cos(w0)
    a_gain = 10.0 ** (gain_db / 40.0)
    if kind == "low":
        b = [(1 - cos) / 2, 1 - cos, (1 - cos) / 2]
        a = [1 + alpha, -2 * cos, 1 - alpha]
    elif kind == "high":
        b = [(1 + cos) / 2, -(1 + cos), (1 + cos) / 2]
        a = [1 + alpha, -2 * cos, 1 - alpha]
    elif kind == "band":
        b = [alpha, 0.0, -alpha]
        a = [1 + alpha, -2 * cos, 1 - alpha]
    elif kind == "peak":
        b = [1 + alpha * a_gain, -2 * cos, 1 - alpha * a_gain]
        a = [1 + alpha / a_gain, -2 * cos, 1 - alpha / a_gain]
    elif kind == "lowshelf":
        sq = 2 * np.sqrt(a_gain) * alpha
        b = [a_gain * ((a_gain + 1) - (a_gain - 1) * cos + sq), 2 * a_gain * ((a_gain - 1) - (a_gain + 1) * cos),
             a_gain * ((a_gain + 1) - (a_gain - 1) * cos - sq)]
        a = [(a_gain + 1) + (a_gain - 1) * cos + sq, -2 * ((a_gain - 1) + (a_gain + 1) * cos),
             (a_gain + 1) + (a_gain - 1) * cos - sq]
    elif kind == "highshelf":
        sq = 2 * np.sqrt(a_gain) * alpha
        b = [a_gain * ((a_gain + 1) + (a_gain - 1) * cos + sq), -2 * a_gain * ((a_gain - 1) + (a_gain + 1) * cos),
             a_gain * ((a_gain + 1) + (a_gain - 1) * cos - sq)]
        a = [(a_gain + 1) - (a_gain - 1) * cos + sq, 2 * ((a_gain - 1) - (a_gain + 1) * cos),
             (a_gain + 1) - (a_gain - 1) * cos - sq]
    else:
        raise ValueError(kind)
    return np.array(b) / a[0], np.array(a) / a[0]


def filt(x, kind, freq, q=0.707, gain_db=0.0, order=1):
    """A static RBJ biquad (applied `order` times) through the FFT with enough zero padding for the tail."""
    b, a = _biquad_coefficients(kind, freq, q, gain_db)
    single = x.ndim == 1
    data = x[:, None] if single else x
    tail = int(RATE * min(2.0, max(0.05, 8.0 / max(freq, 5.0))))
    n = data.shape[0] + tail
    size = 1 << int(np.ceil(np.log2(n)))
    w = np.exp(-1j * np.pi * np.arange(size // 2 + 1) / (size // 2))
    h = (b[0] + b[1] * w + b[2] * w * w) / (a[0] + a[1] * w + a[2] * w * w)
    h = h ** order
    out = np.fft.irfft(np.fft.rfft(data, size, axis=0) * h[:, None], size, axis=0)[:data.shape[0]]
    return out[:, 0] if single else out


def band(x, low, high, order=1):
    return filt(filt(x, "high", low, order=order), "low", high, order=order)


def sweep_filter(x, kind, freqs, q=0.707):
    """A biquad whose cutoff moves per sample (freqs: array as long as x)."""
    freqs = np.broadcast_to(np.asarray(freqs, dtype=float), x.shape)
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
    b0, b1, b2 = (b0 / a0).tolist(), (b1 / a0).tolist(), (b2 / a0).tolist()
    a1, a2 = (-2 * cos / a0).tolist(), ((1 - alpha) / a0).tolist()
    out = [0.0] * len(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, x0 in enumerate(x.tolist()):
        y0 = b0[i] * x0 + b1[i] * x1 + b2[i] * x2 - a1[i] * y1 - a2[i] * y2
        out[i] = y0
        x2, x1, y2, y1 = x1, x0, y1, y0
    return np.array(out)


# ============================================================================================ analysis

def k_weight(x):
    """BS.1770 K-weighting approximation: high shelf +4 dB above ~1.7 kHz and a 38 Hz high-pass."""
    return filt(filt(x, "highshelf", 1680.0, q=0.71, gain_db=4.0), "high", 38.0, q=0.5)


def loudness(x, window=0.1):
    """Loudest momentary K-weighted power over `window` seconds, in dB (LUFS-like, mono)."""
    if x.ndim > 1:
        x = x.mean(axis=1)
    k = k_weight(x)
    size = max(1, int(window * RATE))
    if len(k) <= size:
        return 10.0 * np.log10(np.mean(k ** 2) + 1e-12) - 0.691
    running = np.concatenate([[0.0], np.cumsum(k ** 2)])
    power = (running[size:] - running[:-size]) / size
    return 10.0 * np.log10(np.max(power) + 1e-12) - 0.691


def peak_db(x):
    return 20.0 * np.log10(np.max(np.abs(x)) + 1e-12)


def describe(x):
    """Short text: length, peak, momentary loudness, spectral centroid."""
    mono = x.mean(axis=1) if x.ndim > 1 else x
    spectrum = np.abs(np.fft.rfft(mono))
    freqs = np.fft.rfftfreq(len(mono), 1.0 / RATE)
    centroid = float(np.sum(spectrum * freqs) / (np.sum(spectrum) + 1e-12))
    return f"{len(mono) / RATE:5.2f}s pk {peak_db(mono):6.1f} L {loudness(mono):6.1f} c {centroid:6.0f}Hz"
