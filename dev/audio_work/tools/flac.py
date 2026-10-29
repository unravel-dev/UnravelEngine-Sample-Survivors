"""Minimal lossless FLAC encoder (numpy): fixed-blocksize 4096, 16-bit, fixed predictors of order 0-4 per
subframe, best stereo decorrelation per frame (independent / left-side / right-side / mid-side), partitioned
Rice coding (5-bit parameters). Constant subframes for digital silence. No MD5 (zeros = not computed)."""
import struct

import numpy as np

BLOCK = 4096
MAX_PARTITION_ORDER = 6


def _crc_table(poly, width):
    table = []
    top = 1 << (width - 1)
    mask = (1 << width) - 1
    for byte in range(256):
        crc = byte << (width - 8)
        for _ in range(8):
            crc = ((crc << 1) ^ poly) & mask if crc & top else (crc << 1) & mask
        table.append(crc)
    return table


CRC8 = _crc_table(0x07, 8)
CRC16 = _crc_table(0x8005, 16)


def crc8(data):
    crc = 0
    for b in data:
        crc = CRC8[crc ^ b]
    return crc


def crc16(data):
    crc = 0
    table = CRC16
    for b in data:
        crc = ((crc << 8) & 0xFFFF) ^ table[(crc >> 8) ^ b]
    return crc


def bits(value, count):
    """MSB-first bits of an unsigned value."""
    if count == 0:
        return np.zeros(0, dtype=np.uint8)
    shifts = np.arange(count - 1, -1, -1, dtype=np.int64)
    return ((np.int64(value) >> shifts) & 1).astype(np.uint8)


def signed_bits(values, count):
    values = np.asarray(values, dtype=np.int64) & ((1 << count) - 1)
    shifts = np.arange(count - 1, -1, -1, dtype=np.int64)
    return ((values[:, None] >> shifts[None, :]) & 1).astype(np.uint8).reshape(-1)


def utf8_number(n):
    if n < 0x80:
        return bytes([n])
    if n < 0x800:
        return bytes([0xC0 | (n >> 6), 0x80 | (n & 0x3F)])
    if n < 0x10000:
        return bytes([0xE0 | (n >> 12), 0x80 | ((n >> 6) & 0x3F), 0x80 | (n & 0x3F)])
    return bytes([0xF0 | (n >> 18), 0x80 | ((n >> 12) & 0x3F), 0x80 | ((n >> 6) & 0x3F), 0x80 | (n & 0x3F)])


def residual(x, order):
    return np.diff(x, n=order) if order > 0 else x.copy()


def rice_cost(u, k):
    return int(np.sum(u >> k)) + len(u) * (k + 1)


def best_rice(u):
    best = None
    for k in range(0, 31):
        cost = rice_cost(u, k)
        if best is None or cost < best[0]:
            best = (cost, k)
        elif cost > best[0]:
            break
    return best


def plan_residual(e, order, block_size):
    """Partition order and Rice parameters with the fewest bits."""
    u = ((e << 1) ^ (e >> 63)).astype(np.int64)
    best = None
    for p in range(0, MAX_PARTITION_ORDER + 1):
        if block_size % (1 << p) or (block_size >> p) <= order:
            break
        size = block_size >> p
        parts = []
        total = 2 + 4
        start = 0
        for i in range(1 << p):
            count = size - order if i == 0 else size
            cost, k = best_rice(u[start:start + count])
            parts.append((start, count, k))
            total += 5 + cost
            start += count
        if best is None or total < best[0]:
            best = (total, p, parts)
    return best, u


def encode_rice(u, parts, p):
    chunks = [bits(1, 2), bits(p, 4)]
    for start, count, k in parts:
        chunks.append(bits(k, 5))
        seg = u[start:start + count]
        q = seg >> k
        lengths = q + 1 + k
        offsets = np.concatenate([[0], np.cumsum(lengths)[:-1]])
        out = np.zeros(int(np.sum(lengths)), dtype=np.uint8)
        out[offsets + q] = 1
        for b in range(k):
            out[offsets + q + 1 + b] = (seg >> (k - 1 - b)) & 1
        chunks.append(out)
    return np.concatenate(chunks)


LPC_ORDERS = (8, 12)
LPC_PRECISION = 13


def lpc_coefficients(x, order):
    """Quantized LPC predictor (Tukey-windowed autocorrelation + Levinson-Durbin), or None."""
    n = len(x)
    window = np.ones(n)
    taper = n // 4
    ramp = 0.5 - 0.5 * np.cos(np.pi * np.arange(taper) / taper)
    window[:taper] = ramp
    window[n - taper:] = ramp[::-1]
    w = x.astype(np.float64) * window
    r = np.array([np.dot(w[:n - k], w[k:]) for k in range(order + 1)])
    if r[0] <= 0.0:
        return None
    r[0] *= 1.0 + 1e-9
    a = np.zeros(order + 1)
    a[0] = 1.0
    err = r[0]
    for i in range(1, order + 1):
        acc = r[i] + np.dot(a[1:i], r[i - 1:0:-1])
        k = -acc / err
        a[1:i + 1] = a[1:i + 1] + k * np.concatenate([a[i - 1:0:-1], [1.0]])
        err *= 1.0 - k * k
        if err <= 0.0:
            return None
    coefficients = -a[1:]
    top = np.max(np.abs(coefficients))
    if top <= 0.0:
        return None
    shift = LPC_PRECISION - 1 - (int(np.floor(np.log2(top))) + 1)
    shift = max(0, min(15, shift))
    limit = 1 << (LPC_PRECISION - 1)
    qlp = np.clip(np.round(coefficients * (1 << shift)), -limit, limit - 1).astype(np.int64)
    return qlp, shift


