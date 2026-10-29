# Usage: python -I inventory.py <dest_root> <knight_glb> <out_md>
# Reads the downloaded survivors packs (untrusted data, parsed only) and writes an ASCII
# markdown inventory: skeleton rigs vs the Knight rig, environment kits, audio, VFX, fonts.
import json
import math
import os
import re
import struct
import sys
import wave

AUDIO_EXTENSIONS = (".wav", ".ogg", ".mp3", ".flac", ".aif", ".aiff")
MODEL_EXTENSIONS = (".glb", ".gltf", ".obj", ".fbx")
TEXTURE_EXTENSIONS = (".png", ".jpg", ".jpeg", ".tga")
GLB_JSON_CHUNK = 0x4E4F534A
OGG_TAIL_BYTES = 1 << 16
TRANSLATION_EPSILON = 1e-4
AUDIO_PACKS = ["sfx_80_rpg", "sfx_rpg_sound_pack", "sfx_swishes", "sfx_bone_breaks", "sfx_bones_rattle", "sfx_gem",
               "sfx_soundpack01", "kenney_impact_sounds", "kenney_interface_sounds", "kenney_scifi_sounds",
               "kenney_music_jingles", "kenney_rpg_audio", "music_battle", "music_boss", "music_menu"]
VFX_PACKS = ["kenney_particle_pack", "kenney_smoke_particles"]
MP3_BITRATES = {1: [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320],
                2: [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160]}
MP3_RATES = {3: [44100, 48000, 32000], 2: [22050, 24000, 16000], 0: [11025, 12000, 8000]}


def ascii_text(s):
    return str(s).encode("ascii", "replace").decode("ascii")


def walk_files(root):
    """Every file under root, sorted, without macOS AppleDouble / Finder metadata entries."""
    out = []
    for base, dirs, names in os.walk(root):
        dirs[:] = sorted(d for d in dirs if d != "__MACOSX")
        for n in sorted(names):
            if n.startswith("._") or n == ".DS_Store":
                continue
            out.append(os.path.join(base, n))
    return out


def rel(path, root):
    return os.path.relpath(path, root).replace("\\", "/")


# ---------------------------------------------------------------- glTF
def load_gltf_json(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] == b"glTF":
        length, kind = struct.unpack("<II", data[12:20])
        if kind != GLB_JSON_CHUNK:
            raise ValueError("first chunk is not JSON")
        return json.loads(data[20:20 + length])
    return json.loads(data)


def load_glb_bin(path):
    """Return the BIN chunk of a .glb (empty bytes when absent)."""
    with open(path, "rb") as f:
        data = f.read()
    length = struct.unpack("<I", data[12:16])[0]
    pos = 20 + length
    if pos + 8 > len(data):
        return b""
    bin_length = struct.unpack("<I", data[pos:pos + 4])[0]
    return data[pos + 8:pos + 8 + bin_length]


def inverse_bind_matrices(doc, blob):
    skin = doc["skins"][0]
    acc = doc["accessors"][skin["inverseBindMatrices"]]
    view = doc["bufferViews"][acc["bufferView"]]
    offset = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    values = struct.unpack("<%df" % (16 * acc["count"]), blob[offset:offset + 64 * acc["count"]])
    names = [node_name(doc, j) for j in skin["joints"]]
    return {n: values[i * 16:(i + 1) * 16] for i, n in enumerate(names)}


def rest_rotations(doc):
    return {node_name(doc, j): doc["nodes"][j].get("rotation", [0.0, 0.0, 0.0, 1.0]) for j in doc["skins"][0]["joints"]}


def clip_table(doc):
    """Clip name -> (duration seconds, channel count)."""
    out = {}
    for a in doc.get("animations", []):
        duration = max(doc["accessors"][smp["input"]]["max"][0] for smp in a["samplers"])
        out[a.get("name")] = (round(duration, 4), len(a["channels"]))
    return out


