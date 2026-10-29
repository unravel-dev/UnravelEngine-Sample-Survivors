# Usage: python -I rot_check.py <reference.glb> <other.glb> ; per-joint rest rotation angle difference (degrees)
import json, math, struct, sys
def load(p):
    d = open(p, "rb").read(); jl = struct.unpack("<I", d[12:16])[0]; return json.loads(d[20:20 + jl])
def rots(doc):
    return {doc["nodes"][j]["name"]: doc["nodes"][j].get("rotation", [0, 0, 0, 1]) for j in doc["skins"][0]["joints"]}
a, b = rots(load(sys.argv[1])), rots(load(sys.argv[2]))
diffs = []
for n in a:
    dot = abs(sum(x * y for x, y in zip(a[n], b[n])))
    diffs.append((math.degrees(2 * math.acos(min(1.0, dot))), n))
diffs.sort(reverse=True)
print("joints with rest rotation difference > 0.01 deg:", [(n, round(d, 3)) for d, n in diffs if d > 0.01])
