"""Weapon effect textures for Barrowlight (license-free, procedural): white masks tinted by the particle colour.

    python gen_weapon_textures.py            writes tools/out/weapon_textures/*.png (+ a preview sheet)

crack_radial.png      radial ground crack: bright crack lines (RGB white) around a dark impact blotch (RGB near black)
slash_x.png           two crossed, tapered slash strokes with a soft glow fringe
rock_spike_2x2.png    four upright jagged rock spikes (2x2 sheet); each spike's foot is at the frame's vertical
                      centre, so a vertical billboard centred on the ground shows the spike standing on it
rock_chunk_2x2.png    four faceted rock chunks (2x2 sheet), grey shading in RGB for debris
vortex.png            three-armed spiral swirl (rifts, whirlpools)
thorn_ring.png        a thorny ring at the quad's rim (r 0.93) over a faint inner fill (auras sized by diameter)
shock_ring.png        a bright ring line at r 0.93 with a soft halo (shockwaves sized by diameter hit their radius)
fire_8x8_white.png    the project's fire_8x8 flipbook turned into a white luminance mask, so tints set its colour

Every texture is RGBA with power-of-two sizes. Seeds are fixed: the output is the same on every run.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "weapon_textures")
FIRE_SOURCE = r"C:\Workspace\github\UnravelEngine\UnravelEngine-Sample-Survivors\data\Textures\Particles\fire_8x8.png"
SUPERSAMPLE = 4
SEED = 1337


def save(rgb, alpha, name):
    """Writes an RGBA PNG from rgb (h, w) or (h, w, 3) in 0..1 and alpha (h, w) in 0..1."""
    alpha = np.clip(alpha, 0.0, 1.0)
    rgb = np.clip(rgb, 0.0, 1.0)
    if rgb.ndim == 2:
        rgb = np.repeat(rgb[..., None], 3, axis=2)
    rgba = np.zeros((alpha.shape[0], alpha.shape[1], 4), dtype=np.uint8)
    rgba[..., 0:3] = (rgb * 255.0 + 0.5).astype(np.uint8)
    rgba[..., 3] = (alpha * 255.0 + 0.5).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))
    return name


def _python_rng(seed):
    return random.Random(seed)


def grid(size):
    """Pixel-centre coordinates in -1..1 (x right, y down)."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    return (x + 0.5) / size * 2.0 - 1.0, (y + 0.5) / size * 2.0 - 1.0


def smooth_noise(size, cells, rng):
    """Value noise of `cells` cells across, bicubic-upsampled to size x size, 0..1."""
    coarse = rng.random((cells + 1, cells + 1))
    image = Image.fromarray((coarse * 255.0).astype(np.uint8), "L").resize((size, size), Image.BICUBIC)
    return np.asarray(image).astype(np.float64) / 255.0


def fbm(size, rng, octaves=4, base=4):
    total = np.zeros((size, size))
    weight = 0.0
    for octave in range(octaves):
        amplitude = 0.5 ** octave
        total += smooth_noise(size, base * 2 ** octave, rng) * amplitude
        weight += amplitude
    return total / weight


def segment_distance(px, py, ax, ay, bx, by):
    """Distance from every pixel to segment ab, and the parameter t (0..1) of the closest point."""
    dx, dy = bx - ax, by - ay
    length_sq = max(dx * dx + dy * dy, 1e-12)
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / length_sq, 0.0, 1.0)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy)), t


def stroke_polyline(points, widths, size, cover, halo, halo_width):
    """Max-accumulates an antialiased tapered polyline (coordinates in -1..1) into cover and a soft halo."""
    pixel = 2.0 / size
    for (a, b), (wa, wb) in zip(zip(points[:-1], points[1:]), zip(widths[:-1], widths[1:])):
        reach = max(wa, wb) + halo_width + 2.0 * pixel
        lo_x = int(max(0, (min(a[0], b[0]) - reach + 1.0) / pixel))
        hi_x = int(min(size, (max(a[0], b[0]) + reach + 1.0) / pixel + 1))
        lo_y = int(max(0, (min(a[1], b[1]) - reach + 1.0) / pixel))
        hi_y = int(min(size, (max(a[1], b[1]) + reach + 1.0) / pixel + 1))
        if lo_x >= hi_x or lo_y >= hi_y:
            continue
        ys, xs = np.mgrid[lo_y:hi_y, lo_x:hi_x].astype(np.float64)
        px = (xs + 0.5) * pixel - 1.0
        py = (ys + 0.5) * pixel - 1.0
        distance, t = segment_distance(px, py, a[0], a[1], b[0], b[1])
        width = wa + (wb - wa) * t
        line = np.clip((width - distance) / pixel + 0.5, 0.0, 1.0)
        glow = np.clip(1.0 - np.maximum(distance - width, 0.0) / halo_width, 0.0, 1.0) ** 2
        cover[lo_y:hi_y, lo_x:hi_x] = np.maximum(cover[lo_y:hi_y, lo_x:hi_x], line)
        halo[lo_y:hi_y, lo_x:hi_x] = np.maximum(halo[lo_y:hi_y, lo_x:hi_x], glow)


