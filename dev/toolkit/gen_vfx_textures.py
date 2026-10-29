"""Production particle and environment textures for the ParticlesSample Spell Arena.

    python gen_vfx_textures.py [names...]      (no names = everything; names are the function names below)

Particle textures are linear RGBA. The particle shader outputs rgb * a * colour, premultiplied: RGB holds
the colour, alpha the coverage (normal blend) or the emission (additive blend). Environment albedo maps
are written in sRGB and imported with colorspace srgb.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "particles_src", "Textures2")


# ============================================================================================ helpers

def save(name, rgba):
    rgba = np.clip(rgba, 0.0, 1.0)
    Image.fromarray((rgba * 255.0 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))
    print("wrote", name, rgba.shape[1], "x", rgba.shape[0])


def coords(width, height=None):
    height = height or width
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    u = (x + 0.5) / width * 2.0 - 1.0
    v = (y + 0.5) / height * 2.0 - 1.0
    return u, v, np.sqrt(u * u + v * v)


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur(image, sigma):
    """Gaussian blur through the FFT (wraps; callers keep a dark margin or want tiling)."""
    height, width = image.shape
    fy = np.fft.fftfreq(height)[:, None]
    fx = np.fft.fftfreq(width)[None, :]
    kernel = np.exp(-2.0 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(image) * kernel)).astype(np.float32)


def fbm2(size, base, octaves, rng, persistence=0.5):
    """Tileable 2D fractal value noise in [0, 1]."""
    out = np.zeros((size, size), np.float32)
    amplitude, total = 1.0, 0.0
    for octave in range(octaves):
        cells = base * 2 ** octave
        lattice = rng.random((cells, cells)).astype(np.float32)
        c = np.arange(size, dtype=np.float32) * cells / size
        i0 = c.astype(int) % cells
        i1 = (i0 + 1) % cells
        f = c - np.floor(c)
        f = f * f * (3.0 - 2.0 * f)
        top = lattice[i0][:, i0] * (1 - f)[None, :] + lattice[i0][:, i1] * f[None, :]
        bottom = lattice[i1][:, i0] * (1 - f)[None, :] + lattice[i1][:, i1] * f[None, :]
        out += amplitude * (top * (1 - f)[:, None] + bottom * f[:, None])
        total += amplitude
        amplitude *= persistence
    return out / total


def tile_sheet(tiles, columns):
    rows = (len(tiles) + columns - 1) // columns
    h, w = tiles[0].shape[:2]
    sheet = np.zeros((h * rows, w * columns, 4), np.float32)
    for index, tile in enumerate(tiles):
        r, c = divmod(index, columns)
        sheet[r * h:(r + 1) * h, c * w:(c + 1) * w] = tile
    return sheet


def rgba_from(alpha, rgb):
    out = np.zeros(alpha.shape + (4,), np.float32)
    out[..., 0], out[..., 1], out[..., 2] = rgb
    out[..., 3] = alpha
    return out


def strokes_to_glow(lines, core_gain=1.3, glow_sigmas=((2.0, 1.2), (7.0, 0.7)), pad=32):
    """Anti-aliased line art to an emission alpha: a bright core plus soft halos."""
    padded = np.pad(lines, pad)
    halo = sum(blur(padded, sigma) * gain for sigma, gain in glow_sigmas)[pad:-pad, pad:-pad]
    return np.clip(lines * core_gain + halo, 0.0, 1.0)


def supersampled(width, height, scale, draw_function):
    """Draws with PIL at `scale` x resolution and filters down: crisp anti-aliased strokes and polygons."""
    image = Image.new("L", (width * scale, height * scale), 0)
    draw_function(ImageDraw.Draw(image), scale)
    return np.asarray(image.resize((width, height), Image.LANCZOS), np.float32) / 255.0


# ============================================================================================ billowy volumes

class noise3:
    """Periodic 3D value noise on a lattice, vectorised over arrays of points."""

    def __init__(self, rng, size=32):
        self.size = size
        self.lattice = rng.random(size ** 3).astype(np.float32)

    def __call__(self, x, y, z):
        s = self.size
        xi, yi, zi = np.floor(x), np.floor(y), np.floor(z)
        fx, fy, fz = x - xi, y - yi, z - zi
        fx, fy, fz = [f * f * (3.0 - 2.0 * f) for f in (fx, fy, fz)]
        xi = xi.astype(np.int64) % s
        yi = yi.astype(np.int64) % s
        zi = zi.astype(np.int64) % s
        x1, y1, z1 = (xi + 1) % s, (yi + 1) % s, (zi + 1) % s
        lat = self.lattice

        def at(a, b, c):
            return lat[(a * s + b) * s + c]

        c00 = at(xi, yi, zi) * (1 - fx) + at(x1, yi, zi) * fx
        c10 = at(xi, y1, zi) * (1 - fx) + at(x1, y1, zi) * fx
        c01 = at(xi, yi, z1) * (1 - fx) + at(x1, yi, z1) * fx
        c11 = at(xi, y1, z1) * (1 - fx) + at(x1, y1, z1) * fx
        return (c00 * (1 - fy) + c10 * fy) * (1 - fz) + (c01 * (1 - fy) + c11 * fy) * fz


def billow(noise, p, octaves, frequency):
    """Cauliflower noise: absolute-valued octaves, round lumps with creases between them, in [0, 1]."""
    value = np.zeros(p[0].shape, np.float32)
    amplitude, total = 1.0, 0.0
    for octave in range(octaves):
        f = frequency * 2.0 ** octave
        n = noise(p[0] * f + 17.0 * octave, p[1] * f + 31.0 * octave, p[2] * f + 7.0 * octave)
        value += amplitude * (1.0 - np.abs(2.0 * n - 1.0))
        total += amplitude
        amplitude *= 0.5
    return value / total


def billow_ball(size, radius, displacement, noise, offset, octaves=5, frequency=2.2, erosion=0.0,
                edge_softness=0.025, normal_softness=0.0):
    """A noise-displaced sphere seen from the front: coverage, surface normal, surface noise and the
    depth through the ball (0..1), on a size x size grid spanning [-1, 1]."""
    u, v, r = coords(size)
    r2 = u * u + v * v
    z = np.sqrt(np.maximum(radius * radius - r2, 0.0))
    n = np.zeros_like(u)
    surface = np.full_like(u, radius)
    for _ in range(3):
        length = np.sqrt(r2 + z * z) + 1e-5
        direction = (u / length + offset[0], v / length + offset[1], z / length + offset[2])
        n = billow(noise, direction, octaves, frequency)
        surface = radius * (1.0 + displacement * (n - 0.55))
        z = np.sqrt(np.maximum(surface * surface - r2, 0.0))
    edge = surface - np.sqrt(r2)
    coverage = smoothstep(0.0, edge_softness, edge) * smoothstep(erosion, erosion + 0.12, n)
    shaped = blur(np.pad(z, 16), normal_softness * size)[16:-16, 16:-16] if normal_softness > 0.0 else z
    gy, gx = np.gradient(shaped * size * 0.5)
    normal = np.stack([-gx, gy, np.ones_like(z)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    thickness = np.clip(z / max(radius, 1e-4), 0.0, 1.0)
    return coverage, normal, n, thickness


FIRE_RAMP = [
    (0.00, (0.0, 0.0, 0.0)),
    (0.15, (0.25, 0.03, 0.0)),
    (0.30, (0.65, 0.1, 0.01)),
    (0.45, (0.95, 0.28, 0.03)),
    (0.65, (1.0, 0.5, 0.1)),
    (0.85, (1.0, 0.72, 0.28)),
    (1.10, (1.0, 0.9, 0.62)),
    (1.50, (1.0, 1.0, 0.92)),
]

LIGHT = np.array([-0.45, 0.65, 0.6], np.float32) / np.linalg.norm([-0.45, 0.65, 0.6])


def fire_emission(temperature):
    stops = np.array([s[0] for s in FIRE_RAMP], np.float32)
    colours = np.array([s[1] for s in FIRE_RAMP], np.float32)
    t = np.clip(temperature, 0.0, stops[-1])
    return np.stack([np.interp(t, stops, colours[:, c]) for c in range(3)], axis=-1)


def density_sprite(noise, size, radius, time, offset, frequency=2.4, octaves=5, roughness=1.1, erosion=0.0):
    """A soft cloud: cauliflower noise inside a radial falloff, the noise breaking the silhouette.
    Returns the density (0..1), the noise and the radial distance in units of the radius."""
    u, v, r = coords(size)
    warp_u = noise(u * 1.3 + offset[0] + 3.1, v * 1.3 + offset[1], time * 0.5 + 9.0) - 0.5
    warp_v = noise(u * 1.3 + offset[0], v * 1.3 + offset[1] + 5.7, time * 0.5 + 4.0) - 0.5
    n = billow(noise, (u + warp_u * 0.35 + offset[0], v + warp_v * 0.35 + offset[1], np.full_like(u, time + offset[2])),
               octaves, frequency)
    rr = r / radius
    shape = 1.0 - rr * rr + (n - 0.55) * roughness
    density = smoothstep(0.0, 0.55, shape) * (0.45 + 0.55 * n)
    if erosion > 0.0:
        density *= smoothstep(erosion, erosion + 0.25, n)
    # The finest octaves leave hair-thin ridges on the silhouette; a light blur keeps the edge soft.
    density = blur(np.pad(density, 8), 1.6)[8:-8, 8:-8]
    return np.clip(density, 0.0, 1.0), n, rr


def light_density(density, light=(-0.55, -0.8), steps=24, reach=0.18, absorption=3.0, ambient=0.28):
    """Self-shadowed lighting of a density sprite: transmittance towards a light up and to the left
    (image space), plus a soft normal term from the density's gradient and an ambient floor."""
    size = density.shape[0]
    shadow = np.zeros_like(density)
    for k in range(1, steps + 1):
        dx = int(round(-light[0] * k * reach * size / steps))
        dy = int(round(-light[1] * k * reach * size / steps))
        shadow += np.roll(np.roll(density, dy, axis=0), dx, axis=1)
    transmittance = np.exp(-absorption * shadow / steps)
    height = blur(np.pad(density, 16), 3.0)[16:-16, 16:-16]
    gy, gx = np.gradient(height * size * 0.06)
    normal = np.stack([-gx, -gy, np.ones_like(height)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    l = np.array([light[0], light[1], 0.7], np.float32)
    l /= np.linalg.norm(l)
    wrap = np.clip((normal @ l + 0.5) / 1.5, 0.0, 1.0)
    return ambient + (1.0 - ambient) * transmittance * wrap


def explosion():
    """explosion_fire_8x8 (additive emission) and explosion_smoke_8x8 (normal blend) of one fireball:
    it bursts out white-hot, rolls while the fire sinks into its creases, then cools to drifting smoke."""
    size, tiles = 256, 8
    frames = tiles * tiles
    noise = noise3(np.random.default_rng(5))
    fire_tiles, smoke_tiles = [], []
    for frame in range(frames):
        t = frame / (frames - 1)
        radius = 0.34 + 0.52 * (1.0 - (1.0 - t) ** 3)
        erosion = smoothstep(0.55, 1.0, t) * 0.5
        density, n, rr = density_sprite(noise, size, radius, time=t * 1.6, offset=(0.0, 0.0, 0.0),
                                        frequency=2.2, roughness=1.0, erosion=erosion)
        heat = 1.25 * (1.0 - t) ** 1.4
        core = np.clip(1.0 - rr, 0.0, 1.0)
        temperature = heat * (0.25 + 1.05 * (1.0 - n)) * (0.3 + 0.9 * core) * smoothstep(0.05, 0.5, density) \
            + 0.3 * max(0.0, 1.0 - t * 6.0) * core
        emission = fire_emission(temperature)
        alpha = emission.max(axis=-1, keepdims=True)
        hue = np.where(alpha > 1e-4, emission / np.maximum(alpha, 1e-4), 0.0)
        fire_tiles.append(np.concatenate([hue, alpha], -1))
        lit = light_density(density)
        shade = lit * (0.42 + 0.58 * smoothstep(0.0, 0.7, t))
        cover = density * smoothstep(0.05, 0.45, t) * (1.0 - 0.6 * smoothstep(0.7, 1.0, t))
        smoke_tiles.append(np.stack([shade, shade * 0.96, shade * 0.92, np.clip(cover * 1.1, 0.0, 1.0)], axis=-1))
    save("explosion_fire_8x8.png", tile_sheet(fire_tiles, tiles))
    save("explosion_smoke_8x8.png", tile_sheet(smoke_tiles, tiles))


def smoke():
    """smoke_puffs_4x4: sixteen soft billowing puffs, self-shadowed from above, thinner towards the rim."""
    size = 256
    rng = np.random.default_rng(31)
    noise = noise3(rng)
    tiles = []
    for _ in range(16):
        offset = tuple(rng.uniform(-6.0, 6.0, 3))
        density, n, rr = density_sprite(noise, size, rng.uniform(0.62, 0.74), time=0.0, offset=offset,
                                        frequency=rng.uniform(1.6, 2.2), octaves=4, roughness=rng.uniform(0.9, 1.2),
                                        erosion=rng.uniform(0.05, 0.15))
        shade = light_density(density, absorption=4.0, ambient=0.2)
        tiles.append(np.stack([shade, shade * 0.98, shade * 0.95, np.clip(density * 0.95, 0.0, 1.0)], axis=-1))
    save("smoke_puffs_4x4.png", tile_sheet(tiles, 4))


# ============================================================================================ lightning

def midpoint_path(start, end, rng, roughness, depth):
    points = [np.array(start, np.float64), np.array(end, np.float64)]
    offset = roughness * np.linalg.norm(np.array(end) - np.array(start))
    for _ in range(depth):
        refined = [points[0]]
        for a, b in zip(points[:-1], points[1:]):
            mid = (a + b) * 0.5
            d = b - a
            normal = np.array([-d[1], d[0]]) / max(np.linalg.norm(d), 1e-6)
            refined += [mid + normal * rng.normal() * offset, b]
        points = refined
        offset *= 0.55
    return points


def draw_bolt(width, height, rng, branches, scale=4, sideways=0.04):
    """A crisp bolt from the top centre to the bottom centre: a white core, a thin bluish halo, thinner
    and dimmer forks. Bloom on the bright core does the rest in the engine."""
    big_w, big_h = width * scale, height * scale
    core = Image.new("L", (big_w, big_h), 0)
    draw = ImageDraw.Draw(core)
    top = (big_w * (0.5 + rng.uniform(-sideways, sideways)), -2.0 * scale)
    bottom = (big_w * 0.5, big_h - 3.0 * scale)
    main = midpoint_path(top, bottom, rng, 0.06, 9)
    draw.line([tuple(p) for p in main], fill=255, width=int(2.2 * scale), joint="curve")
    for _ in range(branches):
        origin = main[rng.integers(len(main) // 10, len(main) * 7 // 10)]
        reach = big_h * rng.uniform(0.08, 0.22)
        angle = math.radians(rng.uniform(12.0, 40.0)) * rng.choice([-1.0, 1.0])
        tip = origin + np.array([math.sin(angle), math.cos(angle)]) * reach
        fork = midpoint_path(origin, tip, rng, 0.09, 7)
        draw.line([tuple(p) for p in fork], fill=150, width=int(1.2 * scale), joint="curve")
        if rng.random() < 0.5:
            sub_origin = fork[len(fork) // 2]
            sub_tip = sub_origin + np.array([math.sin(angle * 1.4), math.cos(angle * 1.4)]) * reach * 0.45
            draw.line([tuple(p) for p in midpoint_path(sub_origin, sub_tip, rng, 0.1, 6)], fill=90,
                      width=int(0.9 * scale), joint="curve")
    lines = np.asarray(core.resize((width, height), Image.LANCZOS), np.float32) / 255.0
    alpha = strokes_to_glow(lines, 1.4, ((1.5, 0.95), (5.0, 0.7)))
    white = np.clip(lines * 1.6, 0.0, 1.0)
    rgba = np.zeros((height, width, 4), np.float32)
    rgba[..., 0] = 0.62 + 0.38 * white
    rgba[..., 1] = 0.74 + 0.26 * white
    rgba[..., 2] = 1.0
    rgba[..., 3] = alpha
    return rgba


def lightning():
    """bolt_8x1: eight sky-to-ground bolts (256 x 1024 tiles); arc_8x1: eight short arcs pointing up
    (128 x 1024 columns), for particles aligned to their motion."""
    rng = np.random.default_rng(23)
    bolts = []
    for _ in range(8):
        tile = draw_bolt(256, 1024, rng, branches=int(rng.integers(3, 7)))
        fade_top = smoothstep(0.0, 0.18, np.linspace(0.0, 1.0, 1024, dtype=np.float32))[:, None]
        across = 1.0 - smoothstep(0.7, 0.98, np.abs(np.linspace(-1.0, 1.0, 256, dtype=np.float32)))[None, :]
        tile[..., 3] *= fade_top * across
        bolts.append(tile)
    save("bolt_8x1.png", tile_sheet(bolts, 8))
    arcs = []
    for _ in range(8):
        tile = np.rot90(draw_bolt(128, 1024, rng, branches=int(rng.integers(1, 3)), sideways=0.0), k=1).copy()
        ends = 1.0 - smoothstep(0.88, 1.0, np.abs(np.linspace(-1.0, 1.0, tile.shape[1], dtype=np.float32)))[None, :]
        rows = 1.0 - smoothstep(0.7, 0.98, np.abs(np.linspace(-1.0, 1.0, tile.shape[0], dtype=np.float32)))[:, None]
        tile[..., 3] *= ends * rows
        arcs.append(tile)
    save("arc_8x1.png", np.rot90(tile_sheet(arcs, 1)))


# ============================================================================================ ice

def facet_polygon_tile(width, height, polygons, scale=4):
    """Renders shaded polygons (points, brightness) and their outlines: a faceted crystal."""
    shade = Image.new("L", (width * scale, height * scale), 0)
    mask = Image.new("L", (width * scale, height * scale), 0)
    edges = Image.new("L", (width * scale, height * scale), 0)
    ds, dm, de = ImageDraw.Draw(shade), ImageDraw.Draw(mask), ImageDraw.Draw(edges)
    for points, brightness in polygons:
        pts = [(x * width * scale, y * height * scale) for x, y in points]
        ds.polygon(pts, fill=int(255 * brightness))
        dm.polygon(pts, fill=255)
        de.line(pts + [pts[0]], fill=255, width=max(1, int(1.2 * scale)))
    down = lambda im: np.asarray(im.resize((width, height), Image.LANCZOS), np.float32) / 255.0
    return down(shade), down(mask), down(edges)


def ice():
    """ice_spike_4x1 (256 x 512 tiles, base at the bottom), crystal_shard_2x2 (long axis vertical),
    frost_decal, snowflake_2x2."""
    rng = np.random.default_rng(41)
    spikes = []
    for _ in range(4):
        polygons = []
        for k in range(3):
            # One big spike and two smaller ones leaning out of its base.
            if k == 0:
                cx, half, tip_x, tip_y = 0.5, rng.uniform(0.16, 0.2), 0.5 + rng.uniform(-0.05, 0.05), 0.04
            else:
                side = -1 if k == 1 else 1
                cx, half = 0.5 + side * rng.uniform(0.17, 0.24), rng.uniform(0.08, 0.11)
                tip_x, tip_y = cx + side * rng.uniform(0.1, 0.2), rng.uniform(0.35, 0.55)
            left, right = cx - half, cx + half
            ridge = cx + rng.uniform(-0.25, 0.25) * half
            inner_left = (left + ridge) * 0.5
            inner_right = (ridge + right) * 0.5
            polygons.append(([(left, 1.0), (inner_left, 1.0), (tip_x, tip_y)], 0.35))
            polygons.append(([(inner_left, 1.0), (ridge, 1.0), (tip_x, tip_y)], 0.6))
            polygons.append(([(ridge, 1.0), (inner_right, 1.0), (tip_x, tip_y)], 1.0))
            polygons.append(([(inner_right, 1.0), (right, 1.0), (tip_x, tip_y)], 0.75))
        shade, mask, edges = facet_polygon_tile(256, 512, polygons)
        u, v, _ = coords(256, 512)
        height_factor = (v + 1.0) * 0.5
        sparkle = blur(rng.random((512, 256)).astype(np.float32), 1.0)
        tip_light = 1.0 - height_factor * 0.45
        tone = np.clip(shade * tip_light * (0.8 + 0.3 * sparkle) + edges * 0.8, 0.0, 1.0)
        glow = np.clip(blur(np.pad(mask, 24), 6.0)[24:-24, 24:-24] * 0.4, 0.0, 1.0)
        # Clearer in the middle of each facet, denser at the edges and towards the frozen base.
        alpha = np.clip(mask * (0.5 + 0.35 * height_factor) + edges * 0.45 + glow * (1.0 - mask), 0.0, 1.0)
        rgba = np.zeros((512, 256, 4), np.float32)
        rgba[..., 0] = 0.55 + 0.45 * tone
        rgba[..., 1] = 0.78 + 0.22 * tone
        rgba[..., 2] = 1.0
        rgba[..., 3] = alpha
        spikes.append(rgba)
    save("ice_spike_4x1.png", tile_sheet(spikes, 4))

    shards = []
    for _ in range(4):
        tip_r, tip_l = rng.uniform(0.92, 0.98), rng.uniform(0.15, 0.35)
        top, bottom = rng.uniform(0.32, 0.4), rng.uniform(0.6, 0.68)
        ridge_y = 0.5 + rng.uniform(-0.05, 0.05)
        polygons = [([(tip_l, 0.5), (0.45, top), (tip_r, 0.5)], 1.0),
                    ([(tip_l, 0.5), (tip_r, 0.5), (0.5, bottom)], 0.6),
                    ([(0.45, top), (0.7, ridge_y - 0.04), (tip_r, 0.5)], 0.8)]
        shade, mask, edges = facet_polygon_tile(256, 256, polygons)
        glow = np.clip(blur(np.pad(mask, 24), 5.0)[24:-24, 24:-24] * 0.4, 0.0, 1.0)
        tone = np.clip(shade + edges * 0.6, 0.0, 1.0)
        rgba = np.zeros((256, 256, 4), np.float32)
        rgba[..., 0] = 0.6 + 0.4 * tone
        rgba[..., 1] = 0.82 + 0.18 * tone
        rgba[..., 2] = 1.0
        rgba[..., 3] = np.clip(mask * 0.9 + edges * 0.3 + glow * (1.0 - mask), 0.0, 1.0)
        shards.append(rgba)
    save("crystal_shard_2x2.png", np.rot90(tile_sheet(shards, 2)))

    size = 1024

    def frost(draw, scale):
        centre = size * scale / 2.0
        for arm in range(26):
            angle = arm / 26 * 2 * math.pi + rng.uniform(-0.1, 0.1)
            length = size * scale * rng.uniform(0.3, 0.47)
            points = [(centre, centre)]
            x, y = centre, centre
            steps = 18
            for s in range(steps):
                angle += rng.uniform(-0.18, 0.18)
                x += math.cos(angle) * length / steps
                y += math.sin(angle) * length / steps
                points.append((x, y))
                if s > 2 and rng.random() < 0.55:
                    # Feathered side branches, as frost grows.
                    branch_angle = angle + rng.choice([-1, 1]) * rng.uniform(0.6, 1.1)
                    branch_length = length / steps * rng.uniform(1.0, 3.0) * (1.0 - s / steps)
                    draw.line([(x, y), (x + math.cos(branch_angle) * branch_length,
                                        y + math.sin(branch_angle) * branch_length)], fill=170, width=int(1.2 * scale))
            width_px = max(1, int(2.4 * scale))
            draw.line(points, fill=255, width=width_px, joint="curve")

    lines = supersampled(size, size, 2, frost)
    u, v, r = coords(size)
    haze = fbm2(size, 6, 5, rng)
    fill = np.clip((1.0 - smoothstep(0.25, 0.92, r + (haze - 0.5) * 0.35)) * 0.45, 0.0, 1.0)
    alpha = np.clip(lines * 0.9 + blur(np.pad(lines, 16), 2.0)[16:-16, 16:-16] * 0.5 + fill * (0.4 + 0.6 * haze), 0.0, 1.0)
    alpha *= 1.0 - smoothstep(0.85, 1.0, r)
    save("frost_decal.png", rgba_from(alpha, (0.85, 0.94, 1.0)))

    flakes = []
    for _ in range(4):
        arms = 6
        branch_at = rng.uniform(0.3, 0.6, 2)
        branch_len = rng.uniform(0.15, 0.3, 2)

        def flake(draw, scale):
            c = 128 * scale / 2.0
            radius = c * 0.85
            for a in range(arms):
                angle = a / arms * 2 * math.pi
                dx, dy = math.cos(angle), math.sin(angle)
                draw.line([(c, c), (c + dx * radius, c + dy * radius)], fill=255, width=int(1.6 * scale))
                for at, length in zip(branch_at, branch_len):
                    bx, by = c + dx * radius * at, c + dy * radius * at
                    for side in (-1, 1):
                        ba = angle + side * math.radians(55)
                        draw.line([(bx, by), (bx + math.cos(ba) * radius * length, by + math.sin(ba) * radius * length)],
                                  fill=220, width=int(1.2 * scale))

        lines = supersampled(128, 128, 4, flake)
        flakes.append(rgba_from(strokes_to_glow(lines, 1.2, ((1.5, 0.8), (4.0, 0.4))), (0.9, 0.97, 1.0)))
    save("snowflake_2x2.png", tile_sheet(flakes, 2))


# ============================================================================================ arcane

def arcane():
    """wisp_2x2 (curved energy strokes), glyphs_4x4 (rune glyphs), ring_energy (a shockwave rim)."""
    rng = np.random.default_rng(53)
    wisps = []
    for index in range(4):
        sweep = rng.uniform(70, 150)
        radius = rng.uniform(0.32, 0.42)

        def wisp(draw, scale):
            c = 256 * scale / 2.0
            start = rng.uniform(0, 360)
            steps = 40
            for s in range(steps):
                a0 = math.radians(start + sweep * s / steps)
                a1 = math.radians(start + sweep * (s + 1) / steps)
                t = s / steps
                thickness = max(1, int((0.5 + 5.5 * math.sin(math.pi * t) ** 1.5) * scale))
                draw.line([(c + math.cos(a0) * radius * 2 * c, c + math.sin(a0) * radius * 2 * c),
                           (c + math.cos(a1) * radius * 2 * c, c + math.sin(a1) * radius * 2 * c)],
                          fill=int(255 * (0.4 + 0.6 * math.sin(math.pi * t))), width=thickness)

        lines = supersampled(256, 256, 4, wisp)
        wisps.append(rgba_from(strokes_to_glow(lines, 1.0, ((2.5, 0.9), (9.0, 0.6))), (1.0, 1.0, 1.0)))
    save("wisp_2x2.png", tile_sheet(wisps, 2))

    glyphs = []
    for _ in range(16):
        def glyph(draw, scale):
            s = 128 * scale
            m = s * 0.22

            def P(x, y):
                return (m + x * (s - 2 * m), m + y * (s - 2 * m))

            width = int(5 * scale)
            draw.line([P(0.5, 0.0), P(0.5, 1.0)], fill=255, width=width)
            for _ in range(rng.integers(2, 4)):
                kind = rng.integers(0, 5)
                y0 = rng.uniform(0.1, 0.9)
                if kind == 0:
                    draw.line([P(0.5, y0), P(rng.choice([0.0, 1.0]), y0 + rng.uniform(-0.3, 0.3))], fill=255, width=width)
                elif kind == 1:
                    draw.line([P(0.15, y0), P(0.85, y0)], fill=255, width=width)
                elif kind == 2:
                    r = rng.uniform(0.12, 0.2)
                    cx = rng.choice([0.3, 0.7])
                    a, b = P(cx - r, y0 - r), P(cx + r, y0 + r)
                    draw.ellipse([a, b], outline=255, width=width)
                elif kind == 3:
                    draw.line([P(0.5, y0), P(0.15, y0 + 0.2), P(0.5, y0 + 0.4)], fill=255, width=width)
                else:
                    draw.line([P(0.5, y0), P(0.85, y0 - 0.2), P(0.85, y0 + 0.15)], fill=255, width=width)

        lines = supersampled(128, 128, 4, glyph)
        glyphs.append(rgba_from(strokes_to_glow(lines, 1.2, ((2.0, 0.9), (6.0, 0.5))), (1.0, 1.0, 1.0)))
    save("glyphs_4x4.png", tile_sheet(glyphs, 4))

    size = 512
    u, v, r = coords(size)
    angle = np.arctan2(v, u)
    breakup = 0.55 + 0.45 * np.cos(angle * 7.0 + 1.3) * np.cos(angle * 3.0 + 0.4)
    breakup = np.clip(breakup + (fbm2(size, 8, 4, rng) - 0.5) * 0.6, 0.0, 1.0)
    rim = np.exp(-((r - 0.84) / 0.03) ** 2)
    inner = smoothstep(0.35, 0.84, r) ** 3 * (r < 0.84) * 0.45
    alpha = np.clip((rim * (0.4 + 0.6 * breakup) + inner * (0.5 + 0.5 * breakup)), 0.0, 1.0) * (1.0 - smoothstep(0.93, 1.0, r))
    save("ring_energy.png", rgba_from(alpha, (1.0, 1.0, 1.0)))


# ============================================================================================ holy

def holy():
    """beam_4x1 (128 x 512 vertical beams with streaks), heal_plus (a soft plus sign)."""
    rng = np.random.default_rng(67)
    beams = []
    for _ in range(4):
        u, v, _ = coords(128, 512)
        height_factor = (v + 1.0) * 0.5
        streak_noise = np.repeat(rng.random((1, 128)).astype(np.float32), 512, axis=0)
        streaks = blur(streak_noise, 1.5)
        streaks = (streaks - streaks.min()) / (streaks.max() - streaks.min())
        flow = fbm2(512, 4, 3, rng)[:, :128]
        core = np.exp(-(u / 0.12) ** 2) * 0.9 + np.exp(-(u / 0.4) ** 2) * 0.45
        alpha = core * (0.55 + 0.65 * streaks) * (0.7 + 0.5 * flow)
        alpha *= smoothstep(0.0, 0.55, height_factor) * (1.0 - smoothstep(0.96, 1.0, height_factor) * 0.5)
        alpha *= 1.0 - smoothstep(0.85, 1.0, np.abs(u))
        beams.append(rgba_from(np.clip(alpha, 0.0, 1.0), (1.0, 1.0, 1.0)))
    save("beam_4x1.png", tile_sheet(beams, 4))

    u, v, r = coords(128)
    bar = lambda a, b: np.exp(-(a / 0.13) ** 4) * (1.0 - smoothstep(0.55, 0.7, np.abs(b)))
    plus = np.maximum(bar(u, v), bar(v, u))
    alpha = np.clip(plus + blur(np.pad(plus, 16), 6.0)[16:-16, 16:-16] * 0.8, 0.0, 1.0)
    save("heal_plus.png", rgba_from(alpha, (1.0, 1.0, 1.0)))


# ============================================================================================ fire leftovers

def ground():
    """dust_ring (a soft ring of thrown-up dust), scorch_embers (glowing cracks for an additive decal)."""
    rng = np.random.default_rng(79)
    size = 512
    u, v, r = coords(size)
    lumps = fbm2(size, 6, 5, rng)
    band = np.exp(-((r - 0.62) / 0.22) ** 2) * (0.55 + 0.6 * lumps)
    alpha = np.clip(band * (1.0 - smoothstep(0.88, 1.0, r)), 0.0, 1.0)
    shade = 0.75 + 0.25 * lumps
    rgba = np.stack([shade, shade * 0.95, shade * 0.88, alpha], axis=-1)
    save("dust_ring.png", rgba)

    cells = fbm2(size, 12, 3, rng)
    cracks = 1.0 - np.abs(2.0 * fbm2(size, 10, 4, rng) - 1.0)
    cracks = smoothstep(0.82, 0.97, cracks)
    heat = (1.0 - smoothstep(0.1, 0.75, r)) * (0.6 + 0.4 * cells)
    alpha = np.clip(cracks * heat * 1.3 + (1.0 - smoothstep(0.0, 0.45, r)) * 0.25 * cells, 0.0, 1.0)
    save("scorch_embers.png", rgba_from(alpha, (1.0, 1.0, 1.0)))


# ============================================================================================ environment

def environment():
    """Flagstones for the arena floor and stone blocks for the pillars: albedo (sRGB), normal, roughness."""
    rng = np.random.default_rng(97)
    size = 1024
    count = 22
    points = rng.random((count, 2)).astype(np.float32)
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    nearest = np.full((size, size), 9.0, np.float32)
    second = np.full((size, size), 9.0, np.float32)
    owner = np.zeros((size, size), np.int32)
    for index, (px, py) in enumerate(points):
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                d = np.sqrt((x - px - ox) ** 2 + (y - py - oy) ** 2)
                closer = d < nearest
                second = np.where(closer, nearest, np.minimum(second, d))
                owner = np.where(closer, index, owner)
                nearest = np.where(closer, d, nearest)
    edge = second - nearest
    mortar = 1.0 - smoothstep(0.004, 0.012, edge)
    stone_tone = rng.uniform(0.75, 1.1, count).astype(np.float32)[owner]
    grain = fbm2(size, 16, 5, rng)
    blotch = fbm2(size, 4, 4, rng)
    height = smoothstep(0.0, 0.08, edge) * 0.8 + grain * 0.25 + blotch * 0.1
    albedo_value = (0.34 * stone_tone * (0.75 + 0.35 * grain) * (0.85 + 0.3 * blotch)) * (1.0 - mortar * 0.55)
    albedo = np.stack([albedo_value * 1.0, albedo_value * 0.96, albedo_value * 0.9], axis=-1)
    write_material("floor", albedo, height, 0.78 + 0.18 * grain + mortar * 0.04, strength=0.35)

    rows = 6
    row = np.floor(y * rows)
    offset = (row % 2) * 0.5 / 3.0
    column = np.floor((x + offset) * 3.0)
    by = y * rows - row
    bx = (x + offset) * 3.0 - column
    joint = 1.0 - smoothstep(0.0, 0.03, np.minimum(np.minimum(by, 1.0 - by) / 1.0, np.minimum(bx, 1.0 - bx) / 2.0))
    block_id = ((row * 7 + column * 13) % 17).astype(np.int32)
    tone = rng.uniform(0.8, 1.1, 17).astype(np.float32)[block_id]
    grain = fbm2(size, 16, 5, rng)
    albedo_value = 0.42 * tone * (0.8 + 0.3 * grain) * (1.0 - joint * 0.5)
    albedo = np.stack([albedo_value, albedo_value * 0.97, albedo_value * 0.92], axis=-1)
    height = (1.0 - joint) * 0.7 + grain * 0.3
    write_material("pillar", albedo, height, 0.82 + 0.15 * grain, strength=0.3)


def write_material(name, albedo, height, roughness, strength):
    srgb = np.where(albedo <= 0.0031308, albedo * 12.92, 1.055 * np.power(np.clip(albedo, 0, None), 1 / 2.4) - 0.055)
    save(f"{name}_albedo.png", np.concatenate([srgb, np.ones(albedo.shape[:2] + (1,), np.float32)], -1))
    gy, gx = np.gradient(blur(height, 0.7))
    normal = np.stack([-gx * strength * height.shape[0] / 64.0, gy * strength * height.shape[0] / 64.0,
                       np.ones_like(height)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    save(f"{name}_normal.png", np.concatenate([normal * 0.5 + 0.5, np.ones(height.shape + (1,), np.float32)], -1))
    rough = np.clip(roughness, 0.0, 1.0)
    save(f"{name}_roughness.png", np.stack([rough, rough, rough, np.ones_like(rough)], axis=-1))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    everything = (smoke, lightning, explosion, ice, arcane, holy, ground, environment)
    wanted = set(sys.argv[1:])
    for function in everything:
        if not wanted or function.__name__ in wanted:
            function()
