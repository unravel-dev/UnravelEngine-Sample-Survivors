"""Imports the curated Kenney particle textures (tools/out/kenney) into app:/data/Textures/Kenney."""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

paths = [p.replace(os.sep, "/") for p in sorted(glob.glob(os.path.join(HERE, "tools", "out", "kenney", "*.png")))]
data = json.loads(call("assets_import_files", {"paths": paths, "folder": "app:/data/Textures/Kenney", "wait_ms": 60000}))
print(len(paths), {k: v for k, v in data.items() if k != "results"})