def walk_crack(rng, start_radius, end_radius, angle, step, wobble):
    """A jagged path leaving the centre along `angle`: points every `step`, heading wandering by `wobble` rad."""
    points = [(math.cos(angle) * start_radius, math.sin(angle) * start_radius)]
    heading = angle
    radius = start_radius
    while radius < end_radius:
        heading += rng.uniform(-wobble, wobble)
        heading += (angle - heading) * 0.35
        x, y = points[-1]
        points.append((x + math.cos(heading) * step, y + math.sin(heading) * step))
        radius = math.hypot(*points[-1])
    return points


def crack_radial(size=512):
    """Radial cracks with branches around a dark impact blotch; the blotch's RGB is dark, the lines' RGB white."""
    rng_py = np.random.default_rng(SEED)
    rng = _python_rng(SEED)
    cover = np.zeros((size, size))
    halo = np.zeros((size, size))
    main_count = 11
    for index in range(main_count):
        angle = (index + rng.uniform(-0.3, 0.3)) / main_count * math.tau
        reach = rng.uniform(0.72, 0.97)
        points = walk_crack(rng, 0.07, reach, angle, 0.035, 0.45)
        widths = [0.024 * (1.0 - k / len(points)) ** 1.3 + 0.003 for k in range(len(points))]
        stroke_polyline(points, widths, size, cover, halo, 0.045)
        for _ in range(rng.randint(1, 3)):
            at = rng.randint(len(points) // 4, max(len(points) // 4 + 1, len(points) - 3))
            side = rng.choice((-1.0, 1.0))
            branch_angle = math.atan2(points[at][1], points[at][0]) + side * rng.uniform(0.45, 0.95)
            branch = [points[at]]
            heading = branch_angle
            for _step in range(rng.randint(4, 9)):
                heading += rng.uniform(-0.35, 0.35)
                x, y = branch[-1]
                branch.append((x + math.cos(heading) * 0.03, y + math.sin(heading) * 0.03))
            base_width = widths[at] * 0.7
            branch_widths = [base_width * (1.0 - k / len(branch)) + 0.002 for k in range(len(branch))]
            stroke_polyline(branch, branch_widths, size, cover, halo, 0.03)
    x, y = grid(size)
    r = np.hypot(x, y)
    rim = 0.24 + 0.07 * (fbm(size, rng_py, 3, 6) - 0.5) * 2.0
    blotch = np.clip((rim - r) / 0.12, 0.0, 1.0) ** 0.8 * 0.9
    lines = np.maximum(cover, halo * 0.35)
    alpha = lines + blotch * (1.0 - lines)
    # The cracks glow from the blotch's rim outward: inside it they stay dark, so the centre reads as the impact.
    emerge = 0.15 + 0.85 * np.clip((r - 0.1) / 0.18, 0.0, 1.0)
    bright = (cover + halo * 0.35 * (1.0 - cover)) * emerge
    rgb = np.where(alpha > 1e-4, (bright * 1.0 + blotch * (1.0 - lines) * 0.08) / np.maximum(alpha, 1e-4), 1.0)
    return save(rgb, alpha * np.clip((1.0 - r) / 0.04, 0.0, 1.0), "crack_radial.png")


def slash_x(size=512):
    """Two crossed crescent strokes, pointed at both ends, with a soft fringe."""
    x, y = grid(size)
    pixel = 2.0 / size
    cover = np.zeros((size, size))
    fringe = np.zeros((size, size))
    for angle, half_length, bend, thickness in ((45.0, 0.9, 0.09, 0.075), (-45.0, 0.8, -0.07, 0.065)):
        theta = math.radians(angle)
        along = x * math.cos(theta) - y * math.sin(theta)
        across = x * math.sin(theta) + y * math.cos(theta)
        u = along / half_length
        inside = np.clip(1.0 - u * u, 0.0, 1.0)
        centre = bend * inside
        width = thickness * inside ** 0.75
        distance = np.abs(across - centre)
        cover = np.maximum(cover, np.clip((width - distance) / pixel + 0.5, 0.0, 1.0) * (np.abs(u) < 1.0))
        glow = np.exp(-np.maximum(distance - width, 0.0) / 0.035) * inside ** 0.5
        fringe = np.maximum(fringe, glow * 0.45)
    alpha = cover + fringe * (1.0 - cover)
    r = np.hypot(x, y)
    alpha *= np.clip((1.0 - r) / 0.06, 0.0, 1.0)
    return save(np.ones_like(alpha), alpha, "slash_x.png")


def draw_supersampled(size, draw_fn):
    """Calls draw_fn(draw, scale) on an L image SUPERSAMPLE times larger and returns coverage 0..1 at size."""
    big = Image.new("L", (size * SUPERSAMPLE, size * SUPERSAMPLE), 0)
    draw_fn(ImageDraw.Draw(big), size * SUPERSAMPLE)
    return np.asarray(big.resize((size, size), Image.LANCZOS)).astype(np.float64) / 255.0


def spike_frame(size, rng, rng_np, twin):
    """One upright spike: tip near the top, foot at the frame centre, widening below it (buried part)."""
    tip_x = rng.uniform(-0.06, 0.06)
    tip_y = rng.uniform(-0.92, -0.8)
    foot = rng.uniform(0.2, 0.27)
    left, right = [], []
    steps = 9
    for k in range(steps + 1):
        t = k / steps
        y = tip_y + (1.0 - tip_y) * t
        half = 0.015 + foot * min(1.0, (y - tip_y) / (0.0 - tip_y)) ** 0.9 * (1.0 if y <= 0.0 else 1.0 + 0.35 * y)
        jag = 0.0 if k == 0 else rng.uniform(-0.03, 0.03)
        left.append((tip_x * (1.0 - t) - half + jag, y))
        right.append((tip_x * (1.0 - t) + half + rng.uniform(-0.03, 0.03) * (k > 0), y))
    ridge = [(tip_x * (1.0 - k / steps) + rng.uniform(-0.03, 0.03) * (k > 0) + 0.04 * k / steps, tip_y + (1.0 - tip_y) * k / steps)
             for k in range(steps + 1)]
    shapes = [(left, right, ridge, 1.0)]
    if twin:
        offset = rng.choice((-1.0, 1.0)) * rng.uniform(0.2, 0.26)
        scale = rng.uniform(0.45, 0.6)
        shrink = lambda pts: [(p[0] * scale * 0.8 + offset, p[1] * scale + (1.0 - scale) * 0.0) for p in pts]
        shapes.insert(0, (shrink(left), shrink(right), shrink(ridge), 0.8))

    def to_pixels(points, scale):
        return [((p[0] + 1.0) * 0.5 * scale, (p[1] + 1.0) * 0.5 * scale) for p in points]

    alpha = np.zeros((size, size))
    shade = np.zeros((size, size))
    for left_pts, right_pts, ridge_pts, tone in shapes:
        body = draw_supersampled(size, lambda d, s: d.polygon(to_pixels(left_pts + right_pts[::-1], s), fill=255))
        lit = draw_supersampled(size, lambda d, s: d.polygon(to_pixels(left_pts + ridge_pts[::-1], s), fill=255))
        face = 0.55 + 0.4 * lit
        shade = shade * (1.0 - body) + face * tone * body
        alpha = np.maximum(alpha, body)
    x, y = grid(size)
    grain = 0.82 + 0.18 * fbm(size, rng_np, 3, 8)
    strata = 1.0 - 0.1 * np.clip(np.sin((y + 0.07 * x) * 17.0 + fbm(size, rng_np, 2, 4) * 6.0) - 0.75, 0.0, 1.0) / 0.25
    return shade * grain * strata, alpha


def rock_spike_2x2(frame=512):
    rng = _python_rng(SEED + 1)
    rng_np = np.random.default_rng(SEED + 1)
    rgb = np.zeros((frame * 2, frame * 2))
    alpha = np.zeros((frame * 2, frame * 2))
    for index in range(4):
        shade, cover = spike_frame(frame, rng, rng_np, twin=index % 2 == 1)
        row, column = divmod(index, 2)
        rgb[row * frame:(row + 1) * frame, column * frame:(column + 1) * frame] = shade
        alpha[row * frame:(row + 1) * frame, column * frame:(column + 1) * frame] = cover
    return save(rgb, alpha, "rock_spike_2x2.png")


def chunk_frame(size, rng, rng_np):
    """A faceted rock: an irregular polygon split into facets around an off-centre peak, lit from the top left."""
    count = rng.randint(7, 10)
    angles = sorted(rng.uniform(0.0, math.tau) for _ in range(count))
    outline = [(math.cos(a) * rng.uniform(0.6, 0.86), math.sin(a) * rng.uniform(0.55, 0.82)) for a in angles]
    peak = (rng.uniform(-0.2, 0.15), rng.uniform(-0.25, 0.1))
    light = np.array([-0.55, -0.65, 0.52])
    light /= np.linalg.norm(light)

    def to_pixels(points, scale):
        return [((p[0] + 1.0) * 0.5 * scale, (p[1] + 1.0) * 0.5 * scale) for p in points]

    alpha = draw_supersampled(size, lambda d, s: d.polygon(to_pixels(outline, s), fill=255))
    shade = np.zeros((size, size))
    for k in range(count):
        a, b = outline[k], outline[(k + 1) % count]
        mid = np.array([(a[0] + b[0]) * 0.5 - peak[0], (a[1] + b[1]) * 0.5 - peak[1]])
        normal = np.array([mid[0], mid[1], 0.9])
        normal /= np.linalg.norm(normal)
        tone = 0.35 + 0.65 * max(float(normal @ light), 0.0)
        facet = draw_supersampled(size, lambda d, s: d.polygon(to_pixels([peak, a, b], s), fill=255))
        shade = shade * (1.0 - facet) + tone * facet
    edge = np.clip(alpha * 4.0 - 3.0, 0.0, 1.0)
    grain = 0.85 + 0.15 * fbm(size, rng_np, 3, 6)
    return shade * grain * (0.65 + 0.35 * edge), alpha


def rock_chunk_2x2(frame=256):
    rng = _python_rng(SEED + 2)
    rng_np = np.random.default_rng(SEED + 2)
    rgb = np.zeros((frame * 2, frame * 2))
    alpha = np.zeros((frame * 2, frame * 2))
    for index in range(4):
        shade, cover = chunk_frame(frame, rng, rng_np)
        row, column = divmod(index, 2)
        rgb[row * frame:(row + 1) * frame, column * frame:(column + 1) * frame] = shade
        alpha[row * frame:(row + 1) * frame, column * frame:(column + 1) * frame] = cover
    return save(rgb, alpha, "rock_chunk_2x2.png")


def vortex(size=512):
    """Three spiral arms winding into a bright eye; fades out at the rim."""
    x, y = grid(size)
    r = np.hypot(x, y) + 1e-6
    theta = np.arctan2(y, x)
    arms = 3
    twist = 2.6
    phase = arms * (theta + twist * np.log(r))
    wave = np.clip(0.5 + 0.5 * np.cos(phase), 0.0, 1.0) ** 2.2
    envelope = np.clip((0.97 - r) / 0.3, 0.0, 1.0) ** 1.5 * np.clip(r / 0.12, 0.0, 1.0)
    eye = np.exp(-(r / 0.13) ** 2) * 0.9
    streaks = 0.75 + 0.25 * fbm(size, np.random.default_rng(SEED + 3), 3, 6)
    alpha = np.clip(wave * envelope * streaks * 0.95 + eye, 0.0, 1.0)
    return save(np.ones_like(alpha), alpha, "vortex.png")


def thorn_ring(size=512):
    """A ring band at r 0.88..0.97 with thorns pointing out and in, over a faint fill that brightens toward it."""
    x, y = grid(size)
    pixel = 2.0 / size
    r = np.hypot(x, y)
    theta = np.arctan2(y, x)
    band_centre, band_half = 0.915, 0.022
    band = np.clip((band_half - np.abs(r - band_centre)) / pixel + 0.5, 0.0, 1.0)
    rng = _python_rng(SEED + 4)
    thorns = np.zeros((size, size))
    count = 28
    for k in range(count):
        for outward in (True, False):
            angle = (k + (0.0 if outward else 0.5) + rng.uniform(-0.15, 0.15)) / count * math.tau
            length = rng.uniform(0.045, 0.075) if outward else rng.uniform(0.035, 0.06)
            lean = rng.uniform(0.15, 0.35)
            base_r = band_centre + (band_half if outward else -band_half)
            tip_r = base_r + (length if outward else -length)
            tip_angle = angle + lean * length / base_r
            base_half = 0.022 / base_r
            delta = np.angle(np.exp(1j * (theta - angle)))
            t = np.clip((r - base_r) / (tip_r - base_r), 0.0, 1.0)
            within = ((r - base_r) * (tip_r - base_r) >= 0.0) & ((r - tip_r) * (base_r - tip_r) >= 0.0)
            centre = (tip_angle - angle) * t
            half = base_half * (1.0 - t)
            distance = np.abs(delta - centre) * r
            thorns = np.maximum(thorns, np.clip((half * r - distance) / pixel + 0.5, 0.0, 1.0) * within)
    fill = np.clip(r / band_centre, 0.0, 1.0) ** 3 * 0.22 * (r < band_centre)
    alpha = np.maximum(np.maximum(band, thorns), fill)
    alpha *= np.clip((1.0 - r) / 0.01, 0.0, 1.0)
    return save(np.ones_like(alpha), alpha, "thorn_ring.png")


def shock_ring(size=512):
    """A bright ring line at r 0.93 with a soft halo and a faint inner fill: scripted rings pass their diameter, so the
    line lands on the hit radius (ring_energy's line sits at r 0.83)."""
    x, y = grid(size)
    r = np.hypot(x, y)
    line = np.exp(-((r - 0.93) / 0.022) ** 2)
    halo = np.exp(-((r - 0.89) / 0.09) ** 2) * 0.45
    fill = np.clip(r / 0.93, 0.0, 1.0) ** 4 * 0.16 * (r < 0.93)
    streaks = 0.8 + 0.2 * fbm(size, np.random.default_rng(SEED + 5), 3, 8)
    alpha = np.clip(np.maximum(line, halo * streaks) + fill, 0.0, 1.0) * np.clip((1.0 - r) / 0.02, 0.0, 1.0)
    return save(np.ones_like(alpha), alpha, "shock_ring.png")


def fire_white():
    """The fire flipbook as a white mask: luminance becomes brightness (the hot core stays hottest), alpha kept."""
    source = np.asarray(Image.open(FIRE_SOURCE).convert("RGBA")).astype(np.float64) / 255.0
    luminance = 0.2126 * source[..., 0] + 0.7152 * source[..., 1] + 0.0722 * source[..., 2]
    reference = np.percentile(luminance[source[..., 3] > 0.04], 99.5)
    brightness = np.clip(luminance / max(reference, 1e-3), 0.0, 1.0) ** 0.8
    return save(brightness, source[..., 3], "fire_8x8_white.png")


def preview(names):
    """A dark contact sheet of every texture (tools/out/weapon_textures/_preview.png, not an asset)."""
    cell = 200
    sheet = Image.new("RGB", (cell * len(names), cell + 16), (36, 40, 46))
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(names):
        image = Image.open(os.path.join(OUT, name)).convert("RGBA")
        image.thumbnail((cell - 8, cell - 8))
        backdrop = Image.new("RGBA", image.size, (36, 40, 46, 255))
        backdrop.alpha_composite(image)
        sheet.paste(backdrop.convert("RGB"), (index * cell + 4, 4))
        draw.text((index * cell + 4, cell), name, fill=(220, 220, 220))
    sheet.save(os.path.join(OUT, "_preview.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    names = [crack_radial(), slash_x(), rock_spike_2x2(), rock_chunk_2x2(), vortex(), thorn_ring(), shock_ring()]
    if os.path.exists(FIRE_SOURCE):
        names.append(fire_white())
    else:
        print("fire_8x8.png not found, fire_8x8_white.png skipped:", FIRE_SOURCE, file=sys.stderr)
    preview(names)
    for name in names:
        with Image.open(os.path.join(OUT, name)) as image:
            print(f"{name:22s} {image.size[0]}x{image.size[1]} {image.mode}")


if __name__ == "__main__":
    main()