def bind_pose_lines(knight, path):
    doc, blob = load_gltf_json(path), load_glb_bin(path)
    ibm = inverse_bind_matrices(doc, blob)
    ibm_delta = max(max(abs(a - b) for a, b in zip(ibm[n], knight["ibm"][n])) for n in ibm if n in knight["ibm"])
    rots = rest_rotations(doc)
    angles = []
    for n, q in rots.items():
        if n in knight["rot"]:
            dot = min(1.0, abs(sum(a * b for a, b in zip(q, knight["rot"][n]))))
            angles.append((math.degrees(2.0 * math.acos(dot)), n))
    angles.sort(reverse=True)
    clips = clip_table(doc)
    common = [n for n in clips if n in knight["clip_table"]]
    same = [n for n in common if clips[n] == knight["clip_table"][n]]
    return ["- inverse bind matrices vs Knight: max abs delta %.6f" % ibm_delta,
            "- rest rotations vs Knight: largest angle %.3f deg (%s); larger raw quaternion deltas are sign flips (q vs -q)" % (
                angles[0][0], angles[0][1]) if angles else "- rest rotations: n/a",
            "- clips shared with Knight by name: %d, same duration and channel count: %d" % (len(common), len(same)),
            "- durations of the clips not in Knight: " + ", ".join("%s %.2fs" % (n, clips[n][0]) for n in clips if n not in knight["clip_table"])]


def node_name(doc, index):
    if index < 0:
        return "-"
    return doc["nodes"][index].get("name", "node%d" % index)


def parent_table(doc):
    parents = [-1] * len(doc.get("nodes", []))
    for i, node in enumerate(doc.get("nodes", [])):
        for c in node.get("children", []):
            parents[c] = i
    return parents


def joint_rig(doc, skin_index=0):
    """Return [(joint name, parent joint name or '-', local translation)] for a skin."""
    skin = doc["skins"][skin_index]
    parents = parent_table(doc)
    joints = skin["joints"]
    joint_set = set(joints)
    rig = []
    for j in joints:
        p = parents[j]
        while p != -1 and p not in joint_set:
            p = parents[p]
        t = doc["nodes"][j].get("translation", [0.0, 0.0, 0.0])
        rig.append((node_name(doc, j), node_name(doc, p), t))
    return rig


