"""Night mood of the Barrowlight arena: moon, sky, exposure, fog, bloom, vignette; and the gameplay camera pose."""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

SUN = "2970a030-f235-6f44-baa7-b1c7a6fcd847"
VOLUME = "bca7d75d-723c-e446-b9ff-473d0609c63a"

MOOD = {
    "moon_rotation": [42, 150, 0],
    "moon_color": [0.58, 0.68, 1.0, 1.0],
    "moon_intensity": 4.0,
    "sky_brightness": 0.07,
    "irradiance": 0.7,
    "irradiance_tint": [0.62, 0.74, 1.0, 1.0],
    "compensation": -0.5,
    "min_ev": 0.5,
    "max_ev": 4.0,
    "bloom": 0.2,
    "fog_albedo": [0.32, 0.4, 0.46, 1.0],
    "fog_distance": 140.0,
    "fog_height": 8.0,
    "vignette": 0.3,
}


def apply(mood):
    items = [
        {"entity_id": SUN, "component": "Light", "properties": {"light": {
            "color": mood["moon_color"], "intensity": mood["moon_intensity"], "casts_shadows": True,
            "shadow_params": {"far_plane": 90}, "directional_shadow_params": {"splits": 3, "distribution": 0.75},
            "contact_shadow": {"enabled": True}}}},
        {"entity_id": SUN, "component": "Skylight", "properties": {
            "sky_brightness": mood["sky_brightness"], "cloud_mode": "volumetric", "cloud_coverage": 0.55,
            "cloud_brightness": 0.5, "irradiance_intensity": mood["irradiance"], "irradiance_tint": mood["irradiance_tint"]}},
        {"entity_id": VOLUME, "component": "Auto Exposure", "properties": {"enabled": True, "settings": {
            "compensation": mood["compensation"], "min_ev": mood["min_ev"], "max_ev": mood["max_ev"]}}},
        {"entity_id": VOLUME, "component": "Bloom", "properties": {"enabled": True, "settings": {"intensity": mood["bloom"]}}},
        {"entity_id": VOLUME, "component": "Fog", "properties": {"enabled": True, "settings": {
            "albedo": mood["fog_albedo"], "attenuation_distance": mood["fog_distance"], "maximum_height": mood["fog_height"],
            "base_height": 0, "sky_affect": 0.6}}},
        {"entity_id": VOLUME, "component": "Tonemapping", "properties": {"settings": {"vignette_intensity": mood["vignette"]}}},
    ]
    print(call("scene_set_component_properties_batch", {"items": items})[:600])
    print(call("scene_set_transforms_batch", {"items": [{"entity_id": SUN, "rotation_euler": mood["moon_rotation"]}]})[:200])


def place_camera(pitch=52.0, distance=21.0, focus=(0.0, 0.0, 0.0)):
    camera = json.loads(call("scene_find_entities_batch", {"name_contains": "Main Camera"}))["entities"][0]["id"]
    y = math.sin(math.radians(pitch)) * distance
    z = -math.cos(math.radians(pitch)) * distance
    print(call("scene_set_transforms_batch", {"items": [{"entity_id": camera, "position": [focus[0], focus[1] + y, focus[2] + z],
                                                          "rotation_euler": [pitch, 0, 0]}]})[:200])
    return camera


if __name__ == "__main__":
    apply(MOOD)
    place_camera()
