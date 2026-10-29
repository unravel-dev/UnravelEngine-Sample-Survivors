"""Telegraph textures for Barrowlight (license-free, procedural): white masks tinted by the particle colour.

disc_fill.png   filled disc, slightly brighter toward the rim (the fill that grows over a wind-up)
ring_thin.png   a thin crisp ring (the outline that shows the final size from the start)
cone_90.png / cone_120.png / cone_200.png   sectors with the apex at the bottom centre, pointing up (+V)
aim_line.png    a thin line along the texture's height, brighter at the start (bottom)
"""
import math
import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "telegraphs")
SIZE = 256


def save(alpha, name, rgb=None):
    alpha = np.clip(alpha, 0.0, 1.0)
    rgba = np.zeros((alpha.shape[0], alpha.shape[1], 4), dtype=np.uint8)
    rgba[..., 0:3] = 255 if rgb is None else rgb
    rgba[..., 3] = (alpha * 255.0 + 0.5).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))


def grid(size=SIZE):
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    return (x + 0.5) / size * 2.0 - 1.0, (y + 0.5) / size * 2.0 - 1.0


def edge(distance, width):
    """1 inside, 0 outside, a soft edge of `width` (in normalized units)."""
    return np.clip(0.5 - distance / width, 0.0, 1.0)


def disc_fill():
    x, y = grid()
    r = np.sqrt(x * x + y * y)
    body = 0.55 + 0.45 * np.clip((r - 0.55) / 0.4, 0.0, 1.0) ** 2
    save(edge(r - 0.97, 0.03) * body, "disc_fill.png")


def ring_thin():
    x, y = grid()
    r = np.sqrt(x * x + y * y)
    band = edge(np.abs(r - 0.955) - 0.02, 0.02)
    save(band, "ring_thin.png")


def cone(degrees):
    """A sector centred on the texture, opening toward +V (up), radius = half the texture."""
    x, y = grid()
    py = -y
    r = np.sqrt(x * x + py * py)
    angle = np.degrees(np.arctan2(np.abs(x), py))
    half = degrees * 0.5
    inside = edge(angle - half, 2.0) * edge(r - 0.97, 0.03)
    body = 0.55 + 0.45 * np.clip((r - 0.5) / 0.45, 0.0, 1.0) ** 2
    rim = edge(np.abs(r - 0.955) - 0.02, 0.02) * edge(angle - half, 2.0)
    side = edge(np.abs(angle - half) - 0.8, 1.0) * edge(r - 0.97, 0.03)
    save(np.maximum(inside * body, np.maximum(rim, side)), f"cone_{degrees}.png")


def aim_line():
    """A thin line from the centre to the top edge of a square texture (the shooter sits at the centre)."""
    size = 512
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    px = (x + 0.5) - size / 2.0
    t = 1.0 - (y + 0.5) / (size / 2.0)
    core = np.exp(-(px / 1.6) ** 2)
    along = (0.45 + 0.55 * (1.0 - np.clip(t, 0.0, 1.0))) * (t > 0.0) * np.clip((1.0 - t) * 30.0, 0.0, 1.0)
    save(core * along, "aim_line.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    disc_fill()
    ring_thin()
    for degrees in (90, 120, 200):
        cone(degrees)
    aim_line()
    print(OUT)
