"""Imports the audio library (src/Audio) into app:/data/Audio."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from mcp import call  # noqa: E402

source = os.path.join(HERE, "src", "Audio").replace(os.sep, "/")
data = json.loads(call("assets_import_files", {"paths": [source], "folder": "app:/data", "wait_ms": 60000}))
print({k: v for k, v in data.items() if k != "results"})
print([r for r in data.get("results", []) if r.get("state") != "ready"][:5])
