# Usage: python -I clip_check.py <reference.glb> <other.glb> ; compares clip durations and channel counts by name
import json, struct, sys
def load(p):
    d = open(p, "rb").read(); jl = struct.unpack("<I", d[12:16])[0]; return json.loads(d[20:20 + jl])
def clips(doc):
    out = {}
    for a in doc.get("animations", []):
        dur = max(doc["accessors"][s["input"]]["max"][0] for s in a["samplers"])
        out[a["name"]] = (round(dur, 4), len(a["channels"]))
    return out
a, b = clips(load(sys.argv[1])), clips(load(sys.argv[2]))
same = [n for n in a if n in b and a[n] == b[n]]
diff = [(n, a[n], b[n]) for n in a if n in b and a[n] != b[n]]
print("common clips:", len(same) + len(diff), "same duration+channels:", len(same), "different:", diff[:20])
print("other-only clip durations:", {n: b[n][0] for n in b if n not in a})
