# Usage: python -I anim_scan.py <dir> ; lists glTF/GLB files that carry animations or several mesh nodes
import json, os, struct, sys
for base, _d, names in os.walk(sys.argv[1]):
    for n in sorted(names):
        if not n.lower().endswith((".glb", ".gltf")):
            continue
        data = open(os.path.join(base, n), "rb").read()
        doc = json.loads(data[20:20 + struct.unpack("<I", data[12:16])[0]]) if data[:4] == b"glTF" else json.loads(data)
        anims = [a.get("name") for a in doc.get("animations", [])]
        meshes = [x.get("name") for x in doc.get("nodes", []) if "mesh" in x]
        if anims or len(meshes) > 1:
            print(n, "| anims", anims, "| mesh nodes", meshes)
