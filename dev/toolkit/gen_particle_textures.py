"""Procedural particle textures for the ParticlesSample project.

    python gen_particle_textures.py [out_dir]

Every texture is linear RGBA. The particle shader outputs rgb * a * colour, premultiplied, so RGB holds the
colour (mostly white, the emitter's gradient tints it) and alpha holds the coverage or, for additive effects,
the emission.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                         "particles_src", "Textures")
RNG = np.random.default_rng(7)


def save(name, rgba):
    rgba = np.clip(rgba, 0.0, 1.0)
    Image.fromarray((rgba * 255.0 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))
    print("wrote", name, rgba.shape[1], "x", rgba.shape[0])


def coords(width, height=None):
    """u, v in -1..1 across the image (v grows downwards, like the image rows), and the radius."""
    height = height or width
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    u = (x + 0.5) / width * 2.0 - 1.0
    v = (y + 0.5) / height * 2.0 - 1.0
    return u, v, np.sqrt(u * u + v * v)


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur(image, sigma):
    """Gaussian blur through the FFT (wraps at the borders; every caller keeps a dark margin)."""
    height, width = image.shape
    fy = np.fft.fftfreq(height)[:, None]
    fx = np.fft.fftfreq(width)[None, :]
    kernel = np.exp(-2.0 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(image) * kernel))


def white(alpha, rgb=(1.0, 1.0, 1.0)):
    out = np.zeros(alpha.shape + (4,), np.float32)
    out[..., 0], out[..., 1], out[..., 2] = rgb
    out[..., 3] = alpha
    return out


def glow():
    _, _, r = coords(128)
    alpha = np.exp(-4.5 * r * r) + 0.55 * np.exp(-40.0 * r * r)
    alpha *= 1.0 - smoothstep(0.82, 1.0, r)
    save("glow.png", white(alpha / alpha.max()))


def spark():
    """A streak pointing up: aligned particles turn the texture's top along their motion."""
    u, v, _ = coords(128)
    taper = np.clip(1.0 - u * u, 0.0, 1.0)
    core = np.exp(-(v / (0.05 * taper + 0.012)) ** 2) * taper ** 1.2
    halo = 0.05 * np.exp(-(v / 0.18) ** 2) * taper ** 3
    save("spark.png", np.rot90(white(np.clip(core + halo, 0.0, 1.0))))


def soft():
    """A plain gaussian puff without a core: mist and haze that overlap into a smooth layer."""
    _, _, r = coords(128)
    alpha = np.exp(-3.2 * r * r) * (1.0 - smoothstep(0.75, 1.0, r))
    save("soft.png", white(alpha / alpha.max()))


def ring():
    """A shockwave: a bright rim with a soft wake inside it."""
    _, _, r = coords(256)
    rim = np.exp(-((r - 0.82) / 0.045) ** 2)
    wake = 0.32 * smoothstep(0.25, 0.8, r) ** 2 * (r < 0.82)
    alpha = (rim + wake) * (1.0 - smoothstep(0.93, 1.0, r))
    save("ring.png", white(np.clip(alpha, 0.0, 1.0)))


def shard():
    """An ice shard lying along the horizontal axis, its upper facet lit."""
    u, v, _ = coords(128)
    # Long point to the right, short blunt one to the left.
    reach = np.where(u > 0.0, 0.95, 0.55)
    edge = np.abs(u) / reach + np.abs(v) / 0.17
    body = 1.0 - smoothstep(0.92, 1.0, edge)
    facet = np.where(v < 0.0, 1.0, 0.72)
    rim = np.exp(-((edge - 0.9) / 0.08) ** 2) * 0.35
    halo = 0.22 * np.exp(-(edge / 1.6) ** 2 * 3.0)
    alpha = np.clip(body * 0.92 + halo, 0.0, 1.0)
    shade = np.clip(facet * body + rim + (1.0 - body), 0.0, 1.0)
    rgba = np.zeros((128, 128, 4), np.float32)
    rgba[..., 0] = 0.78 * shade + 0.22
    rgba[..., 1] = 0.92 * shade + 0.08
    rgba[..., 2] = 1.0
    rgba[..., 3] = alpha
    save("shard.png", rgba)


