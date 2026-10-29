import json, sys
sys.path.insert(0, "..")
from mcp import call
raw = call("scene_find_entities_batch", {"name_exact": "KnightHammerHaft"})
print(raw[:500])
r = json.loads(raw[raw.find("{"):])
ids = [e.get("id") or e.get("entity_id") for e in r.get("entities", r.get("results", []))]
ids = [i for i in ids if i]
print("ids", ids)
if ids:
    print(call("scene_delete_entities_batch", {"entity_ids": ids})[:300])
    print(call("scene_save", {})[:200])
print(call("assets_delete_batch", {"assets": ["app:/data/Scripts/Weapons/Knight/StunReader.cs"]})[:300])
