"""Generates the arena ground texture and the gem mesh for Barrowlight (license-free, procedural)."""
import json
import math
import os
import struct

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "art")
SIZE = 1024


def periodic_noise(size, cells, seed):
    """Smooth value noise that tiles: a periodic grid of random values, bicubic-like smoothstep interpolation."""
    rng = np.random.default_rng(seed)
    grid = rng.random((cells, cells))
    coords = np.arange(size) * cells / size
    i0 = np.floor(coords).astype(int) % cells
    i1 = (i0 + 1) % cells
    f = coords - np.floor(coords)
    f = f * f * (3.0 - 2.0 * f)
    top = grid[i0][:, i0] * (1 - f)[None, :] + grid[i0][:, i1] * f[None, :]
    bottom = grid[i1][:, i0] * (1 - f)[None, :] + grid[i1][:, i1] * f[None, :]
    return top * (1 - f)[:, None] + bottom * f[:, None]


def fbm(size, seed, octaves=((4, 0.5), (8, 0.25), (16, 0.15), (32, 0.1))):
    total = np.zeros((size, size))
    weight = 0.0
    for k, (cells, amplitude) in enumerate(octaves):
        total += periodic_noise(size, cells, seed + k * 101) * amplitude
        weight += amplitude
    return total / weight


def hex_to_rgb(value):
    value = value.lstrip("#")
    return np.array([int(value[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64) / 255.0


def ground_texture():
    """Stylized moor: two moss greens, dry-grass patches, dirt hollows and fine speckles; tiles seamlessly."""
    moss_dark = hex_to_rgb("#38432f")
    moss = hex_to_rgb("#414d33")
    dry = hex_to_rgb("#545438")
    dirt = hex_to_rgb("#3f392d")
    large = fbm(SIZE, 11)
    patches = fbm(SIZE, 23, ((6, 0.6), (12, 0.3), (24, 0.1)))
    hollows = fbm(SIZE, 37, ((5, 0.7), (10, 0.3)))
    detail = periodic_noise(SIZE, 256, 51) * 0.6 + periodic_noise(SIZE, 128, 53) * 0.4
    t = np.clip((large - 0.35) / 0.3, 0.0, 1.0)[..., None]
    color = moss_dark * (1 - t) + moss * t
    dry_mask = np.clip((patches - 0.55) / 0.15, 0.0, 1.0)[..., None] * 0.45
    color = color * (1 - dry_mask) + dry * dry_mask
    dirt_mask = np.clip((hollows - 0.62) / 0.12, 0.0, 1.0)[..., None] * 0.45
    color = color * (1 - dirt_mask) + dirt * dirt_mask
    # Quantised speckles keep the flat KayKit look instead of photographic noise.
    speck = np.round((detail - 0.5) * 6.0) / 6.0
    color = color * (1.0 + speck[..., None] * 0.09)
    color = np.clip(color, 0.0, 1.0)
    image = Image.fromarray((color * 255.0 + 0.5).astype(np.uint8), "RGB")
    path = os.path.join(OUT, "moor_ground.png")
    image.save(path)
    return path


def write_glb(path, positions, normals, indices, name):
    positions = np.asarray(positions, dtype=np.float32)
    normals = np.asarray(normals, dtype=np.float32)
    indices = np.asarray(indices, dtype=np.uint16)
    blobs = [positions.tobytes(), normals.tobytes(), indices.tobytes()]
    offsets = []
    data = b""
    for blob in blobs:
        offsets.append(len(data))
        data += blob
        data += b"\0" * ((4 - len(data) % 4) % 4)
    doc = {
        "asset": {"version": "2.0", "generator": "barrowlight gen_art.py"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": name}],
        "meshes": [{"name": name, "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1}, "indices": 2, "material": 0}]}],
        "materials": [{"name": name, "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0.0, "roughnessFactor": 0.25}}],
        "buffers": [{"byteLength": len(data)}],
        "bufferViews": [
            {"buffer": 0, "byteOffset": offsets[0], "byteLength": len(blobs[0]), "target": 34962},
            {"buffer": 0, "byteOffset": offsets[1], "byteLength": len(blobs[1]), "target": 34962},
            {"buffer": 0, "byteOffset": offsets[2], "byteLength": len(blobs[2]), "target": 34963},
        ],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": len(positions), "type": "VEC3",
             "min": positions.min(axis=0).tolist(), "max": positions.max(axis=0).tolist()},
            {"bufferView": 1, "componentType": 5126, "count": len(normals), "type": "VEC3"},
            {"bufferView": 2, "componentType": 5123, "count": len(indices), "type": "SCALAR"},
        ],
    }
    text = json.dumps(doc, separators=(",", ":")).encode("ascii")
    text += b" " * ((4 - len(text) % 4) % 4)
    total = 12 + 8 + len(text) + 8 + len(data)
    with open(path, "wb") as file:
        file.write(struct.pack("<III", 0x46546C67, 2, total))
        file.write(struct.pack("<II", len(text), 0x4E4F534A))
        file.write(text)
        file.write(struct.pack("<II", len(data), 0x004E4942))
        file.write(data)


def gem_mesh():
    """A hexagonal bipyramid with a short girdle, 1 m tall, flat shaded (every face its own vertices)."""
    sides = 6
    radius = 0.42
    girdle = 0.08
    top = np.array([0.0, 0.5, 0.0])
    bottom = np.array([0.0, -0.5, 0.0])
    upper = [np.array([math.cos(a) * radius, girdle, math.sin(a) * radius]) for a in np.linspace(0, 2 * math.pi, sides, endpoint=False)]
    lower = [np.array([p[0], -girdle, p[2]]) for p in upper]
    triangles = []
    for i in range(sides):
        j = (i + 1) % sides
        triangles.append((top, upper[j], upper[i]))
        triangles.append((upper[i], upper[j], lower[j]))
        triangles.append((upper[i], lower[j], lower[i]))
        triangles.append((bottom, lower[i], lower[j]))
    positions, normals, indices = [], [], []
    for a, b, c in triangles:
        normal = np.cross(b - a, c - a)
        normal /= np.linalg.norm(normal)
        if np.dot(normal, (a + b + c) / 3.0) < 0.0:
            b, c = c, b
            normal = -normal
        base = len(positions)
        positions += [a, b, c]
        normals += [normal, normal, normal]
        indices += [base, base + 1, base + 2]
    path = os.path.join(OUT, "gem.glb")
    write_glb(path, positions, normals, indices, "Gem")
    return path


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print(ground_texture())
    print(gem_mesh())
