"""Imports the generated weapon and boss effect textures (contact sheets excluded)."""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

for source, folder in (("weapon_textures", "weapons"), ("boss_textures", "bosses")):
    paths = [p.replace(os.sep, "/") for p in sorted(glob.glob(os.path.join(HERE, "tools", "out", source, "*.png")))
             if not os.path.basename(p).startswith("_")]
    data = json.loads(call("assets_import_files", {"paths": paths, "folder": f"app:/data/Textures/{folder}", "wait_ms": 60000}))
    print(folder, len(paths), {k: v for k, v in data.items() if k != "results"})
