# Usage: python -I rig_check.py <reference.glb> <other.glb>...
# Compares joint rest rotations/scales and inverse bind matrices of skin 0, and lists materials.
import json, struct, sys

def load(path):
    data = open(path, "rb").read()
    jl = struct.unpack("<I", data[12:16])[0]
    doc = json.loads(data[20:20 + jl])
    pos = 20 + jl
    bl = struct.unpack("<I", data[pos:pos + 4])[0]
    return doc, data[pos + 8:pos + 8 + bl]

def ibms(doc, blob):
    acc = doc["accessors"][doc["skins"][0]["inverseBindMatrices"]]
    bv = doc["bufferViews"][acc["bufferView"]]
    off = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    vals = struct.unpack("<%df" % (16 * acc["count"]), blob[off:off + 64 * acc["count"]])
    return [vals[i * 16:(i + 1) * 16] for i in range(acc["count"])]

def rest(doc):
    out = {}
    for j in doc["skins"][0]["joints"]:
        n = doc["nodes"][j]
        out[n["name"]] = (n.get("rotation", [0, 0, 0, 1]), n.get("scale", [1, 1, 1]))
    return out

ref, rblob = load(sys.argv[1])
rnames = [ref["nodes"][j]["name"] for j in ref["skins"][0]["joints"]]
rrest, ribm = rest(ref), dict(zip(rnames, ibms(ref, rblob)))
for p in sys.argv[2:]:
    doc, blob = load(p)
    names = [doc["nodes"][j]["name"] for j in doc["skins"][0]["joints"]]
    r, ib = rest(doc), dict(zip(names, ibms(doc, blob)))
    drot = max(max(abs(a - b) for a in [0] for a, b in zip(r[n][0], rrest[n][0])) for n in names)
    dscl = max(max(abs(a - b) for a, b in zip(r[n][1], rrest[n][1])) for n in names)
    dibm = max(max(abs(a - b) for a, b in zip(ib[n], ribm[n])) for n in names)
    rig_node = [n for n in doc["nodes"] if n.get("name") == "Rig"]
    mats = [(m.get("name"), m.get("pbrMetallicRoughness", {}).get("baseColorFactor"), m.get("emissiveFactor"),
             "tex" if m.get("pbrMetallicRoughness", {}).get("baseColorTexture") else "-") for m in doc.get("materials", [])]
    users = {}
    for n in doc["nodes"]:
        if "mesh" in n:
            for pr in doc["meshes"][n["mesh"]]["primitives"]:
                users.setdefault(doc["materials"][pr.get("material", 0)]["name"], []).append(n["name"])
    print(p.split("/")[-1].split("\\")[-1], "max |drot| %.6f |dscale| %.6f |dIBM| %.6f" % (drot, dscl, dibm),
          "| Rig node", [(n.get("translation"), n.get("rotation"), n.get("scale")) for n in rig_node])
    print("   materials", mats)
    print("   material users", {k: v for k, v in users.items()})
