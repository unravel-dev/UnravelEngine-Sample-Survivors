"""Verifies MCP array/list script fields on a probe script in the Sandbox scene, then removes the probe."""
import json
import time

from playutil import call

PROBE = """using System.Collections.Generic;
using Unravel.Core;

[ScriptSourceFile]
public class ArrayProbe : ScriptComponent
{
    public Prefab[] items;
    public List<int> numbers = new List<int>();
    public Material[] mats;
}
"""


def main():
    for _ in range(60):
        try:
            call("editor_get_status", {})
            break
        except Exception:
            time.sleep(2)
    print(call("project_open", {"path": "C:/Workspace/github/UnravelEngine/UnravelEngine-Sample-Survivors"})[:160])
    time.sleep(3)
    print(call("scene_open", {"key": "app:/data/Scenes/Sandbox.spfb"})[:160])
    time.sleep(2)
    print(call("scripts_set_sources_batch", {"items": [{"key": "app:/data/Scripts/Dev/ArrayProbe.cs", "code": PROBE}]})[:160])
    print(call("scripts_get_compile_status", {"wait": True, "timeout_ms": 180000})[:300])
    entity = json.loads(call("scene_create_batch", {"items": [{"kind": "empty", "name": "ArrayProbe"}]}))["created"][0]["id"]
    print(call("scene_add_components_batch", {"items": [{"entity_id": entity, "component": "Script", "type_name": "ArrayProbe"}]})[:160])
    get = {"items": [{"entity_id": entity, "component": "Script", "type_name": "ArrayProbe"}]}
    print("read 1:", call("scene_get_component_properties_batch", get)[:300])
    prefab = "app:/data/Prefabs/Heroes/Hero_Knight.pfb"
    material = "app:/data/Materials/Gem.mat"
    print("set:", call("scene_set_component_properties_batch", {"items": [{"entity_id": entity, "component": "Script", "type_name": "ArrayProbe",
                                                                         "properties": {"items": [prefab, None], "numbers": [3, 1, 4], "mats": [material]}}]})[:300])
    print("read 2:", call("scene_get_component_properties_batch", get)[:400])
    for bad in ({"numbers": [1, "x", 3]}, {"mats": ["no/such/asset"]}, {"numbers": 5}):
        print("bad:", call("scene_set_component_properties_batch", {"items": [{"entity_id": entity, "component": "Script", "type_name": "ArrayProbe", "properties": bad}]})[:240])
    print("read 3:", call("scene_get_component_properties_batch", get)[:400])
    print(call("scene_delete_entities_batch", {"entity_ids": [entity]})[:100])
    print(call("assets_delete_batch", {"items": [{"key": "app:/data/Scripts/Dev/ArrayProbe.cs"}]})[:200])
    print(call("scripts_get_compile_status", {"wait": True, "timeout_ms": 180000})[:200])
    print(call("scene_open", {"key": "app:/data/Scenes/Barrowlight.spfb", "force": True})[:160])


if __name__ == "__main__":
    main()
