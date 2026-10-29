"""Imports the effect atlases built from the Kenney particle pack (tools/out/atlases) into app:/data/Textures/Atlases."""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

paths = [p.replace(os.sep, "/") for p in sorted(glob.glob(os.path.join(HERE, "tools", "out", "atlases", "*.png")))]
data = json.loads(call("assets_import_files", {"paths": paths, "folder": "app:/data/Textures/Atlases", "wait_ms": 60000}))
print(len(paths), {k: v for k, v in data.items() if k != "results"})
