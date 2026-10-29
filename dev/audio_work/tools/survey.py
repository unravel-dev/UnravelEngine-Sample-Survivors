"""Decodes every Ogg in the audio packs once and prints a line per file (length, peak, loudness, centroid)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al  # noqa: E402

PACKS = ["sfx_80_rpg", "sfx_rpg_sound_pack", "sfx_swishes", "sfx_bone_breaks", "sfx_bones_rattle", "sfx_gem",
         "sfx_soundpack01", "kenney_impact_sounds", "kenney_interface_sounds", "kenney_scifi_sounds",
         "kenney_music_jingles", "kenney_rpg_audio", "music_battle", "music_boss", "music_menu"]
EXTS = (".ogg", ".wav", ".aif", ".aiff")

files = []
for pack in PACKS:
    for root, _, names in os.walk(os.path.join(al.DOWNLOADS, pack)):
        for name in sorted(names):
            if name.lower().endswith(EXTS):
                files.append(os.path.join(root, name))
al.predecode(files)
only = sys.argv[1] if len(sys.argv) > 1 else None
for path in files:
    rel = os.path.relpath(path, al.DOWNLOADS)
    if only and only not in rel:
        continue
    if "music_" in rel or "Preview" in rel:
        continue
    try:
        x = al.src(rel)
        print(f"{rel.replace(os.sep, '/'):80s} {al.describe(x)}")
    except Exception as e:  # noqa
        print(f"{rel}: ERROR {e}")