def lpc_residual(x, qlp, shift):
    order = len(qlp)
    prediction = np.convolve(x, qlp)[order - 1:len(x) - 1]
    return x[order:] - (prediction >> shift)


def subframe(x, bps):
    """Smallest subframe (constant, fixed order 0-4, LPC order 8/12 or verbatim) for one channel block."""
    n = len(x)
    if np.all(x == x[0]):
        return np.concatenate([bits(0, 1), bits(0, 6), bits(0, 1), signed_bits([x[0]], bps)])
    best = None
    orders = [o for o in range(0, 5) if o < n]
    order = min(orders, key=lambda o: int(np.sum(np.abs(residual(x, o)))) + o * bps)
    e = residual(x, order)
    plan, u = plan_residual(e, order, n)
    if plan is not None:
        best = (8 + order * bps + plan[0], "fixed", order, plan, u, None)
    for lpc_order in LPC_ORDERS:
        if n <= lpc_order * 4:
            continue
        found = lpc_coefficients(x, lpc_order)
        if found is None:
            continue
        qlp, shift = found
        plan, u = plan_residual(lpc_residual(x, qlp, shift), lpc_order, n)
        if plan is None:
            continue
        cost = 8 + lpc_order * bps + 4 + 5 + lpc_order * LPC_PRECISION + plan[0]
        if best is None or cost < best[0]:
            best = (cost, "lpc", lpc_order, plan, u, (qlp, shift))
    verbatim = 8 + n * bps
    if best is None or best[0] >= verbatim:
        return np.concatenate([bits(0, 1), bits(1, 6), bits(0, 1), signed_bits(x, bps)])
    _, kind, order, (total, p, parts), u, lpc = best
    warm = signed_bits(x[:order], bps) if order else np.zeros(0, dtype=np.uint8)
    if kind == "fixed":
        head = np.concatenate([bits(0, 1), bits(8 | order, 6), bits(0, 1)])
        return np.concatenate([head, warm, encode_rice(u, parts, p)])
    qlp, shift = lpc
    head = np.concatenate([bits(0, 1), bits(32 | (order - 1), 6), bits(0, 1)])
    params = np.concatenate([bits(LPC_PRECISION - 1, 4), bits(shift, 5), signed_bits(qlp, LPC_PRECISION)])
    return np.concatenate([head, warm, params, encode_rice(u, parts, p)])


def frame(block, number, rate_code):
    n, channels = block.shape
    if n == BLOCK:
        size_code, size_extra = 12, b""
    elif n <= 256:
        size_code, size_extra = 6, bytes([n - 1])
    else:
        size_code, size_extra = 7, struct.pack(">H", n - 1)
    candidates = []
    if channels == 2:
        left, right = block[:, 0], block[:, 1]
        side = left - right
        mid = (left + right) >> 1
        options = {1: (left, right, 16, 16), 8: (left, side, 16, 17), 9: (side, right, 17, 16), 10: (mid, side, 16, 17)}

        def estimate(sig):
            return min(int(np.sum(np.abs(residual(sig, o)))) for o in range(0, 5))

        scores = {key: estimate(a) + estimate(b) for key, (a, b, _, _) in options.items()}
        assign = min(scores, key=scores.get)
        a, b, bps_a, bps_b = options[assign]
        subs = [subframe(a, bps_a), subframe(b, bps_b)]
        channel_code = assign
    else:
        subs = [subframe(block[:, c], 16) for c in range(channels)]
        channel_code = channels - 1
    header = bytes([0xFF, 0xF8, (size_code << 4) | rate_code, (channel_code << 4) | (0b100 << 1)])
    header += utf8_number(number) + size_extra
    header += bytes([crc8(header)])
    body = np.concatenate(subs)
    pad = (-len(body)) % 8
    body = np.packbits(np.concatenate([body, np.zeros(pad, dtype=np.uint8)])).tobytes()
    data = header + body
    return data + struct.pack(">H", crc16(data))


def encode(path, samples, rate):
    """samples: int16-range integers, shape (frames, channels)."""
    samples = np.asarray(samples, dtype=np.int64)
    if samples.ndim == 1:
        samples = samples[:, None]
    total, channels = samples.shape
    rate_code = {44100: 9, 48000: 10, 32000: 8, 22050: 7}.get(rate, 0)
    if rate_code == 0:
        raise ValueError("unsupported rate")
    info = struct.pack(">HH", BLOCK, BLOCK) + b"\x00\x00\x00" + b"\x00\x00\x00"
    packed = (rate << 44) | ((channels - 1) << 41) | ((16 - 1) << 36) | total
    info += packed.to_bytes(8, "big") + bytes(16)
    out = bytearray(b"fLaC")
    out += bytes([0x80]) + len(info).to_bytes(3, "big") + info
    for number, start in enumerate(range(0, total, BLOCK)):
        out += frame(samples[start:start + BLOCK], number, rate_code)
    with open(path, "wb") as f:
        f.write(bytes(out))
    return len(out)
