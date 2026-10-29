"""Lists the props hanging off hand slots, head and chest of hero entities: python props.py <entity id>..."""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from mcp import call  # noqa: E402

SLOTS = ("handslot.l", "handslot.r", "head", "chest")


def walk(node, parent, out):
    if parent in SLOTS:
        out.append((parent, node["name"], node["id"]))
    for child in node.get("children", []):
        walk(child, node["name"], out)


for entity in sys.argv[1:]:
    data = json.loads(call("scene_get_hierarchy_batch", {"parent_id": entity, "max_depth": 30, "limit": 400}))
    out = []
    for root in data["entities"]:
        walk(root, "", out)
    print(entity)
    for slot, name, ident in out:
        print(f"  {slot:11} {name:28} {ident}")