def sparkle():
    """A four-point star with fainter diagonal rays."""
    u, v, r = coords(128)
    au, av = np.abs(u), np.abs(v)
    cross = np.exp(-(v / 0.035) ** 2) * (1.0 - au) ** 3 + np.exp(-(u / 0.035) ** 2) * (1.0 - av) ** 3
    d1, d2 = np.abs(u - v) / math.sqrt(2.0), np.abs(u + v) / math.sqrt(2.0)
    diagonal = 0.35 * (np.exp(-(d1 / 0.03) ** 2) + np.exp(-(d2 / 0.03) ** 2)) * np.clip(1.0 - r * 1.6, 0.0, 1.0) ** 2
    core = np.exp(-r * r / 0.012) + 0.35 * np.exp(-r * r / 0.08)
    alpha = np.clip(cross + diagonal + core, 0.0, 1.0) * (1.0 - smoothstep(0.9, 1.0, r))
    save("sparkle.png", white(alpha))


def rune_circle():
    """A magic circle: rings, a band of procedural glyphs, an inscribed hexagram. Drawn 4x and filtered down."""
    size, scale = 512, 4
    big = size * scale
    image = Image.new("L", (big, big), 0)
    draw = ImageDraw.Draw(image)
    centre = big / 2.0

    def circle(radius, width):
        r = radius * centre
        draw.ellipse([centre - r, centre - r, centre + r, centre + r], outline=255, width=int(width * scale))

    def polar(radius, angle):
        return centre + radius * centre * math.cos(angle), centre + radius * centre * math.sin(angle)

    for radius, width in ((0.95, 3), (0.875, 2), (0.66, 3), (0.6, 1.5), (0.2, 2), (0.14, 1)):
        circle(radius, width)
    # Glyphs: each cell of the outer band gets a few strokes from a small vocabulary.
    cells = 28
    for cell in range(cells):
        a0 = (cell + 0.18) / cells * 2.0 * math.pi
        a1 = (cell + 0.82) / cells * 2.0 * math.pi
        am = (a0 + a1) * 0.5
        r_in, r_out = 0.895, 0.93
        strokes = RNG.integers(2, 4)
        for _ in range(strokes):
            kind = RNG.integers(0, 4)
            if kind == 0:
                draw.line([polar(r_in, am), polar(r_out, am)], fill=255, width=2 * scale)
            elif kind == 1:
                draw.line([polar(r_in, a0), polar(r_out, am), polar(r_in, a1)], fill=255, width=2 * scale)
            elif kind == 2:
                draw.line([polar(r_out, a0), polar(r_in, am), polar(r_out, a1)], fill=255, width=2 * scale)
            else:
                x, y = polar((r_in + r_out) * 0.5, am + (RNG.random() - 0.5) * 0.08)
                dot = 2.2 * scale
                draw.ellipse([x - dot, y - dot, x + dot, y + dot], fill=255)
    # Ticks between the middle rings.
    for tick in range(72):
        angle = tick / 72.0 * 2.0 * math.pi
        length = 0.035 if tick % 6 == 0 else 0.018
        draw.line([polar(0.66, angle), polar(0.66 - length, angle)], fill=255, width=2 * scale)
    # Hexagram inscribed in the inner ring, and dots at its points.
    for offset in (0.0, math.pi / 3.0):
        points = [polar(0.6, offset + k * 2.0 * math.pi / 3.0 - math.pi / 2.0) for k in range(3)]
        draw.line(points + [points[0]], fill=255, width=2 * scale)
    for k in range(6):
        x, y = polar(0.6, k * math.pi / 3.0 - math.pi / 2.0)
        dot = 5 * scale
        draw.ellipse([x - dot, y - dot, x + dot, y + dot], fill=255)
    lines = np.asarray(image.resize((size, size), Image.LANCZOS), np.float32) / 255.0
    glow_alpha = blur(lines, 4.0) * 1.6 + blur(lines, 14.0) * 0.9
    save("rune_circle.png", white(np.clip(lines + glow_alpha, 0.0, 1.0)))
    # The same circle as an emissive map for the arena floor: RGB carries the lines, alpha is opaque.
    sigil = np.clip(lines + blur(lines, 3.0) * 0.6, 0.0, 1.0)
    save("sigil.png", np.stack([sigil, sigil, sigil, np.ones_like(sigil)], axis=-1))


def light_shaft():
    """A vertical beam, brightest at the ground (image bottom) and fading upwards."""
    u, v, _ = coords(128)
    height = (v + 1.0) * 0.5
    beam = np.exp(-(u / 0.38) ** 2) * 0.55 + np.exp(-(u / 0.12) ** 2) * 0.5
    beam *= smoothstep(0.0, 0.75, height) * (1.0 - smoothstep(0.97, 1.0, height) * 0.4)
    beam *= 1.0 - smoothstep(0.85, 1.0, np.abs(u))
    save("light_shaft.png", white(np.clip(beam, 0.0, 1.0)))


