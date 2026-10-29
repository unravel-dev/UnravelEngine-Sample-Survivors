"""Boss effect textures for Barrowlight (license-free, procedural): white masks tinted by the particle colour.

    python gen_boss_textures.py            writes tools/out/boss_textures/*.png (+ a preview sheet)

lane_fill_4x4.png      16 frames (row-major) of a lane 3/14 of the tile wide with a crisp 2 px outline, filled at alpha
                       0.45 from the bottom (the near end, -V) up to (k + 1) / 16 of its height in frame k
cone_200_edge.png      only the rim arc and the two side edges of telegraphs/cone_200.png (apex at the centre, +V)
ring_gapped_60.png     telegraphs/ring_thin.png's band with two 60 deg gaps centred on +V and -V (soft 3 deg ends)
nova_segment.png       a soft bar along U (10% soft edges top and bottom, 20% fades at the ends), a brighter centre line
bone_spikes_2x2.png    jagged bone spikes rising from each frame's bottom edge, dark 2 px outline; frames 1 and 3 are
                       frames 0 and 2 mirrored (a random frame = a random flip)
dazed_stars.png        five four-point stars evenly on a circle of 80% of the radius
sigil_circle.png       two thin rings with a band of runes between them and a pentagram inside
ghost_mage.png         the Skeleton_Mage (Idle, front, orthographic) as a shaded white silhouette, 512 x 1024; the quad
                       is 1 : 2 (size_axes [1.2, 2.0]) and centred on the chest (model y 1.1), feet fading out
coin.png               a coin face: rim, inner face, a raised star; grey shading for a gold gradient

Seeds are fixed: the output is the same on every run.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from gen_weapon_textures import SEED, _python_rng, draw_supersampled, fbm, grid, stroke_polyline  # noqa: E402

OUT = os.path.join(HERE, "out", "boss_textures")
MAGE_GLTF = os.path.join(HERE, "out", "skeletons", "Skeleton_Mage", "Skeleton_Mage.gltf")
MAGE_CLIP = "Idle"
# The ghost's quad (model units): the script passes 1.8 x scale as the size and emits it at the chest (1.1 x scale);
# size_axes [1.2, 2.0] make it 2.16 wide and 3.6 tall around the chest.
GHOST_CHEST = 1.1
GHOST_HALF_WIDTH = 0.9 * 1.2
GHOST_HALF_HEIGHT = 0.9 * 2.0
GHOST_SIZE = (512, 1024)


def save(rgb, alpha, name):
    alpha = np.clip(alpha, 0.0, 1.0)
    rgb = np.clip(rgb, 0.0, 1.0)
    if np.ndim(rgb) == 0:
        rgb = np.full(alpha.shape, float(rgb))
    if rgb.ndim == 2:
        rgb = np.repeat(rgb[..., None], 3, axis=2)
    rgba = np.zeros((alpha.shape[0], alpha.shape[1], 4), dtype=np.uint8)
    rgba[..., 0:3] = (rgb * 255.0 + 0.5).astype(np.uint8)
    rgba[..., 3] = (alpha * 255.0 + 0.5).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))
    return name


def edge(distance, width):
    """1 inside, 0 outside, a soft edge of `width` (as gen_telegraphs.edge)."""
    return np.clip(0.5 - distance / width, 0.0, 1.0)


def lane_fill_4x4(tile=256):
    """Frame k: the outline in every frame, the fill rising from the bottom to (k + 1) / 16 of the height."""
    columns = 4
    frames = columns * columns
    ys, xs = np.mgrid[0:tile, 0:tile].astype(np.float64) + 0.5
    width = tile * 3.0 / 14.0
    left, right = (tile - width) * 0.5, (tile + width) * 0.5
    across = np.clip(np.minimum(xs - left, right - xs) + 0.5, 0.0, 1.0)
    outline_px = 2.0
    near_side = np.minimum(xs - left, right - xs) < outline_px
    near_end = (ys < outline_px) | (ys > tile - outline_px)
    outline = across * (near_side | near_end)
    sheet = np.zeros((tile * columns, tile * columns))
    for k in range(frames):
        top = tile * (1.0 - (k + 1) / frames)
        fill = np.clip(ys - top + 0.5, 0.0, 1.0) * across * 0.45
        row, column = divmod(k, columns)
        sheet[row * tile:(row + 1) * tile, column * tile:(column + 1) * tile] = np.maximum(outline, fill)
    return save(1.0, sheet, "lane_fill_4x4.png")


def cone_200_edge(size=256, degrees=200):
    """The rim and side terms of gen_telegraphs.cone(), without the body."""
    x, y = grid(size)
    py = -y
    r = np.sqrt(x * x + py * py)
    angle = np.degrees(np.arctan2(np.abs(x), py))
    half = degrees * 0.5
    rim = edge(np.abs(r - 0.955) - 0.02, 0.02) * edge(angle - half, 2.0)
    side = edge(np.abs(angle - half) - 0.8, 1.0) * edge(r - 0.97, 0.03)
    return save(1.0, np.maximum(rim, side), "cone_200_edge.png")


def ring_gapped_60(size=256, gap_degrees=60.0, soft_degrees=3.0):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    band = edge(np.abs(r - 0.955) - 0.02, 0.02)
    from_up = np.abs(np.degrees(np.arctan2(x, -y)))
    to_gap = np.minimum(from_up, 180.0 - from_up)
    open_part = np.clip((to_gap - gap_degrees * 0.5) / soft_degrees + 0.5, 0.0, 1.0)
    return save(1.0, band * open_part, "ring_gapped_60.png")


def nova_segment(size=256):
    x, y = grid(size)
    across = np.clip((1.0 - np.abs(y)) / 0.2, 0.0, 1.0)
    along = np.clip((1.0 - np.abs(x)) / 0.4, 0.0, 1.0)
    centre = np.exp(-(y / 0.22) ** 2)
    return save(0.77 + 0.23 * centre, across * along, "nova_segment.png")


def bone_spike_frame(tile, rng):
    """3-4 slightly curved, knobbly bone spikes standing on the bottom edge; shading lit from the left."""
    count = rng.randint(3, 4)
    slots = sorted(rng.uniform(-0.62, 0.62) for _ in range(count))
    shade_layers = []

    def to_pixels(points, scale):
        return [((p[0] + 1.0) * 0.5 * scale, (p[1] + 1.0) * 0.5 * scale) for p in points]

    for index, base_x in enumerate(slots):
        height = rng.uniform(0.95, 1.85)
        half_base = rng.uniform(0.11, 0.17)
        lean = rng.uniform(-0.25, 0.25) - base_x * 0.25
        steps = 8
        left, right, ridge = [], [], []
        for k in range(steps + 1):
            t = k / steps
            y = 1.0 - height * t
            centre = base_x + lean * t * t
            half = half_base * (1.0 - t) ** 0.85 + 0.008
            knob = 0.025 * math.sin(t * 9.0 + index) * (1.0 - t)
            left.append((centre - half - knob + rng.uniform(-0.012, 0.012) * (k > 0), y))
            right.append((centre + half + knob + rng.uniform(-0.012, 0.012) * (k > 0), y))
            ridge.append((centre - half * 0.15, y))
        left[0] = (left[0][0], 1.02)
        right[0] = (right[0][0], 1.02)
        ridge[0] = (ridge[0][0], 1.02)
        body = draw_supersampled(tile, lambda d, s: d.polygon(to_pixels(left + right[::-1], s), fill=255))
        lit = draw_supersampled(tile, lambda d, s: d.polygon(to_pixels(left + ridge[::-1], s), fill=255))
        shade_layers.append((body, 0.7 + 0.3 * lit))
    cover = np.zeros((tile, tile))
    shade = np.zeros((tile, tile))
    for body, face in shade_layers:
        shade = shade * (1.0 - body) + face * body
        cover = np.maximum(cover, body)
    dilated = np.asarray(Image.fromarray((cover * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(5))) / 255.0
    rgb = shade * cover + 0.12 * (1.0 - cover)
    return rgb, np.maximum(cover, dilated)


def bone_spikes_2x2(tile=256):
    rng = _python_rng(SEED + 10)
    rgb = np.zeros((tile * 2, tile * 2))
    alpha = np.zeros((tile * 2, tile * 2))
    for row in range(2):
        shade, cover = bone_spike_frame(tile, rng)
        for column, flip in enumerate((False, True)):
            frame_rgb = shade[:, ::-1] if flip else shade
            frame_alpha = cover[:, ::-1] if flip else cover
            rgb[row * tile:(row + 1) * tile, column * tile:(column + 1) * tile] = frame_rgb
            alpha[row * tile:(row + 1) * tile, column * tile:(column + 1) * tile] = frame_alpha
    return save(rgb, alpha, "bone_spikes_2x2.png")


def four_point_star(x, y, cx, cy, radius):
    """Alpha of a four-point star (an astroid) of `radius` centred at (cx, cy), with a small soft glow."""
    u = np.abs(x - cx) / radius
    v = np.abs(y - cy) / radius
    star = np.clip((1.0 - (np.sqrt(u) + np.sqrt(v))) * 6.0, 0.0, 1.0)
    glow = np.exp(-(u * u + v * v) * 9.0) * 0.5
    return np.maximum(star, glow)


def dazed_stars(size=256):
    x, y = grid(size)
    alpha = np.zeros((size, size))
    for k in range(5):
        angle = math.radians(90.0 + k * 72.0)
        alpha = np.maximum(alpha, four_point_star(x, y, 0.8 * math.cos(angle), -0.8 * math.sin(angle), 0.17))
    return save(1.0, alpha, "dazed_stars.png")


def sigil_circle(size=512):
    """Outer and inner thin rings, a band of procedural runes between them, a pentagram inside."""
    x, y = grid(size)
    pixel = 2.0 / size
    r = np.sqrt(x * x + y * y)
    rings = np.maximum(edge(np.abs(r - 0.95) - 0.012, pixel * 1.5), edge(np.abs(r - 0.78) - 0.009, pixel * 1.5))
    rings = np.maximum(rings, edge(np.abs(r - 0.32) - 0.007, pixel * 1.5))
    cover = np.zeros((size, size))
    halo = np.zeros((size, size))
    rng = _python_rng(SEED + 11)
    cells = 28
    inner, outer = 0.815, 0.915
    for cell in range(cells):
        start = cell / cells * math.tau
        span = math.tau / cells * 0.7

        def polar(u, v):
            angle = start + span * u
            radius = inner + (outer - inner) * v
            return (math.cos(angle) * radius, math.sin(angle) * radius)

        anchors = [(0.0, 0.0), (0.5, 0.0), (1.0, 0.0), (0.0, 0.5), (0.5, 0.5), (1.0, 0.5), (0.0, 1.0), (0.5, 1.0), (1.0, 1.0)]
        stem = rng.choice((0.0, 0.5, 1.0))
        strokes = [[(stem, 0.0), (stem, 1.0)]]
        for _ in range(rng.randint(1, 3)):
            a, b = rng.sample(anchors, 2)
            strokes.append([a, b])
        for stroke in strokes:
            points = [polar(u, v) for u, v in stroke]
            stroke_polyline(points, [0.007, 0.007], size, cover, halo, 0.01)
    star = np.zeros((size, size))
    vertices = [(math.cos(math.radians(90 + k * 72)) * 0.76, -math.sin(math.radians(90 + k * 72)) * 0.76) for k in range(5)]
    for k in range(5):
        stroke_polyline([vertices[k], vertices[(k + 2) % 5]], [0.008, 0.008], size, star, halo, 0.012)
    alpha = np.maximum(np.maximum(rings, cover), star)
    alpha = np.maximum(alpha, halo * 0.3) * edge(r - 0.985, pixel * 2.0)
    return save(1.0, alpha, "sigil_circle.png")


def ghost_mage():
    """The mage's Idle pose, front orthographic, painter-shaded triangles; alpha = coverage blurred by ~2 px."""
    import kaykit_merge as kaykit
    document = kaykit.GltfDocument(MAGE_GLTF)
    mesh_node = next(index for index, node in enumerate(document.nodes) if "mesh" in node)
    clip = kaykit.find_animation(document, MAGE_CLIP)
    evaluator = kaykit.AnimationEvaluator(document, clip)
    pose = kaykit.compute_global_matrices(document, evaluator.evaluate((evaluator.start + evaluator.end) * 0.5))
    positions, _ = kaykit.compute_node_vertices(document, mesh_node, pose)
    primitive = document.json["meshes"][document.nodes[mesh_node]["mesh"]]["primitives"][0]
    triangles = document.read_accessor(primitive["indices"])[:, 0].astype(np.int64).reshape(-1, 3)
    width, height = GHOST_SIZE[0] * 4, GHOST_SIZE[1] * 4
    pixel_x = (positions[:, 0] + GHOST_HALF_WIDTH) / (2.0 * GHOST_HALF_WIDTH) * width
    pixel_y = (GHOST_CHEST + GHOST_HALF_HEIGHT - positions[:, 1]) / (2.0 * GHOST_HALF_HEIGHT) * height
    corners = positions[triangles]
    normals = np.cross(corners[:, 1] - corners[:, 0], corners[:, 2] - corners[:, 0])
    normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
    light = np.array([-0.3, 0.5, 1.0])
    light /= np.linalg.norm(light)
    shades = 0.45 + 0.55 * np.abs(normals @ light)
    shade_image = Image.new("L", (width, height), 0)
    cover_image = Image.new("L", (width, height), 0)
    shade_draw, cover_draw = ImageDraw.Draw(shade_image), ImageDraw.Draw(cover_image)
    for triangle in np.argsort(corners[:, :, 2].mean(axis=1)):
        polygon = [(pixel_x[c], pixel_y[c]) for c in triangles[triangle]]
        shade_draw.polygon(polygon, fill=int(shades[triangle] * 255))
        cover_draw.polygon(polygon, fill=255)
    shade = np.asarray(shade_image.resize(GHOST_SIZE, Image.LANCZOS)).astype(np.float64) / 255.0
    cover_small = cover_image.resize(GHOST_SIZE, Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.2))
    cover = np.asarray(cover_small).astype(np.float64) / 255.0
    rows = np.arange(GHOST_SIZE[1], dtype=np.float64)[:, None] + 0.5
    model_y = GHOST_CHEST + GHOST_HALF_HEIGHT - rows / GHOST_SIZE[1] * 2.0 * GHOST_HALF_HEIGHT
    feet_fade = np.clip((model_y - 0.05) / 0.55, 0.0, 1.0)
    rgb = np.where(cover > 1e-3, shade / np.maximum(cover, 1e-3), 0.0)
    return save(np.clip(rgb, 0.0, 1.0), cover * 0.9 * feet_fade, "ghost_mage.png")


