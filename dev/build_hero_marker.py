"""Hero readability (design 10.8, 11.3): a health ring under the feet and a warm light that follows the hero.

Generates the ring texture, imports it, creates the HeroRing material and the two entities under the follow root,
and wires them to Run (heroRing). Run tints the ring by health every frame.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

IDS = json.load(open(os.path.join(HERE, "game_ids.json")))
OUT = os.path.join(HERE, "tools", "out", "hero")
TEXTURE = "hero_ring.png"
TEXTURE_KEY = "app:/data/Textures/Hero/hero_ring.png"
MATERIAL_KEY = "app:/data/Materials/HeroRing.mat"
SIZE = 512
RING_DIAMETER = 1.9
# The embedded plane primitive is 10 units across.
PLANE_SIZE = 10.0
RING_HEIGHT = 0.035
LIGHT = {"color": [1.0, 0.78, 0.52, 1.0], "intensity": 55.0, "range": 8.0, "height": 3.2}


def make_texture():
    """A crisp band with four gaps (reads as a HUD ring, not a spell) and a faint inner wash."""
    coords = (np.arange(SIZE) + 0.5) / SIZE * 2.0 - 1.0
    x, y = np.meshgrid(coords, coords)
    r = np.sqrt(x * x + y * y)
    angle = np.degrees(np.arctan2(y, x)) % 90.0
    pixel = 2.0 / SIZE
    band = np.clip((0.045 - np.abs(r - 0.9)) / pixel, 0.0, 1.0)
    gap = np.clip((np.abs(angle - 45.0) - 4.0) / 1.5, 0.0, 1.0)
    wash = np.clip((0.86 - r) / 0.3, 0.0, 1.0) * np.clip(r / 0.86, 0.0, 1.0) ** 3 * 0.22
    alpha = np.maximum(band * gap, wash)
    rgba = np.zeros((SIZE, SIZE, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (alpha * 255.0 + 0.5).astype(np.uint8)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, TEXTURE)
    Image.fromarray(rgba, "RGBA").save(path)
    return path


def check(text):
    print(text[:400])
    return json.loads(text)


def main():
    path = make_texture()
    check(call("assets_import_files", {"paths": [path], "folder": "app:/data/Textures/Hero", "wait_ms": 20000}))
    existing = json.loads(call("assets_get_properties_batch", {"assets": [MATERIAL_KEY]}))
    if not existing.get("results", [{}])[0].get("ok"):
        check(call("assets_create_batch", {"items": [{"type": "material", "folder": "Materials", "name": "HeroRing"}], "wait_ms": 10000}))
    check(call("assets_set_properties_batch", {"items": [{"asset": MATERIAL_KEY, "properties": {
        "cull_type": "none", "alpha_mode": "blend", "base_color": [0.35, 1.0, 0.45, 1.0], "emissive_color": [0.35, 1.0, 0.45, 1.0],
        "emissive_intensity": 1.4, "roughness": 1.0, "metalness": 0.0, "color_map": TEXTURE_KEY, "emissive_map": TEXTURE_KEY}}]}))
    for name in ("HeroRing", "HeroLight"):
        old = json.loads(call("scene_find_entities_batch", {"name_exact": name, "limit": 4}))["entities"]
        if old:
            check(call("scene_delete_entities_batch", {"entity_ids": [e["id"] for e in old]}))
    result = check(call("scene_create_batch", {"items": [
        {"kind": "primitive", "primitive": "plane", "name": "HeroRing", "parent_id": IDS["follow"], "space": "local",
         "position": [0.0, RING_HEIGHT, 0.0], "scale": [RING_DIAMETER / PLANE_SIZE, 1.0, RING_DIAMETER / PLANE_SIZE], "material": MATERIAL_KEY},
        {"kind": "light", "light_type": "point", "name": "HeroLight", "parent_id": IDS["follow"], "space": "local",
         "position": [0.0, LIGHT["height"], 0.0]},
    ]}))
    ring, light = [entry["id"] for entry in result["created"]]
    check(call("scene_set_component_properties_batch", {"items": [
        {"entity_id": ring, "component": "Model", "properties": {"casts_shadow": False}},
        {"entity_id": light, "component": "Light", "properties": {"light": {
            "color": LIGHT["color"], "intensity": LIGHT["intensity"], "casts_shadows": False, "point_data": {"range": LIGHT["range"]}}}},
        {"entity_id": IDS["Run"], "component": "Script", "type_name": "Survivors.Run", "properties": {"heroRing": ring, "heroLight": light}},
    ]}))
    print(call("scene_save", {})[:120])
    IDS["HeroRing"] = ring
    IDS["HeroLight"] = light
    json.dump(IDS, open(os.path.join(HERE, "game_ids.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