def bolt_path(length, rng, roughness, start, end, depth=7):
    """Midpoint displacement between two points; returns the polyline."""
    points = [np.array(start, np.float64), np.array(end, np.float64)]
    offset = roughness * length
    for _ in range(depth):
        refined = [points[0]]
        for a, b in zip(points[:-1], points[1:]):
            mid = (a + b) * 0.5
            direction = b - a
            normal = np.array([-direction[1], direction[0]])
            normal /= max(np.linalg.norm(normal), 1e-6)
            refined += [mid + normal * rng.normal() * offset, b]
        points = refined
        offset *= 0.52
    return points


def bolt_tile(width, height, rng, branches, scale=4, fade_top=True):
    """One lightning tile: a main bolt from the top centre to the bottom centre, with forks."""
    big_w, big_h = width * scale, height * scale
    image = Image.new("L", (big_w, big_h), 0)
    draw = ImageDraw.Draw(image)
    top = (big_w * (0.5 + rng.uniform(-0.05, 0.05)), -4.0 * scale)
    bottom = (big_w * 0.5, big_h - 6.0 * scale)
    main = bolt_path(big_h, rng, 0.07, top, bottom)
    draw.line([tuple(p) for p in main], fill=255, width=int(2.6 * scale), joint="curve")
    for _ in range(branches):
        origin = main[rng.integers(len(main) // 8, len(main) * 3 // 5)]
        reach = big_h * rng.uniform(0.12, 0.3)
        angle = math.radians(rng.uniform(15.0, 35.0)) * rng.choice([-1.0, 1.0])
        tip = origin + np.array([math.sin(angle), math.cos(angle)]) * reach
        fork = bolt_path(reach, rng, 0.14, origin, tip, depth=5)
        draw.line([tuple(p) for p in fork], fill=200, width=int(1.3 * scale), joint="curve")
    core = np.asarray(image.resize((width, height), Image.LANCZOS), np.float32) / 255.0
    halo = blur(np.pad(core, 24), 3.0)[24:-24, 24:-24] * 1.8 + blur(np.pad(core, 48), 10.0)[48:-48, 48:-48] * 1.4
    alpha = np.clip(core + halo, 0.0, 1.0)
    # The halo is a little blue, the core white.
    whiteness = np.clip(core * 1.5, 0.0, 1.0)
    rgba = np.zeros((height, width, 4), np.float32)
    rgba[..., 0] = 0.72 + 0.28 * whiteness
    rgba[..., 1] = 0.82 + 0.18 * whiteness
    rgba[..., 2] = 1.0
    rgba[..., 3] = alpha
    # Fade the top so the bolt dissolves into the sky rather than ending at the quad edge, and the
    # sides so the glow never meets the edge of the quad.
    if fade_top:
        height_factor = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None]
        rgba[..., 3] *= smoothstep(0.0, 0.25, height_factor)
    across = np.abs(np.linspace(-1.0, 1.0, width, dtype=np.float32))[None, :]
    rgba[..., 3] *= 1.0 - smoothstep(0.6, 0.98, across)
    return rgba


def lightning():
    """bolt_4x1: four vertical bolts (128 x 512 tiles). arc_1x4: four horizontal arcs (512 x 128 tiles)."""
    rng = np.random.default_rng(11)
    bolts = [bolt_tile(128, 512, rng, branches=int(rng.integers(2, 5))) for _ in range(4)]
    save("bolt_4x1.png", np.concatenate(bolts, axis=1))
    arcs = []
    for _ in range(4):
        tile = bolt_tile(128, 512, rng, branches=1, fade_top=False)
        # Arcs connect both ends: no top fade, then turn the tile on its side.
        arcs.append(np.rot90(tile, k=1))
    arc_sheet = np.concatenate(arcs, axis=0)
    # Fade both ends a little so neighbouring arcs blend.
    u = np.linspace(-1.0, 1.0, arc_sheet.shape[1], dtype=np.float32)[None, :]
    arc_sheet[..., 3] *= 1.0 - smoothstep(0.85, 1.0, np.abs(u))
    save("arc_1x4.png", arc_sheet)


def value_noise_3d(shape, cells, rng):
    """Tileable-in-time value noise sampled on a (frames, height, width) grid with `cells` lattice cells per axis."""
    frames, height, width = shape
    lattice = rng.random((cells[0] + 1, cells[1] + 1, cells[2] + 1)).astype(np.float32)
    lattice[-1] = lattice[0]
    t = np.linspace(0.0, cells[0], frames, endpoint=False, dtype=np.float32)
    y = np.linspace(0.0, cells[1], height, endpoint=False, dtype=np.float32)
    x = np.linspace(0.0, cells[2], width, endpoint=False, dtype=np.float32)
    ti, yi, xi = t.astype(int), y.astype(int), x.astype(int)
    tf, yf, xf = t - ti, y - yi, x - xi
    tf, yf, xf = [f * f * (3.0 - 2.0 * f) for f in (tf, yf, xf)]
    out = np.zeros(shape, np.float32)
    for dt in (0, 1):
        wt = (tf if dt else 1.0 - tf)[:, None, None]
        for dy in (0, 1):
            wy = (yf if dy else 1.0 - yf)[None, :, None]
            for dx in (0, 1):
                wx = (xf if dx else 1.0 - xf)[None, None, :]
                corner = lattice[np.ix_(ti + dt, yi + dy, xi + dx)]
                out += corner * wt * wy * wx
    return out


def fbm_3d(shape, base_cells, octaves, rng):
    noise = np.zeros(shape, np.float32)
    amplitude, total = 1.0, 0.0
    for octave in range(octaves):
        cells = (base_cells[0] + octave * 2, base_cells[1] * 2 ** octave, base_cells[2] * 2 ** octave)
        noise += amplitude * value_noise_3d(shape, cells, rng)
        total += amplitude
        amplitude *= 0.55
    return noise / total


def explosion():
    """explosion_8x8: a fireball that bursts out, boils and cools away (64 frames of 128 px, plays once)."""
    tile, tiles = 128, 8
    frames = tiles * tiles
    rng = np.random.default_rng(5)
    shape_noise = fbm_3d((frames, tile, tile), (2, 5, 5), 4, rng)
    detail = fbm_3d((frames, tile, tile), (3, 9, 9), 4, rng)
    u, v, r = coords(tile)
    stops = np.array([0.0, 0.15, 0.35, 0.6, 0.85, 1.3])
    colours = np.array([[0.3, 0.05, 0.02], [0.68, 0.13, 0.03], [1.0, 0.36, 0.05],
                        [1.0, 0.62, 0.18], [1.0, 0.88, 0.58], [1.0, 1.0, 0.95]])
    sheet = np.zeros((tile * tiles, tile * tiles, 4), np.float32)
    for frame in range(frames):
        t = frame / (frames - 1)
        radius = 0.3 + 0.55 * (1.0 - (1.0 - t) ** 4)
        boundary = r + (shape_noise[frame] - 0.5) * 1.3 * radius
        density = 1.0 - smoothstep(radius * 0.45, radius * 0.95, boundary)
        # Billows: bright pockets with darker seams between them.
        billow = np.clip((detail[frame] - 0.25) * 1.9, 0.0, 1.0) ** 1.4
        cooling = np.clip(1.25 * (1.0 - t) ** 1.7, 0.0, 1.25)
        core = 1.2 - 0.7 * np.clip(r / radius, 0.0, 1.0)
        heat = density * cooling * (0.25 + 1.05 * billow) * core
        heat += np.exp(-r * r / (0.03 + 0.25 * t)) * max(0.0, 1.0 - t * 5.0) * 1.1
        heat = np.clip(heat, 0.0, 1.3)
        rgb = np.stack([np.interp(heat, stops, colours[:, c]) for c in range(3)], axis=-1)
        emission = np.clip(heat * 1.15, 0.0, 1.0) * (1.0 - smoothstep(0.9, 1.0, r))
        row, col = divmod(frame, tiles)
        cell = sheet[row * tile:(row + 1) * tile, col * tile:(col + 1) * tile]
        cell[..., :3] = rgb / np.maximum(rgb.max(axis=-1, keepdims=True), 1e-4)
        cell[..., 3] = emission
    save("explosion_8x8.png", sheet)


def scorch():
    """A burnt mark with a ragged edge; RGB carries a mottle the emitter colour darkens."""
    rng = np.random.default_rng(3)
    size = 256
    u, v, r = coords(size)
    ragged = value_noise_3d((1, size, size), (1, 10, 10), rng)[0] * 0.7 + value_noise_3d((1, size, size), (1, 28, 28), rng)[0] * 0.3
    edge = r + (ragged - 0.5) * 0.35
    alpha = (1.0 - smoothstep(0.45, 0.82, edge)) * 0.95
    mottle = value_noise_3d((1, size, size), (1, 12, 12), rng)[0] * 0.6 + value_noise_3d((1, size, size), (1, 32, 32), rng)[0] * 0.4
    shade = 0.55 + 0.45 * mottle
    rgba = np.zeros((size, size, 4), np.float32)
    rgba[..., 0] = shade
    rgba[..., 1] = shade
    rgba[..., 2] = shade
    rgba[..., 3] = alpha
    save("scorch.png", rgba)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    glow()
    spark()
    soft()
    ring()
    shard()
    sparkle()
    rune_circle()
    light_shaft()
    lightning()
    explosion()
    scorch()