def coin(size=128):
    x, y = grid(size)
    pixel = 2.0 / size
    r = np.sqrt(x * x + y * y)
    disc = edge(r - 0.92, pixel * 1.5)
    rim = edge(np.abs(r - 0.83) - 0.08, pixel * 1.5)
    light = np.clip(0.75 - 0.25 * (x + y) / 1.4, 0.4, 1.0)
    star = four_point_star(x, y, 0.0, 0.0, 0.55)
    face = 0.72 * light + 0.28 * star
    rgb = np.where(rim > 0.5, 0.95 * light + 0.05, face)
    rgb = rgb * (0.8 + 0.2 * edge(r - 0.89, pixel * 3.0))
    return save(rgb, disc, "coin.png")


def preview(names):
    cell = 180
    sheet = Image.new("RGB", (cell * len(names), cell + 16), (36, 40, 46))
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(names):
        image = Image.open(os.path.join(OUT, name)).convert("RGBA")
        image.thumbnail((cell - 8, cell - 8))
        backdrop = Image.new("RGBA", image.size, (36, 40, 46, 255))
        backdrop.alpha_composite(image)
        sheet.paste(backdrop.convert("RGB"), (index * cell + 4, 4))
        draw.text((index * cell + 4, cell), name[:24], fill=(220, 220, 220))
    sheet.save(os.path.join(OUT, "_preview.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    names = [lane_fill_4x4(), cone_200_edge(), ring_gapped_60(), nova_segment(), bone_spikes_2x2(), dazed_stars(),
             sigil_circle(), coin()]
    if os.path.exists(MAGE_GLTF):
        names.append(ghost_mage())
    else:
        print("Skeleton_Mage.gltf not found, ghost_mage.png skipped:", MAGE_GLTF, file=sys.stderr)
    preview(names)
    for name in names:
        with Image.open(os.path.join(OUT, name)) as image:
            print(f"{name:22s} {image.size[0]}x{image.size[1]} {image.mode}")


if __name__ == "__main__":
    main()