def mesh_nodes(doc):
    parents = parent_table(doc)
    used_skins = {n["skin"] for n in doc.get("nodes", []) if "mesh" in n and "skin" in n}
    joint_nodes = {j for s in used_skins for j in doc["skins"][s]["joints"]}
    rows = []
    for i, node in enumerate(doc.get("nodes", [])):
        if "mesh" not in node:
            continue
        prims = doc["meshes"][node["mesh"]]["primitives"]
        verts = sum(doc["accessors"][p["attributes"]["POSITION"]]["count"] for p in prims)
        tris = sum((doc["accessors"][p["indices"]]["count"] if "indices" in p else
                    doc["accessors"][p["attributes"]["POSITION"]]["count"]) // 3 for p in prims)
        if "skin" in node:
            kind, bone = "skinned", "skin %d" % node["skin"]
        else:
            a = i
            while a != -1 and a not in joint_nodes:
                a = parents[a]
            kind, bone = "rigid", node_name(doc, a)
        rows.append((node_name(doc, i), kind, node_name(doc, parents[i]), bone, verts, tris))
    return rows


def compare_rigs(ref, other):
    ref_names = [r[0] for r in ref]
    names = [r[0] for r in other]
    lines = []
    same_order = ref_names == names
    missing = [n for n in ref_names if n not in names]
    extra = [n for n in names if n not in ref_names]
    lines.append("- joint names: %s (%d vs Knight %d)%s" % (
        "IDENTICAL, same order" if same_order else ("same set, different order" if not missing and not extra else "DIFFERENT"),
        len(names), len(ref_names), ""))
    if missing:
        lines.append("  - missing vs Knight: " + ", ".join(missing))
    if extra:
        lines.append("  - extra vs Knight: " + ", ".join(extra))
    ref_parent = {r[0]: r[1] for r in ref}
    ref_t = {r[0]: r[2] for r in ref}
    parent_diff = [n for n, p, _t in other if n in ref_parent and ref_parent[n] != p]
    lines.append("- joint hierarchy (parent of each joint): %s" % (
        "identical" if not parent_diff else "differs at " + ", ".join(parent_diff)))
    deltas = []
    for n, _p, t in other:
        if n in ref_t:
            d = max(abs(a - b) for a, b in zip(t, ref_t[n]))
            deltas.append((d, n))
    deltas.sort(reverse=True)
    moved = [(d, n) for d, n in deltas if d > TRANSLATION_EPSILON]
    if not moved:
        lines.append("- rest pose local translations: identical to Knight (max delta %.6f)" % (deltas[0][0] if deltas else 0.0))
    else:
        lines.append("- rest pose local translations: %d of %d joints differ from Knight; largest: %s" % (
            len(moved), len(deltas), ", ".join("%s %.4f" % (n, d) for d, n in moved[:6])))
    return lines


def glb_section(path, root, knight):
    doc = load_gltf_json(path)
    out = ["", "### %s" % rel(path, root), ""]
    skins = doc.get("skins", [])
    out.append("- skins: %d; %s" % (len(skins), "; ".join(
        "'%s' %d joints" % (s.get("name", "skin%d" % i), len(s["joints"])) for i, s in enumerate(skins))))
    images = [(im.get("name") or im.get("uri") or "bufferView %s" % im.get("bufferView"), im.get("mimeType")) for im in doc.get("images", [])]
    out.append("- images: " + (", ".join("%s (%s)" % i for i in images) or "none"))
    users = {}
    for n in doc.get("nodes", []):
        if "mesh" in n:
            for prim in doc["meshes"][n["mesh"]]["primitives"]:
                users.setdefault(prim.get("material", -1), []).append(n.get("name"))
    mats = []
    for i, m in enumerate(doc.get("materials", [])):
        pbr = m.get("pbrMetallicRoughness", {})
        desc = "%s (%s%s; used by %s)" % (m.get("name", "?"), "baseColorTexture" if pbr.get("baseColorTexture") else "no texture",
                                         ", emissive %s" % [round(v, 3) for v in m["emissiveFactor"]] if m.get("emissiveFactor") else "",
                                         ", ".join(users.get(i, [])) or "nothing")
        mats.append(desc)
    out.append("- materials: " + ("; ".join(mats) or "none"))
    if skins:
        rig = joint_rig(doc)
        out.append("- joints (%d): %s" % (len(rig), ", ".join(r[0] for r in rig)))
        if knight is not None:
            out.append("- vs Knight.glb:")
            out.extend("  " + ln for ln in compare_rigs(knight["rig"], rig))
            if path.lower().endswith(".glb"):
                out.extend("  " + ln for ln in bind_pose_lines(knight, path))
    out.append("")
    out.append("Mesh nodes:")
    out.append("")
    out.append("| node | kind | parent node | parent bone | verts | tris |")
    out.append("|------|------|-------------|-------------|-------|------|")
    for name, kind, parent, bone, verts, tris in mesh_nodes(doc):
        out.append("| %s | %s | %s | %s | %d | %d |" % (name, kind, parent, bone, verts, tris))
    clips = [a.get("name", "anim%d" % i) for i, a in enumerate(doc.get("animations", []))]
    out.append("")
    out.append("Animation clips (%d): %s" % (len(clips), ", ".join(clips)))
    if knight is not None:
        common = [c for c in clips if c in knight["clips"]]
        only = [c for c in clips if c not in knight["clips"]]
        knight_only = [c for c in knight["clips"] if c not in clips]
        out.append("")
        out.append("- clips also in Knight.glb: %d" % len(common))
        out.append("- clips only here (%d): %s" % (len(only), ", ".join(only) or "none"))
        out.append("- Knight clips missing here (%d): %s" % (len(knight_only), ", ".join(knight_only) or "none"))
    return out, doc


def skeletons_section(root, knight_path):
    pack = os.path.join(root, "skeletons", "extracted")
    kdoc = load_gltf_json(knight_path)
    knight = {"rig": joint_rig(kdoc), "clips": [a.get("name") for a in kdoc.get("animations", [])],
              "ibm": inverse_bind_matrices(kdoc, load_glb_bin(knight_path)), "rot": rest_rotations(kdoc),
              "clip_table": clip_table(kdoc)}
    out = ["", "## skeletons (KayKit Skeletons 1.0)", ""]
    out.append("Reference: Knight.glb (Adventurers 1.0): %d joints, %d clips." % (len(knight["rig"]), len(knight["clips"])))
    out.append("Knight joints: " + ", ".join(r[0] for r in knight["rig"]))
    files = walk_files(pack)
    glbs = [f for f in files if f.lower().endswith(".glb")]
    out.append("")
    out.append("Character GLB files: " + ", ".join("%s (%d B)" % (rel(g, pack), os.path.getsize(g)) for g in glbs))
    for g in glbs:
        sec, _doc = glb_section(g, pack, knight)
        out.extend(sec)
    props = [f for f in files if f.lower().endswith(".gltf")]
    out.append("")
    out.append("### Props (Assets)")
    out.append("")
    for p in props:
        doc = load_gltf_json(p)
        uris = [im.get("uri") for im in doc.get("images", [])]
        bufs = [b.get("uri") for b in doc.get("buffers", [])]
        out.append("- %s: meshes %s, images %s, buffers %s" % (rel(p, pack), [m.get("name") for m in doc.get("meshes", [])], uris, bufs))
    out.append("")
    out.append("Textures: " + ", ".join(rel(f, pack) for f in files if f.lower().endswith(TEXTURE_EXTENSIONS)))
    out.append("Other files: " + ", ".join(rel(f, pack) for f in files if not f.lower().endswith(MODEL_EXTENSIONS + TEXTURE_EXTENSIONS + (".bin",))))
    return out


# ---------------------------------------------------------------- kits
def model_stem(path):
    n = os.path.basename(path)
    for ext in (".gltf.glb", ".glb", ".gltf", ".obj", ".fbx"):
        if n.lower().endswith(ext):
            return n[:-len(ext)]
    return n


def category_of(stem):
    return re.split(r"[_.\-]", stem)[0].lower()


def kit_section(root, name, title):
    pack = os.path.join(root, name, "extracted")
    files = walk_files(pack)
    out = ["", "## %s (%s)" % (name, title), ""]
    models = [f for f in files if f.lower().endswith(MODEL_EXTENSIONS)]
    by_folder = {}
    for m in models:
        by_folder.setdefault(rel(os.path.dirname(m), pack), []).append(m)
    for folder, ms in sorted(by_folder.items()):
        exts = sorted({os.path.basename(m)[len(model_stem(m)):] for m in ms})
        out.append("- %s: %d model files (%s)" % (folder, len(ms), ", ".join(exts)))
    stems = {}
    for m in models:
        stems.setdefault(model_stem(m), set()).add(rel(os.path.dirname(m), pack).split("/")[-1])
    folders = sorted({f for v in stems.values() for f in v})
    for folder in folders:
        missing = sorted((n for n, v in stems.items() if folder not in v), key=str.lower)
        out.append("- names without a %s file (%d): %s" % (folder, len(missing), ", ".join(missing) or "none"))
    multi = []
    for m in models:
        if m.lower().endswith((".gltf", ".glb")):
            doc = load_gltf_json(m)
            parts = [x.get("name") for x in doc.get("nodes", []) if "mesh" in x]
            if len(parts) > 1 or doc.get("animations"):
                multi.append("%s [%s]%s" % (os.path.basename(m), ", ".join(parts),
                             " anims %s" % [a.get("name") for a in doc["animations"]] if doc.get("animations") else ""))
    out.append("- multi-part glTF files (separate mesh nodes; the OBJ folder has these parts as own files): " + ("; ".join(multi) or "none"))
    plain_glb = sorted(os.path.basename(m) for m in models if m.lower().endswith(".glb") and not m.lower().endswith(".gltf.glb"))
    if plain_glb:
        out.append("- files named *.glb (not *.gltf.glb): " + ", ".join(plain_glb))
    cats = {}
    for s in sorted(stems, key=str.lower):
        cats.setdefault(category_of(s), []).append(s)
    out.append("")
    out.append("%d distinct model names in %d categories (first name token):" % (len(stems), len(cats)))
    out.append("")
    for c in sorted(cats):
        out.append("- %s (%d): %s" % (c, len(cats[c]), ", ".join(cats[c])))
    out.append("")
    tex = [rel(f, pack) for f in files if f.lower().endswith(TEXTURE_EXTENSIONS)]
    out.append("Texture files: " + ", ".join("%s (%d B%s)" % (t, os.path.getsize(os.path.join(pack, t)), png_size_text(os.path.join(pack, t))) for t in tex))
    sample = next((m for m in models if m.lower().endswith(".gltf") and not m.lower().endswith(".gltf.glb")), None)
    if sample:
        doc = load_gltf_json(sample)
        out.append("Sample %s: images %s, buffers %s, materials %s" % (rel(sample, pack), [i.get("uri") for i in doc.get("images", [])],
                   [b.get("uri") for b in doc.get("buffers", [])], [m.get("name") for m in doc.get("materials", [])]))
    sample_glb = next((m for m in models if m.lower().endswith(".glb")), None)
    if sample_glb:
        doc = load_gltf_json(sample_glb)
        out.append("Sample %s: images %s, materials %s" % (rel(sample_glb, pack), [(i.get("uri"), i.get("bufferView")) for i in doc.get("images", [])],
                   [m.get("name") for m in doc.get("materials", [])]))
    others = [rel(f, pack) for f in files if not f.lower().endswith(MODEL_EXTENSIONS + TEXTURE_EXTENSIONS + (".bin", ".mtl"))]
    out.append("Other files: " + (", ".join(others) or "none"))
    return out


def png_size_text(path):
    if not path.lower().endswith(".png"):
        return ""
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return ", not a PNG"
    w, h = struct.unpack(">II", head[16:24])
    return ", %dx%d" % (w, h)


# ---------------------------------------------------------------- audio
def wav_info(path):
    try:
        with wave.open(path, "rb") as w:
            rate, ch, frames, width = w.getframerate(), w.getnchannels(), w.getnframes(), w.getsampwidth()
            return frames / rate, rate, ch, "WAV PCM %d-bit" % (width * 8)
    except Exception:
        pass
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError("not RIFF/WAVE")
    pos, fmt, data_size = 12, None, None
    while pos + 8 <= len(data):
        cid, size = data[pos:pos + 4], struct.unpack("<I", data[pos + 4:pos + 8])[0]
        if cid == b"fmt ":
            fmt = struct.unpack("<HHIIHH", data[pos + 8:pos + 24])
        elif cid == b"data":
            data_size = min(size, len(data) - pos - 8)
        pos += 8 + size + (size & 1)
    tag, ch, rate, _bps, align, bits = fmt
    kind = {1: "PCM", 3: "float", 0xFFFE: "extensible"}.get(tag, "tag %d" % tag)
    return data_size / (align * rate), rate, ch, "WAV %s %d-bit" % (kind, bits)


def ogg_info(path):
    with open(path, "rb") as f:
        head = f.read(4096)
        f.seek(0, 2)
        size = f.tell()
        f.seek(max(0, size - OGG_TAIL_BYTES))
        tail = f.read()
    if head[:4] != b"OggS":
        raise ValueError("not Ogg")
    seg_count = head[26]
    body = head[27 + seg_count:]
    last = tail.rfind(b"OggS")
    granule = struct.unpack("<q", tail[last + 6:last + 14])[0]
    if body[:7] == b"\x01vorbis":
        ch, rate = body[11], struct.unpack("<I", body[12:16])[0]
        return granule / rate, rate, ch, "Ogg Vorbis"
    if body[:8] == b"OpusHead":
        ch, pre_skip, rate = body[9], struct.unpack("<H", body[10:12])[0], struct.unpack("<I", body[12:16])[0]
        return (granule - pre_skip) / 48000.0, rate, ch, "Ogg Opus"
    raise ValueError("unknown Ogg codec")


def flac_info(path):
    with open(path, "rb") as f:
        head = f.read(42)
    if head[:4] != b"fLaC":
        raise ValueError("not FLAC")
    si = head[8:26]
    bits = int.from_bytes(si[10:18], "big")
    rate = bits >> 44
    ch = ((bits >> 41) & 7) + 1
    total = bits & ((1 << 36) - 1)
    return total / rate, rate, ch, "FLAC"


def mp3_info(path):
    with open(path, "rb") as f:
        data = f.read()
    pos = 0
    if data[:3] == b"ID3":
        pos = 10 + ((data[6] << 21) | (data[7] << 14) | (data[8] << 7) | data[9])
    while pos + 4 <= len(data) and not (data[pos] == 0xFF and (data[pos + 1] & 0xE0) == 0xE0):
        pos += 1
    h = struct.unpack(">I", data[pos:pos + 4])[0]
    version = (h >> 19) & 3
    rate = MP3_RATES[version][(h >> 10) & 3]
    bitrate = MP3_BITRATES[1 if version == 3 else 2][(h >> 12) & 15] * 1000
    ch = 1 if ((h >> 6) & 3) == 3 else 2
    xing = max(data.find(b"Xing", pos, pos + 64), data.find(b"Info", pos, pos + 64))
    samples_per_frame = 1152 if version == 3 else 576
    if xing > 0 and struct.unpack(">I", data[xing + 4:xing + 8])[0] & 1:
        frames = struct.unpack(">I", data[xing + 8:xing + 12])[0]
        return frames * samples_per_frame / rate, rate, ch, "MP3 (Xing)"
    return (len(data) - pos) * 8 / bitrate, rate, ch, "MP3 CBR est. %d kbps" % (bitrate // 1000)


def aiff_info(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"FORM" or data[8:12] not in (b"AIFF", b"AIFC"):
        raise ValueError("not AIFF")
    pos = 12
    while pos + 8 <= len(data):
        cid, size = data[pos:pos + 4], struct.unpack(">I", data[pos + 4:pos + 8])[0]
        if cid == b"COMM":
            ch, frames, bits = struct.unpack(">hIh", data[pos + 8:pos + 16])
            exponent, mantissa = struct.unpack(">HQ", data[pos + 16:pos + 26])
            rate = mantissa * 2.0 ** ((exponent & 0x7FFF) - 16383 - 63)
            return frames / rate, int(round(rate)), ch, "%s %d-bit" % (data[8:12].decode("ascii"), bits)
        pos += 8 + size + (size & 1)
    raise ValueError("no COMM chunk")


AUDIO_READERS = {".wav": wav_info, ".ogg": ogg_info, ".flac": flac_info, ".mp3": mp3_info, ".aif": aiff_info, ".aiff": aiff_info}


def audio_section(root):
    out = ["", "## Audio inventory", ""]
    summary = ["| pack | files | total s | sample rates | channels | formats |", "|------|-------|---------|--------------|----------|---------|"]
    detail = []
    for name in AUDIO_PACKS:
        pack = os.path.join(root, name)
        if not os.path.isdir(pack):
            summary.append("| %s | MISSING | | | | |" % name)
            continue
        files = [f for f in walk_files(pack) if f.lower().endswith(AUDIO_EXTENSIONS)]
        rows, rates, chans, fmts, total = [], set(), set(), set(), 0.0
        for f in files:
            try:
                dur, rate, ch, fmt = AUDIO_READERS[os.path.splitext(f)[1].lower()](f)
                rows.append("| %s | %.2f | %d | %d | %s |" % (ascii_text(rel(f, pack)), dur, rate, ch, fmt))
                rates.add(rate)
                chans.add(ch)
                fmts.add(fmt.split(" est.")[0])
                total += dur
            except Exception as err:
                rows.append("| %s | ? | ? | ? | parse error: %s |" % (ascii_text(rel(f, pack)), ascii_text(err)))
        summary.append("| %s | %d | %.1f | %s | %s | %s |" % (name, len(files), total, ", ".join(map(str, sorted(rates))),
                       ", ".join(map(str, sorted(chans))), ", ".join(sorted(fmts))))
        detail += ["", "### %s (%d files)" % (name, len(files)), "", "| file | seconds | rate | ch | format |",
                   "|------|---------|------|----|--------|"] + rows
    return out + summary + detail


# ---------------------------------------------------------------- vfx, fonts
def vfx_section(root):
    out = ["", "## VFX textures", ""]
    for name in VFX_PACKS:
        pack = os.path.join(root, name, "extracted")
        if not os.path.isdir(pack):
            out.append("- %s: MISSING" % name)
            continue
        files = walk_files(pack)
        images = [f for f in files if f.lower().endswith(TEXTURE_EXTENSIONS)]
        by_dir = {}
        for f in images:
            by_dir.setdefault(rel(os.path.dirname(f), pack), []).append(f)
        out.append("### %s (%d images)" % (name, len(images)))
        out.append("")
        for d, fs in sorted(by_dir.items()):
            groups = {}
            for f in fs:
                stem = os.path.splitext(os.path.basename(f))[0]
                groups.setdefault(re.sub(r"[_\-]?\d+$", "", stem), []).append(f)
            dims = sorted({png_size_text(f).lstrip(", ") for f in fs})
            out.append("- %s: %d files, sizes %s" % (d, len(fs), ", ".join(dims)))
            out.append("  - " + ", ".join("%s x%d" % (g, len(v)) for g, v in sorted(groups.items())))
        others = [rel(f, pack) for f in files if f not in images]
        out.append("- other files: " + (", ".join(others) or "none"))
        out.append("")
    return out


def fonts_section(root):
    pack = os.path.join(root, "fonts")
    out = ["", "## fonts", ""]
    for f in walk_files(pack):
        out.append("- %s (%d B)" % (rel(f, pack), os.path.getsize(f)))
    return out


PACK_ORDER = ["skeletons", "halloween_bits", "dungeon_remastered", "sfx_80_rpg", "sfx_rpg_sound_pack", "sfx_swishes",
              "sfx_bone_breaks", "sfx_bones_rattle", "sfx_gem", "sfx_soundpack01", "kenney_impact_sounds",
              "kenney_interface_sounds", "kenney_scifi_sounds", "kenney_music_jingles", "kenney_rpg_audio",
              "kenney_particle_pack", "kenney_smoke_particles", "music_battle", "music_boss", "music_menu", "fonts"]
UNUSUAL_SUFFIXES = (".url", ".unitypackage", ".ini", ".ds_store")


def read_source(pack_dir):
    out = {}
    path = os.path.join(pack_dir, "SOURCE_LICENSE.txt")
    if not os.path.isfile(path):
        return out
    for line in open(path, encoding="ascii", errors="replace"):
        key, sep, value = line.rstrip().partition(": ")
        if sep:
            out[key] = value
    return out


def status_section(root):
    out = ["", "## Download status", "",
           "Each pack folder holds the original archive/file, extracted/ (zips only) and SOURCE_LICENSE.txt",
           "(download URL, sha256, source page, page license line).", "",
           "| pack | status | bytes | size check | page license line | license file in pack |",
           "|------|--------|-------|------------|-------------------|----------------------|"]
    unusual = []
    for name in PACK_ORDER:
        pack_dir = os.path.join(root, name)
        src = read_source(pack_dir)
        if not src:
            out.append("| %s | MISSING | | | | |" % name)
            continue
        lic_files = [os.path.basename(f) for f in src.get("license_files", "").split(", ") if f]
        if src.get("readme_license_lines"):
            src["page_license_line"] += "; repo README: " + src["readme_license_lines"].split(" | ")[-1]
        out.append("| %s | ok | %s | %s (expected %s) | %s | %s |" % (
            name, src.get("bytes"), "ok" if src.get("size_ok") == "True" else "MISMATCH", src.get("expected", "per file"),
            src.get("page_license_line", "").split(" || ")[0][:200], ", ".join(lic_files) or "none (license only on the page)"))
        for base, dirs, names in os.walk(pack_dir):
            for n in names:
                rp = rel(os.path.join(base, n), pack_dir)
                if n.lower().endswith(UNUSUAL_SUFFIXES) or "__MACOSX" in rp or n.startswith("._"):
                    unusual.append(name + "/" + rp)
    out += ["", "Non-asset files (not opened or run; ignore when importing): %d, e.g. %s" % (
        len(unusual), ", ".join(sorted(u for u in unusual if "__MACOSX" not in u and ".DS_Store" not in u)))]
    out.append("Plus macOS metadata (__MACOSX/, ._*, .DS_Store) in sfx_rpg_sound_pack, sfx_swishes, sfx_soundpack01.")
    return out


def main():
    root, knight_path, out_path = sys.argv[1], sys.argv[2], sys.argv[3]
    lines = ["# Survivors downloads inventory", "",
             "Root: %s" % root, "Generated by tools/download/inventory.py (parse only; nothing in the packs was run)."]
    lines += status_section(root)
    lines += skeletons_section(root, knight_path)
    lines += kit_section(root, "halloween_bits", "KayKit Halloween Bits 1.0")
    lines += kit_section(root, "dungeon_remastered", "KayKit Dungeon Remastered 1.0")
    lines += audio_section(root)
    lines += vfx_section(root)
    lines += fonts_section(root)
    text = ascii_text("\n".join(lines) + "\n")
    with open(out_path, "w", encoding="ascii", newline="\n") as f:
        f.write(text)
    print("wrote", out_path, len(text), "chars")


if __name__ == "__main__":
    main()
