#!/usr/bin/env python3
"""
kaykit_merge.py - preprocess a KayKit character GLB for an instanced crowd renderer.

Every selected mesh of a skinned character (the skinned body parts plus the rigid
props parented to bones) is merged into ONE triangle primitive with ONE material,
bound to the original skin. Rigid props are baked into the bind pose and weighted
100 percent to their nearest ancestor joint, so plain linear blend skinning
reproduces their motion exactly. External prop files (--attach) are merged the same
way onto a named skin joint.

Materials: every part must use one textured material (attached props: the same image
content as the character) or an emissive-only material without textures (KayKit "Glow"
eyes). Emissive-only parts are folded into the one output material: a strip of
EMISSIVE_STRIP_ROWS rows per emissive material is appended below the colour atlas, every
existing V is scaled by old_height / new_height so old texels keep their exact pixels,
the emissive parts' UVs point at the centre of their strip, and a mask of the atlas size
(white in the strips, black elsewhere) becomes the emissiveTexture with emissiveFactor
set to the emissive colour. The strip rows are printed.

Output: OUT_DIR/NAME.gltf + NAME.bin + NAME.png (the colour texture as an external file),
        NAME_emissive.png when emissive parts were folded, preview.png with --preview.

Usage:
  python kaykit_merge.py IN.glb OUT_DIR --name NAME [--keep NODE ...] [--drop NODE ...]
                         [--attach JOINT=PROP.gltf[@TX,TY,TZ[,QX,QY,QZ,QW]] ...]
                         [--animations all|none|NAME,NAME,...] [--verify]
                         [--preview] [--preview-clip NAME[@SECONDS]]
  python kaykit_merge.py IN.glb --list

  --keep NODE ...   keep only these rigid prop nodes (all rigid props when omitted);
                    skinned body parts are always kept unless dropped
  --drop NODE ...   drop these mesh nodes (skinned or rigid)
  --attach SPEC ... merge the mesh nodes of an external prop glTF onto skin joint JOINT as
                    a rigid part (weight 1 on JOINT), placed at JOINT's rest (bind) global
                    times the optional local offset (translation, then x,y,z,w rotation;
                    identity when omitted) times the prop's own node transforms; the
                    prop's bounds are printed. The KayKit Adventurers GLBs (same 41-joint
                    rig, props authored in the same frame) hold their hand props at:
                      right-hand sword / axe   @0,0.0333,0,0,-1,0,0
                      right-hand staff / wand  @0,0,0,0,-1,0,0
                      right-hand crossbow      @-0.1046,-0.0104,0,0,0.7071,0,0.7071
                      shield on handslot.l     @0,0.017,0.1559
  --animations      'all' (default), 'none', or a comma separated list of names
  --verify          after writing, CPU-skin the original (attached props on their animated
                    joint) and the merged file for Idle, Running_A and a 1H attack at 4
                    times each and compare positions; with folded emissive parts also check
                    the written atlas pixels and every UV
  --preview         write OUT_DIR/preview.png: front and right-side orthographic views of
                    the rest pose and of one clip frame, coloured by part
  --preview-clip    clip of the preview's second row and optional time in seconds
                    (default Idle at mid clip)
  --list            print the mesh node list and exit

Requires Python 3 and numpy; Pillow too when emissive parts are folded or with --preview.
"""

import argparse
import base64
import copy
import dataclasses
import hashlib
import io
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import urllib.parse

import numpy as np

GLB_MAGIC = b"glTF"
GLB_VERSION = 2
GLB_HEADER_SIZE = 12
GLB_CHUNK_HEADER_SIZE = 8
GLB_CHUNK_JSON = 0x4E4F534A
GLB_CHUNK_BIN = 0x004E4942

COMPONENT_BYTE = 5120
COMPONENT_UNSIGNED_BYTE = 5121
COMPONENT_SHORT = 5122
COMPONENT_UNSIGNED_SHORT = 5123
COMPONENT_UNSIGNED_INT = 5125
COMPONENT_FLOAT = 5126

COMPONENT_DTYPES = {
    COMPONENT_BYTE: np.dtype("<i1"),
    COMPONENT_UNSIGNED_BYTE: np.dtype("<u1"),
    COMPONENT_SHORT: np.dtype("<i2"),
    COMPONENT_UNSIGNED_SHORT: np.dtype("<u2"),
    COMPONENT_UNSIGNED_INT: np.dtype("<u4"),
    COMPONENT_FLOAT: np.dtype("<f4"),
}
COMPONENT_NAMES = {
    COMPONENT_UNSIGNED_BYTE: "uint8",
    COMPONENT_UNSIGNED_SHORT: "uint16",
    COMPONENT_UNSIGNED_INT: "uint32",
}
NORMALIZATION_DIVISORS = {
    COMPONENT_BYTE: 127.0,
    COMPONENT_UNSIGNED_BYTE: 255.0,
    COMPONENT_SHORT: 32767.0,
    COMPONENT_UNSIGNED_SHORT: 65535.0,
}
TYPE_COMPONENT_COUNTS = {
    "SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16,
}
INDEX_COMPONENT_TYPES = (COMPONENT_UNSIGNED_BYTE, COMPONENT_UNSIGNED_SHORT, COMPONENT_UNSIGNED_INT)
JOINT_COMPONENT_TYPES = (COMPONENT_UNSIGNED_BYTE, COMPONENT_UNSIGNED_SHORT)

TARGET_ARRAY_BUFFER = 34962
TARGET_ELEMENT_ARRAY_BUFFER = 34963
MODE_TRIANGLES = 4
BUFFER_ALIGNMENT = 4
MIN_BYTE_STRIDE = 4
MAX_BYTE_STRIDE = 252
INFLUENCES_PER_SET = 4
MAX_UINT8_JOINTS = 256
MAX_UINT16_INDEXED_VERTICES = 65535

ANIMATED_PATHS = ("translation", "rotation", "scale")
PATH_TYPES = {"translation": "VEC3", "rotation": "VEC4", "scale": "VEC3"}
INTERPOLATIONS = ("LINEAR", "STEP", "CUBICSPLINE")
CUBIC_VALUES_PER_KEY = 3
NODE_KEYS_KEPT = ("name", "translation", "rotation", "scale", "matrix", "extras")
SUPPORTED_ATTRIBUTES = ("POSITION", "NORMAL", "TEXCOORD_0", "JOINTS_0", "WEIGHTS_0")

IBM_TOLERANCE = 1e-4
SKIN_IBM_MERGE_TOLERANCE = 1e-4
WEIGHT_SUM_TOLERANCE = 1e-3
UNIT_LENGTH_TOLERANCE = 1e-3
MIN_VECTOR_LENGTH = 1e-12
SLERP_LINEAR_THRESHOLD = 0.9995
VERIFY_RELATIVE_TOLERANCE = 1e-3
VERIFY_TIMES_PER_ANIMATION = 4
VERIFY_ANIMATIONS = ("Idle", "Running_A")
VERIFY_ATTACK_PREFIX = "1H_"
VERIFY_ATTACK_WORD = "Attack"
UP_AXIS = 1

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
JPEG_SIGNATURE = b"\xff\xd8\xff"
MIME_EXTENSIONS = {"image/png": ".png", "image/jpeg": ".jpg"}
MIME_SIGNATURES = {"image/png": PNG_SIGNATURE, "image/jpeg": JPEG_SIGNATURE}
GENERATOR = "kaykit_merge.py"
KHRONOS_VALIDATOR = "gltf_validator"
MAX_PRINTED_PROBLEMS = 50

ATTACH_TRANSLATION_COUNT = 3
ATTACH_TRANSFORM_COUNT = 7
EMISSIVE_STRIP_ROWS = 8
STRIP_CENTER_U = 0.5
EMISSIVE_MASK_SUFFIX = "_emissive"
MAX_CHANNEL_VALUE = 255
SRGB_LINEAR_LIMIT = 0.0031308
SRGB_LINEAR_SLOPE = 12.92
SRGB_GAMMA = 2.4
SRGB_SCALE = 1.055
SRGB_OFFSET = 0.055
UV_CHECK_TOLERANCE_TEXELS = 1e-2

PREVIEW_FILE = "preview.png"
PREVIEW_DEFAULT_CLIP = "Idle"
PREVIEW_PANEL_WIDTH = 400
PREVIEW_PANEL_HEIGHT = 480
PREVIEW_MARGIN_FRACTION = 0.06
PREVIEW_TITLE_HEIGHT = 22
PREVIEW_LEGEND_LINE_HEIGHT = 20
PREVIEW_LEGEND_COLUMNS = 2
PREVIEW_SWATCH_SIZE = 12
PREVIEW_FONT_SIZE = 14
PREVIEW_MARKER_SIZE = 6
PREVIEW_AMBIENT = 0.35
PREVIEW_BACKGROUND = (250, 250, 250)
PREVIEW_INK = (20, 20, 20)
PREVIEW_PALETTES = {
    "skinned": [(120, 144, 170), (150, 170, 120), (170, 150, 120), (130, 130, 175), (110, 160, 160),
                (175, 130, 150), (160, 160, 100), (140, 140, 140), (100, 130, 110), (180, 160, 170)],
    "rigid": [(60, 120, 60), (120, 70, 150), (40, 110, 140)],
    "attached": [(215, 40, 40), (245, 140, 20), (200, 40, 170), (240, 200, 0)],
}


def warn(message):
    """Print a warning line to stdout so it stays in order with the report."""
    print("WARNING: " + message)


def import_pillow():
    """Return (Image, ImageDraw, ImageFont); Pillow is only needed for folding and previews."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        raise SystemExit("error: Pillow is required to fold emissive materials and for --preview")
    return Image, ImageDraw, ImageFont


# ---------------------------------------------------------------------------
# glTF / GLB reading
# ---------------------------------------------------------------------------

def parse_glb(data):
    """Split a GLB container into its JSON document and its optional BIN chunk."""
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != GLB_MAGIC or version != GLB_VERSION:
        raise ValueError("not a glTF 2.0 binary file")
    end = min(length, len(data))
    offset = GLB_HEADER_SIZE
    document = None
    binary = None
    while offset + GLB_CHUNK_HEADER_SIZE <= end:
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        start = offset + GLB_CHUNK_HEADER_SIZE
        chunk = data[start:start + chunk_length]
        if chunk_type == GLB_CHUNK_JSON and document is None:
            document = json.loads(chunk.decode("utf-8"))
        elif chunk_type == GLB_CHUNK_BIN and binary is None:
            binary = bytes(chunk)
        offset = start + chunk_length
    if document is None:
        raise ValueError("GLB has no JSON chunk")
    return document, binary


def compute_parents(nodes):
    """Return the parent index of every node (-1 for roots); a node may have one parent only."""
    parents = [-1] * len(nodes)
    for index, node in enumerate(nodes):
        for child in node.get("children", []):
            if parents[child] != -1:
                raise ValueError("node %d has more than one parent" % child)
            parents[child] = index
    return parents


def compute_topological_order(nodes, parents):
    """Return node indices so that every parent precedes its children."""
    order = []
    stack = [index for index, parent in enumerate(parents) if parent == -1][::-1]
    while stack:
        index = stack.pop()
        order.append(index)
        stack.extend(reversed(nodes[index].get("children", [])))
    if len(order) != len(nodes):
        raise ValueError("node hierarchy contains a cycle")
    return order


class GltfDocument:
    """A parsed glTF 2.0 asset (.glb or .gltf) with its binary buffers loaded."""

    def __init__(self, path):
        self.path = os.path.abspath(path)
        self.base_dir = os.path.dirname(self.path)
        with open(self.path, "rb") as handle:
            data = handle.read()
        glb_binary = None
        if data[:4] == GLB_MAGIC:
            self.json, glb_binary = parse_glb(data)
        else:
            self.json = json.loads(data.decode("utf-8"))
        self.buffers = [self._load_buffer(buffer, glb_binary) for buffer in self.json.get("buffers", [])]
        self.nodes = self.json.get("nodes", [])
        self.parents = compute_parents(self.nodes)
        self.order = compute_topological_order(self.nodes, self.parents)

    def _load_buffer(self, buffer, glb_binary):
        if "uri" in buffer:
            return self.load_uri(buffer["uri"])
        if glb_binary is None:
            raise ValueError("buffer without uri and no GLB BIN chunk")
        return glb_binary

    def load_uri(self, uri):
        """Load a data: URI or a file relative to the document."""
        if uri.startswith("data:"):
            return base64.b64decode(uri.split(",", 1)[1])
        with open(os.path.join(self.base_dir, urllib.parse.unquote(uri)), "rb") as handle:
            return handle.read()

    def node_name(self, index):
        return self.nodes[index].get("name", "node%d" % index) if index >= 0 else "-"

    def _read_view_elements(self, view_index, byte_offset, dtype, component_count, count, use_stride):
        view = self.json["bufferViews"][view_index]
        buffer = self.buffers[view["buffer"]]
        element_size = dtype.itemsize * component_count
        stride = (view.get("byteStride") if use_stride else None) or element_size
        if count == 0:
            return np.zeros((0, component_count), dtype)
        span = stride * (count - 1) + element_size
        start = view.get("byteOffset", 0) + byte_offset
        if byte_offset + span > view["byteLength"] or start + span > len(buffer):
            raise ValueError("accessor data exceeds buffer view %d" % view_index)
        raw = np.frombuffer(buffer, dtype=np.uint8, count=span, offset=start)
        rows = np.lib.stride_tricks.as_strided(raw, shape=(count, element_size), strides=(stride, 1))
        return np.ascontiguousarray(rows).view(dtype).reshape(count, component_count)

    def read_accessor(self, index):
        """Return accessor data as a (count, components) array of its component type."""
        accessor = self.json["accessors"][index]
        dtype = COMPONENT_DTYPES[accessor["componentType"]]
        component_count = TYPE_COMPONENT_COUNTS[accessor["type"]]
        if accessor["type"] in ("MAT2", "MAT3") and dtype.itemsize < BUFFER_ALIGNMENT:
            raise NotImplementedError("padded matrix accessors are not supported")
        count = accessor["count"]
        if "bufferView" in accessor:
            values = self._read_view_elements(accessor["bufferView"], accessor.get("byteOffset", 0),
                                              dtype, component_count, count, True)
        else:
            values = np.zeros((count, component_count), dtype)
        sparse = accessor.get("sparse")
        if sparse:
            indices_info = sparse["indices"]
            values_info = sparse["values"]
            sparse_indices = self._read_view_elements(
                indices_info["bufferView"], indices_info.get("byteOffset", 0),
                COMPONENT_DTYPES[indices_info["componentType"]], 1, sparse["count"], False)[:, 0]
            sparse_values = self._read_view_elements(
                values_info["bufferView"], values_info.get("byteOffset", 0),
                dtype, component_count, sparse["count"], False)
            values = values.copy()
            values[sparse_indices.astype(np.int64)] = sparse_values
        return values

    def read_accessor_float(self, index):
        """Return accessor data as float64, applying glTF normalization rules."""
        accessor = self.json["accessors"][index]
        values = self.read_accessor(index)
        if accessor.get("normalized", False):
            divisor = NORMALIZATION_DIVISORS[accessor["componentType"]]
            return np.maximum(values.astype(np.float64) / divisor, -1.0)
        return values.astype(np.float64)

    def read_inverse_bind_matrices(self, skin):
        """Return the (joints, 4, 4) row-major inverse bind matrices of a skin."""
        joint_count = len(skin["joints"])
        if "inverseBindMatrices" not in skin:
            return np.tile(np.eye(4), (joint_count, 1, 1))
        columns = self.read_accessor_float(skin["inverseBindMatrices"])
        return columns.reshape(joint_count, 4, 4).transpose(0, 2, 1)

    def read_image(self, image_index):
        """Return (bytes, mime type) of an image stored in a buffer view or behind a uri."""
        image = self.json["images"][image_index]
        mime = image.get("mimeType")
        if "bufferView" in image:
            view = self.json["bufferViews"][image["bufferView"]]
            start = view.get("byteOffset", 0)
            data = self.buffers[view["buffer"]][start:start + view["byteLength"]]
        else:
            uri = image["uri"]
            if mime is None and uri.startswith("data:"):
                mime = uri[len("data:"):].split(";", 1)[0]
            data = self.load_uri(uri)
        if mime is None:
            mime = next((name for name, signature in MIME_SIGNATURES.items() if data.startswith(signature)), None)
        return bytes(data), mime


# ---------------------------------------------------------------------------
# Transform math and animation sampling
# ---------------------------------------------------------------------------

def quaternion_to_matrix(quaternion):
    """Return the 3x3 rotation matrix of a glTF (x, y, z, w) quaternion."""
    quaternion = np.asarray(quaternion, dtype=np.float64)
    length = np.linalg.norm(quaternion)
    if length < MIN_VECTOR_LENGTH:
        return np.eye(3)
    x, y, z, w = quaternion / length
    return np.array([
        [1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y - z * w), 2.0 * (x * z + y * w)],
        [2.0 * (x * y + z * w), 1.0 - 2.0 * (x * x + z * z), 2.0 * (y * z - x * w)],
        [2.0 * (x * z - y * w), 2.0 * (y * z + x * w), 1.0 - 2.0 * (x * x + y * y)],
    ])


def compose_trs(translation, rotation, scale):
    """Return the 4x4 matrix T * R * S."""
    matrix = np.eye(4)
    matrix[:3, :3] = quaternion_to_matrix(rotation) * np.asarray(scale, dtype=np.float64)[None, :]
    matrix[:3, 3] = translation
    return matrix


def node_local_matrix(node, override=None):
    """Return a node's local matrix; override maps animated paths to sampled values."""
    if "matrix" in node:
        return np.array(node["matrix"], dtype=np.float64).reshape(4, 4).T
    override = override or {}
    translation = override.get("translation", node.get("translation", [0.0, 0.0, 0.0]))
    rotation = override.get("rotation", node.get("rotation", [0.0, 0.0, 0.0, 1.0]))
    scale = override.get("scale", node.get("scale", [1.0, 1.0, 1.0]))
    return compose_trs(translation, rotation, scale)


def compute_global_matrices(document, overrides=None):
    """Return (nodes, 4, 4) global matrices; without overrides this is the rest pose as stored."""
    overrides = overrides or {}
    global_matrices = np.zeros((len(document.nodes), 4, 4))
    for index in document.order:
        local = node_local_matrix(document.nodes[index], overrides.get(index))
        parent = document.parents[index]
        global_matrices[index] = local if parent < 0 else global_matrices[parent] @ local
    return global_matrices


def slerp(start, end, amount):
    """Spherical linear interpolation of two unit quaternions along the shortest arc."""
    cosine = float(np.dot(start, end))
    if cosine < 0.0:
        end = -end
        cosine = -cosine
    if cosine > SLERP_LINEAR_THRESHOLD:
        result = start + amount * (end - start)
    else:
        angle = math.acos(cosine)
        result = (math.sin((1.0 - amount) * angle) * start + math.sin(amount * angle) * end) / math.sin(angle)
    return result / np.linalg.norm(result)


def normalize_if_rotation(value, is_rotation):
    return value / np.linalg.norm(value) if is_rotation else value


def sample_channel(times, values, interpolation, time, is_rotation):
    """Sample one glTF animation sampler at a time (clamped to its key range)."""
    key_count = len(times)
    if interpolation == "CUBICSPLINE":
        keyed = values.reshape(key_count, CUBIC_VALUES_PER_KEY, -1)
        in_tangents, points, out_tangents = keyed[:, 0], keyed[:, 1], keyed[:, 2]
    else:
        points = values
    if key_count == 1 or time <= times[0]:
        return normalize_if_rotation(points[0], is_rotation)
    if time >= times[-1]:
        return normalize_if_rotation(points[-1], is_rotation)
    key = int(np.searchsorted(times, time, side="right")) - 1
    delta = times[key + 1] - times[key]
    amount = (time - times[key]) / delta
    if interpolation == "STEP":
        return normalize_if_rotation(points[key], is_rotation)
    if interpolation == "CUBICSPLINE":
        amount2 = amount * amount
        amount3 = amount2 * amount
        value = ((2.0 * amount3 - 3.0 * amount2 + 1.0) * points[key]
                 + (amount3 - 2.0 * amount2 + amount) * delta * out_tangents[key]
                 + (-2.0 * amount3 + 3.0 * amount2) * points[key + 1]
                 + (amount3 - amount2) * delta * in_tangents[key + 1])
        return normalize_if_rotation(value, is_rotation)
    if is_rotation:
        return slerp(points[key], points[key + 1], amount)
    return points[key] + amount * (points[key + 1] - points[key])


class AnimationEvaluator:
    """Samples every TRS channel of one animation into per-node local overrides."""

    def __init__(self, document, animation_index):
        animation = document.json["animations"][animation_index]
        self.name = animation.get("name", "animation%d" % animation_index)
        self.channels = []
        start_times = []
        end_times = []
        for channel in animation["channels"]:
            target = channel["target"]
            if "node" not in target or target["path"] not in ANIMATED_PATHS:
                continue
            sampler = animation["samplers"][channel["sampler"]]
            times = document.read_accessor_float(sampler["input"])[:, 0]
            values = document.read_accessor_float(sampler["output"])
            interpolation = sampler.get("interpolation", "LINEAR")
            self.channels.append((target["node"], target["path"], times, values, interpolation))
            start_times.append(times[0])
            end_times.append(times[-1])
        self.start = float(min(start_times)) if start_times else 0.0
        self.end = float(max(end_times)) if end_times else 0.0

    def evaluate(self, time):
        overrides = {}
        for node, path, times, values, interpolation in self.channels:
            value = sample_channel(times, values, interpolation, time, path == "rotation")
            overrides.setdefault(node, {})[path] = value
        return overrides


# ---------------------------------------------------------------------------
# Mesh node inventory and selection
# ---------------------------------------------------------------------------

@dataclasses.dataclass
class MeshNodeInfo:
    node_index: int
    name: str
    is_skinned: bool
    parent_index: int
    attach_joint: int
    primitive_count: int
    vertex_count: int
    triangle_count: int
    is_selected: bool = True


def find_nearest_joint(parents, node_index, joint_nodes):
    """Return the nearest ancestor-or-self of a node that is in joint_nodes, or -1."""
    index = node_index
    while index != -1:
        if index in joint_nodes:
            return index
        index = parents[index]
    return -1


def count_primitive_elements(document, primitive):
    accessors = document.json["accessors"]
    vertex_count = accessors[primitive["attributes"]["POSITION"]]["count"]
    index_count = accessors[primitive["indices"]]["count"] if "indices" in primitive else vertex_count
    return vertex_count, index_count // 3


def collect_skinned_joint_nodes(document):
    """Return every joint node of the skins used by skinned mesh nodes."""
    skins = document.json.get("skins", [])
    used = {node["skin"] for node in document.nodes if "mesh" in node and "skin" in node}
    return {joint for skin_index in used for joint in skins[skin_index]["joints"]}


def collect_mesh_nodes(document):
    """Describe every node that carries a mesh: kind, parent, attach bone, sizes."""
    joint_nodes = collect_skinned_joint_nodes(document)
    infos = []
    for index, node in enumerate(document.nodes):
        if "mesh" not in node:
            continue
        primitives = document.json["meshes"][node["mesh"]]["primitives"]
        counts = [count_primitive_elements(document, primitive) for primitive in primitives]
        is_skinned = "skin" in node
        attach = -1 if is_skinned else find_nearest_joint(document.parents, index, joint_nodes)
        infos.append(MeshNodeInfo(index, document.node_name(index), is_skinned, document.parents[index], attach,
                                  len(primitives), sum(c[0] for c in counts), sum(c[1] for c in counts)))
    return infos


def apply_selection(infos, keep_names, drop_names):
    """Mark selected mesh nodes; skinned parts stay unless dropped, rigid props follow --keep."""
    known = {info.name for info in infos}
    unknown = sorted(name for name in set(keep_names) | set(drop_names) if name not in known)
    if unknown:
        raise SystemExit("error: unknown mesh node name(s): %s\nmesh nodes: %s"
                         % (", ".join(unknown), ", ".join(info.name for info in infos)))
    conflicting = sorted(set(keep_names) & set(drop_names))
    if conflicting:
        raise SystemExit("error: node(s) both kept and dropped: %s" % ", ".join(conflicting))
    for info in infos:
        if info.name in drop_names:
            info.is_selected = False
        elif info.is_skinned or not keep_names:
            info.is_selected = True
        else:
            info.is_selected = info.name in keep_names


def print_mesh_node_table(document, infos):
    skins = document.json.get("skins", [])
    print("Mesh nodes in %s (%d skin(s): %s):" % (os.path.basename(document.path), len(skins),
          ", ".join("'%s' %d joints" % (skin.get("name", "skin%d" % i), len(skin["joints"]))
                    for i, skin in enumerate(skins))))
    rows = [("node", "name", "kind", "parent node", "parent bone", "prims", "verts", "tris", "selected")]
    for info in infos:
        bone = ("skin %d" % document.nodes[info.node_index]["skin"]) if info.is_skinned \
            else document.node_name(info.attach_joint)
        rows.append((str(info.node_index), info.name, "skinned" if info.is_skinned else "rigid",
                     document.node_name(info.parent_index), bone, str(info.primitive_count),
                     str(info.vertex_count), str(info.triangle_count), "yes" if info.is_selected else "-"))
    widths = [max(len(row[column]) for row in rows) for column in range(len(rows[0]))]
    for row in rows:
        print("  " + "  ".join(cell.ljust(width) for cell, width in zip(row, widths)).rstrip())


def resolve_animations(document, specification):
    """Return the animation indices selected by 'all', 'none' or a comma separated name list."""
    animations = document.json.get("animations", [])
    if specification == "all":
        return list(range(len(animations)))
    if specification == "none":
        return []
    by_name = {animation.get("name", "animation%d" % i): i for i, animation in enumerate(animations)}
    requested = [name.strip() for name in specification.split(",") if name.strip()]
    missing = [name for name in requested if name not in by_name]
    if missing:
        raise SystemExit("error: unknown animation(s): %s\navailable: %s"
                         % (", ".join(missing), ", ".join(by_name)))
    return list(dict.fromkeys(by_name[name] for name in requested))


# ---------------------------------------------------------------------------
# External props (--attach)
# ---------------------------------------------------------------------------

@dataclasses.dataclass
class AttachedProp:
    """An external prop glTF merged rigidly onto one skin joint at a local offset."""
    joint_name: str
    path: str
    translation: list
    rotation: list
    name: str
    document: object = None
    joint_node: int = -1
    mesh_nodes: list = dataclasses.field(default_factory=list)

    @property
    def offset(self):
        return compose_trs(self.translation, self.rotation, [1.0, 1.0, 1.0])


def parse_attach_specification(text):
    """Parse JOINT=PATH[@TX,TY,TZ[,QX,QY,QZ,QW]] into an AttachedProp with a 4x4 local offset."""
    joint_name, separator, target = text.partition("=")
    path, at_sign, offset_text = target.rpartition("@")
    if not at_sign:
        path, offset_text = target, ""
    if not separator or not joint_name or not path:
        raise SystemExit("error: --attach expects JOINT=PATH.gltf[@TX,TY,TZ[,QX,QY,QZ,QW]], got '%s'" % text)
    try:
        values = [float(value) for value in offset_text.split(",")] if offset_text else []
    except ValueError:
        values = None
    if values is None or len(values) not in (0, ATTACH_TRANSLATION_COUNT, ATTACH_TRANSFORM_COUNT):
        raise SystemExit("error: --attach offset must be 3 numbers (translation) or 7 (translation and an x,y,z,w"
                         " rotation), got '%s'" % offset_text)
    translation = values[:ATTACH_TRANSLATION_COUNT] or [0.0, 0.0, 0.0]
    rotation = values[ATTACH_TRANSLATION_COUNT:] or [0.0, 0.0, 0.0, 1.0]
    if abs(np.linalg.norm(rotation) - 1.0) > UNIT_LENGTH_TOLERANCE:
        raise SystemExit("error: --attach rotation %s is not a unit quaternion" % rotation)
    return AttachedProp(joint_name, path, translation, rotation, os.path.splitext(os.path.basename(path))[0])


def collect_scene_mesh_nodes(document):
    """Return the mesh nodes reachable from the default scene (from every root without scenes)."""
    scenes = document.json.get("scenes", [])
    if scenes:
        roots = scenes[document.json.get("scene", 0)].get("nodes", [])
    else:
        roots = [index for index, parent in enumerate(document.parents) if parent == -1]
    found = []
    stack = list(reversed(roots))
    while stack:
        index = stack.pop()
        if "mesh" in document.nodes[index]:
            found.append(index)
        stack.extend(reversed(document.nodes[index].get("children", [])))
    return found


def load_attached_prop(attachment, source, output_skin):
    """Resolve the skin joint and load the prop's mesh nodes with their prop-space global matrices."""
    joints = [joint for joint in output_skin.joints if source.node_name(joint) == attachment.joint_name]
    if not joints:
        raise SystemExit("error: --attach joint '%s' is not a joint of the output skin; joints: %s"
                         % (attachment.joint_name, ", ".join(source.node_name(joint) for joint in output_skin.joints)))
    attachment.joint_node = joints[0]
    attachment.document = GltfDocument(attachment.path)
    prop_globals = compute_global_matrices(attachment.document)
    for index in collect_scene_mesh_nodes(attachment.document):
        if "skin" in attachment.document.nodes[index]:
            raise SystemExit("error: prop '%s' node '%s' is skinned; only rigid props can be attached"
                             % (attachment.path, attachment.document.node_name(index)))
        attachment.mesh_nodes.append((index, prop_globals[index]))
    if not attachment.mesh_nodes:
        raise SystemExit("error: prop '%s' has no mesh node in its scene" % attachment.path)


# ---------------------------------------------------------------------------
# Skin, geometry merge
# ---------------------------------------------------------------------------

@dataclasses.dataclass
class OutputSkin:
    source_skins: list
    joints: list
    inverse_bind_matrices: np.ndarray
    joint_slots: dict
    name: str
    skeleton: int


def build_output_skin(document, skinned_infos):
    """Union the skins of the kept skinned meshes into one joint list (one skin for KayKit)."""
    skins = document.json["skins"]
    skin_indices = list(dict.fromkeys(document.nodes[info.node_index]["skin"] for info in skinned_infos))
    joints = []
    matrices = []
    slots = {}
    for skin_index in skin_indices:
        skin = skins[skin_index]
        skin_matrices = document.read_inverse_bind_matrices(skin)
        for local_index, joint in enumerate(skin["joints"]):
            if joint in slots:
                if np.max(np.abs(matrices[slots[joint]] - skin_matrices[local_index])) > SKIN_IBM_MERGE_TOLERANCE:
                    raise SystemExit("error: joint '%s' has different inverse bind matrices in skins %s"
                                     % (document.node_name(joint), skin_indices))
                continue
            slots[joint] = len(joints)
            joints.append(joint)
            matrices.append(skin_matrices[local_index])
    first = skins[skin_indices[0]]
    if len(skin_indices) == 1:
        print("Skin: all %d skinned meshes share skin %d '%s' (%d joints)"
              % (len(skinned_infos), skin_indices[0], first.get("name", ""), len(joints)))
    else:
        warn("skinned meshes use %d skins %s; joints remapped into one skin of %d joints"
             % (len(skin_indices), skin_indices, len(joints)))
    return OutputSkin(skin_indices, joints, np.array(matrices), slots, first.get("name", "skin"),
                      first.get("skeleton", -1))


def check_inverse_bind_matrices(document, output_skin, rest_globals):
    """Compare inverse(IBM_j) with the rest global of joint j; return per-joint match flags."""
    matches = []
    worst = 0.0
    for slot, joint in enumerate(output_skin.joints):
        difference = float(np.max(np.abs(np.linalg.inv(output_skin.inverse_bind_matrices[slot])
                                          - rest_globals[joint])))
        worst = max(worst, difference)
        matches.append(difference <= IBM_TOLERANCE)
        if difference > IBM_TOLERANCE:
            warn("joint %d '%s': inverse(IBM) differs from its rest global by %.3g; rigid props on it"
                 " are baked through inverse(IBM)" % (slot, document.node_name(joint), difference))
    print("IBM check: %d/%d joints have inverse(IBM) == rest global (max abs diff %.3g, tolerance %g)"
          % (sum(matches), len(matches), worst, IBM_TOLERANCE))
    return matches


class MergedGeometry:
    """Accumulates vertex streams and indices of every merged primitive.

    Per primitive it records the first vertex, the source material as (document, material
    index) and the part as (name, kind); source_parts lists ("node", node index) or
    ("attached", AttachedProp) in merge order for the verification.
    """

    def __init__(self):
        self.positions = []
        self.normals = []
        self.texcoords = []
        self.joints = []
        self.weights = []
        self.indices = []
        self.vertex_count = 0
        self.primitive_count = 0
        self.primitive_starts = []
        self.primitive_materials = []
        self.primitive_parts = []
        self.source_parts = []

    def append(self, streams, indices, material, part):
        self.primitive_starts.append(self.vertex_count)
        self.primitive_materials.append(material)
        self.primitive_parts.append(part)
        self.positions.append(streams["POSITION"])
        self.normals.append(streams["NORMAL"])
        self.texcoords.append(streams["TEXCOORD_0"])
        self.joints.append(streams["JOINTS_0"])
        self.weights.append(streams["WEIGHTS_0"])
        self.indices.append(indices + self.vertex_count)
        self.vertex_count += len(streams["POSITION"])
        self.primitive_count += 1

    def concatenated(self):
        return {
            "POSITION": np.concatenate(self.positions),
            "NORMAL": np.concatenate(self.normals),
            "TEXCOORD_0": np.concatenate(self.texcoords),
            "JOINTS_0": np.concatenate(self.joints),
            "WEIGHTS_0": np.concatenate(self.weights),
        }, np.concatenate(self.indices)


def normalize_rows(vectors):
    lengths = np.linalg.norm(vectors, axis=1, keepdims=True)
    return np.where(lengths > MIN_VECTOR_LENGTH, vectors / np.maximum(lengths, MIN_VECTOR_LENGTH), vectors)


def read_primitive_streams(document, primitive, node_name):
    """Read POSITION, NORMAL, TEXCOORD_0 and triangle indices of one primitive."""
    if primitive.get("mode", MODE_TRIANGLES) != MODE_TRIANGLES:
        raise SystemExit("error: '%s' has a non-triangle primitive (mode %d)" % (node_name, primitive["mode"]))
    attributes = primitive["attributes"]
    ignored = sorted(name for name in attributes if name not in SUPPORTED_ATTRIBUTES)
    if ignored:
        warn("'%s': attributes %s are not merged" % (node_name, ", ".join(ignored)))
    if "targets" in primitive:
        warn("'%s': morph targets are not merged" % node_name)
    if "NORMAL" not in attributes:
        raise SystemExit("error: '%s' has no NORMAL attribute" % node_name)
    positions = document.read_accessor_float(attributes["POSITION"])
    if "TEXCOORD_0" in attributes:
        texcoords = document.read_accessor_float(attributes["TEXCOORD_0"])
    else:
        warn("'%s' has no TEXCOORD_0; zeros written" % node_name)
        texcoords = np.zeros((len(positions), 2))
    if "indices" in primitive:
        indices = document.read_accessor(primitive["indices"])[:, 0].astype(np.int64)
    else:
        indices = np.arange(len(positions), dtype=np.int64)
    if len(indices) % 3 != 0 or (len(indices) and indices.max() >= len(positions)):
        raise SystemExit("error: '%s' has invalid triangle indices" % node_name)
    streams = {"POSITION": positions, "NORMAL": document.read_accessor_float(attributes["NORMAL"]),
               "TEXCOORD_0": texcoords}
    return streams, indices


def append_skinned_node(document, info, output_skin, geometry):
    """Copy a skinned mesh as stored (its node transform is ignored by glTF skinning)."""
    node = document.nodes[info.node_index]
    skin_joints = document.json["skins"][node["skin"]]["joints"]
    remap = np.array([output_skin.joint_slots[joint] for joint in skin_joints], dtype=np.int64)
    for primitive in document.json["meshes"][node["mesh"]]["primitives"]:
        streams, indices = read_primitive_streams(document, primitive, info.name)
        attributes = primitive["attributes"]
        if "JOINTS_0" not in attributes or "WEIGHTS_0" not in attributes:
            raise SystemExit("error: skinned mesh '%s' lacks JOINTS_0/WEIGHTS_0" % info.name)
        if "JOINTS_1" in attributes:
            warn("'%s': influences beyond 4 dropped, weights renormalized" % info.name)
        local_joints = document.read_accessor(attributes["JOINTS_0"]).astype(np.int64)
        if local_joints.max() >= len(skin_joints):
            raise SystemExit("error: '%s' references joint %d of a %d joint skin"
                             % (info.name, local_joints.max(), len(skin_joints)))
        weights = document.read_accessor_float(attributes["WEIGHTS_0"])
        sums = weights.sum(axis=1, keepdims=True)
        if np.any(sums <= 0.0):
            raise SystemExit("error: '%s' has vertices with zero total weight" % info.name)
        weights = weights / sums
        joints = np.where(weights > 0.0, remap[local_joints], 0)
        streams.update({"JOINTS_0": joints, "WEIGHTS_0": weights})
        geometry.append(streams, indices, (document, primitive.get("material")), (info.name, "skinned"))
    geometry.source_parts.append(("node", info.node_index))


def compute_rigid_bake_matrix(info, output_skin, rest_globals, ibm_matches):
    """Matrix taking the prop's local vertices into the skin's bind space for its attach joint.

    LBS gives G_j(t) * IBM_j * v_mesh and the prop moves as NodeGlobal(t) * v_local, so
    v_mesh = inverse(IBM_j) * inverse(G_j_rest) * NodeGlobal_rest * v_local, which reduces to
    NodeGlobal_rest * v_local when inverse(IBM_j) equals G_j_rest.
    """
    slot = output_skin.joint_slots[info.attach_joint]
    node_global = rest_globals[info.node_index]
    if ibm_matches[slot]:
        return node_global
    return (np.linalg.inv(output_skin.inverse_bind_matrices[slot])
            @ np.linalg.inv(rest_globals[info.attach_joint]) @ node_global)


def append_rigid_primitives(document, mesh_index, bake, slot, part, geometry):
    """Bake every primitive of a rigid mesh by the matrix bake and weight it fully to joint slot."""
    linear = bake[:3, :3]
    normal_matrix = np.linalg.inv(linear).T
    is_mirrored = np.linalg.det(linear) < 0.0
    for primitive in document.json["meshes"][mesh_index]["primitives"]:
        streams, indices = read_primitive_streams(document, primitive, part[0])
        count = len(streams["POSITION"])
        streams["POSITION"] = streams["POSITION"] @ linear.T + bake[:3, 3]
        streams["NORMAL"] = normalize_rows(streams["NORMAL"] @ normal_matrix.T)
        joints = np.zeros((count, INFLUENCES_PER_SET), dtype=np.int64)
        joints[:, 0] = slot
        weights = np.zeros((count, INFLUENCES_PER_SET))
        weights[:, 0] = 1.0
        streams.update({"JOINTS_0": joints, "WEIGHTS_0": weights})
        if is_mirrored:
            indices = indices.reshape(-1, 3)[:, [0, 2, 1]].reshape(-1)
        geometry.append(streams, indices, (document, primitive.get("material")), part)


def append_rigid_node(document, info, output_skin, rest_globals, ibm_matches, geometry):
    """Bake a rigid prop into bind space and weight it fully to its nearest ancestor joint."""
    info.attach_joint = find_nearest_joint(document.parents, info.node_index, output_skin.joint_slots)
    if info.attach_joint < 0:
        warn("rigid mesh '%s' has no ancestor joint in the skin; skipped" % info.name)
        return
    slot = output_skin.joint_slots[info.attach_joint]
    bake = compute_rigid_bake_matrix(info, output_skin, rest_globals, ibm_matches)
    append_rigid_primitives(document, document.nodes[info.node_index]["mesh"], bake, slot, (info.name, "rigid"),
                            geometry)
    geometry.source_parts.append(("node", info.node_index))


def format_vector(values):
    return "(" + ", ".join("%.4f" % value for value in values) + ")"


def append_attached_prop(attachment, output_skin, rest_globals, ibm_matches, geometry):
    """Bake an external prop at its joint's bind transform times its offset, weighted fully to that joint.

    The prop moves as G_joint(t) * offset * PropGlobal * v; LBS gives G_joint(t) * IBM_joint * v_mesh,
    so v_mesh = inverse(IBM_joint) * offset * PropGlobal * v (inverse(IBM) is the joint's rest global).
    """
    slot = output_skin.joint_slots[attachment.joint_node]
    joint_bind = rest_globals[attachment.joint_node] if ibm_matches[slot] \
        else np.linalg.inv(output_skin.inverse_bind_matrices[slot])
    document = attachment.document
    first_primitive = geometry.primitive_count
    prop_positions = []
    for node_index, prop_global in attachment.mesh_nodes:
        mesh_index = document.nodes[node_index]["mesh"]
        for primitive in document.json["meshes"][mesh_index]["primitives"]:
            positions = document.read_accessor_float(primitive["attributes"]["POSITION"])
            prop_positions.append(positions @ prop_global[:3, :3].T + prop_global[:3, 3])
        append_rigid_primitives(document, mesh_index, joint_bind @ attachment.offset @ prop_global, slot,
                                (attachment.name, "attached"), geometry)
    geometry.source_parts.append(("attached", attachment))
    prop_positions = np.concatenate(prop_positions)
    placed_positions = np.concatenate(geometry.positions[first_primitive:])
    print("Attach '%s' (%d mesh node(s), %d vertices) to joint '%s' at rest position %s; offset translation %s,"
          " rotation (x, y, z, w) %s" % (attachment.name, len(attachment.mesh_nodes), len(prop_positions),
                                         attachment.joint_name, format_vector(joint_bind[:3, 3]),
                                         format_vector(attachment.translation), format_vector(attachment.rotation)))
    print("  prop-space bounds min %s max %s size %s" % (format_vector(prop_positions.min(axis=0)),
          format_vector(prop_positions.max(axis=0)), format_vector(np.ptp(prop_positions, axis=0))))
    print("  placed bounds     min %s max %s (bind pose, model space)"
          % (format_vector(placed_positions.min(axis=0)), format_vector(placed_positions.max(axis=0))))


def warn_frozen_animated_nodes(document, rigid_infos, animation_indices):
    """Warn when a selected animation moves a node between a rigid prop and its attach joint."""
    animations = document.json.get("animations", [])
    animated = {}
    for animation_index in animation_indices:
        animation = animations[animation_index]
        for channel in animation["channels"]:
            node = channel["target"].get("node")
            if node is not None and channel["target"]["path"] in ANIMATED_PATHS:
                animated.setdefault(node, set()).add(animation.get("name", str(animation_index)))
    for info in rigid_infos:
        index = info.node_index
        while index not in (-1, info.attach_joint):
            if index in animated:
                warn("node '%s' above prop '%s' is animated in %d animation(s); that motion is frozen"
                     % (document.node_name(index), info.name, len(animated[index])))
            index = document.parents[index]


# ---------------------------------------------------------------------------
# Materials: one textured material plus emissive-only materials folded into strips
# ---------------------------------------------------------------------------

@dataclasses.dataclass
class EmissiveStrip:
    """One emissive-only material, folded into EMISSIVE_STRIP_ROWS rows below the colour atlas."""
    name: str
    emissive: np.ndarray
    color: np.ndarray
    part_names: list
    first_row: int = 0


@dataclasses.dataclass
class MaterialPlan:
    """main is (document, material index) or None; primitive_strips holds a strip index or -1 (main)."""
    main: tuple
    strips: list
    primitive_strips: list


def material_label(reference):
    document, index = reference
    name = document.json["materials"][index].get("name", "material%d" % index)
    return "'%s' of %s" % (name, os.path.basename(document.path))


def is_emissive_only(material):
    return not collect_material_textures(material) and any(value > 0.0 for value in material.get("emissiveFactor", []))


def compute_material_image_hashes(document, material):
    """sha256 of the image content behind every texture of a material, in first-use order."""
    hashes = []
    for texture_index in collect_material_textures(material):
        texture = document.json["textures"][texture_index]
        if "source" not in texture:
            raise SystemExit("error: texture %d of %s has no core image source" % (texture_index, document.path))
        hashes.append(hashlib.sha256(document.read_image(texture["source"])[0]).hexdigest())
    return hashes


def describe_material_differences(first, second):
    """Material keys (names and textures excluded) whose values differ between two materials."""

    def flatten(material):
        values = {key: value for key, value in material.items()
                  if key not in ("name", "pbrMetallicRoughness") and not key.endswith("Texture")}
        values.update({"pbrMetallicRoughness." + key: value
                       for key, value in material.get("pbrMetallicRoughness", {}).items() if not key.endswith("Texture")})
        return values

    first_values, second_values = flatten(first), flatten(second)
    return sorted(key for key in set(first_values) | set(second_values)
                  if first_values.get(key) != second_values.get(key))


def build_emissive_strip(reference, main_material, part_names):
    """Strip colour: the material's explicit baseColorFactor, else its emissive colour (linear RGBA)."""
    document, index = reference
    material = document.json["materials"][index]
    emissive = np.array(material["emissiveFactor"], dtype=np.float64)
    pbr = material.get("pbrMetallicRoughness", {})
    color = np.array(pbr["baseColorFactor"] if "baseColorFactor" in pbr else list(emissive) + [1.0])
    main_factor = np.array(main_material.get("pbrMetallicRoughness", {}).get("baseColorFactor", [1.0] * 4))
    color = np.divide(color, main_factor, out=np.zeros(4), where=main_factor > 0.0)
    if np.any(color > 1.0):
        warn("strip colour of %s exceeds the main baseColorFactor; clamped" % material_label(reference))
    differences = [key for key in describe_material_differences(main_material, material)
                   if key not in ("emissiveFactor", "pbrMetallicRoughness.baseColorFactor")]
    if differences:
        warn("emissive material %s differs from the main material in %s; the main material's values are used"
             % (material_label(reference), ", ".join(differences)))
    return EmissiveStrip(material.get("name", "material%d" % index), emissive, np.clip(color, 0.0, 1.0), part_names)


def resolve_material_plan(geometry):
    """One textured material (same image content everywhere) plus emissive-only materials as strips."""
    references = list(dict.fromkeys(geometry.primitive_materials))
    if all(index is None for _, index in references):
        return MaterialPlan(None, [], [-1] * geometry.primitive_count)
    if any(index is None for _, index in references):
        raise SystemExit("error: some merged primitives have no material and others have one")
    textured = [reference for reference in references
                if collect_material_textures(reference[0].json["materials"][reference[1]])]
    emissive = [reference for reference in references if is_emissive_only(reference[0].json["materials"][reference[1]])]
    unsupported = [reference for reference in references if reference not in textured and reference not in emissive]
    if not textured or unsupported:
        raise SystemExit("error: one textured material plus emissive-only materials can be merged; unsupported: %s"
                         % (", ".join(material_label(reference) for reference in unsupported) or "no textured material"))
    main = textured[0]
    main_material = main[0].json["materials"][main[1]]
    main_hashes = compute_material_image_hashes(main[0], main_material)
    for reference in textured[1:]:
        material = reference[0].json["materials"][reference[1]]
        if compute_material_image_hashes(reference[0], material) != main_hashes:
            raise SystemExit("error: material %s uses other texture image content than the main material %s"
                             % (material_label(reference), material_label(main)))
        differences = describe_material_differences(main_material, material)
        if differences:
            warn("material %s differs from the main material %s in %s; the main material's values are used"
                 % (material_label(reference), material_label(main), ", ".join(differences)))
    print("Material: main %s (image sha256 %s), %d other textured material(s) with the same image, %d emissive-only"
          % (material_label(main), main_hashes[0][:16] if main_hashes else "-", len(textured) - 1, len(emissive)))
    strips = []
    strip_of_reference = {reference: -1 for reference in textured}
    for reference in emissive:
        part_names = list(dict.fromkeys(part[0] for part, material in
                                        zip(geometry.primitive_parts, geometry.primitive_materials)
                                        if material == reference))
        strip_of_reference[reference] = len(strips)
        strips.append(build_emissive_strip(reference, main_material, part_names))
    return MaterialPlan(main, strips, [strip_of_reference[material] for material in geometry.primitive_materials])


# ---------------------------------------------------------------------------
# Output writing
# ---------------------------------------------------------------------------

def json_scalar(value):
    return float(value) if isinstance(value, (np.floating, float)) else int(value)


class BinaryBufferWriter:
    """Packs accessors tightly into one buffer, one buffer view per accessor."""

    def __init__(self):
        self.data = bytearray()
        self.buffer_views = []
        self.accessors = []

    def _align(self):
        self.data.extend(b"\x00" * ((-len(self.data)) % BUFFER_ALIGNMENT))

    def add_accessor(self, values, component_type, accessor_type, target=None, with_bounds=False,
                     normalized=False):
        component_count = TYPE_COMPONENT_COUNTS[accessor_type]
        typed = np.ascontiguousarray(np.asarray(values).reshape(-1, component_count)
                                     .astype(COMPONENT_DTYPES[component_type]))
        self._align()
        payload = typed.tobytes()
        view = {"buffer": 0, "byteOffset": len(self.data), "byteLength": len(payload)}
        if target is not None:
            view["target"] = target
        self.data.extend(payload)
        self.buffer_views.append(view)
        accessor = {"bufferView": len(self.buffer_views) - 1, "componentType": component_type,
                    "count": len(typed), "type": accessor_type}
        if normalized:
            accessor["normalized"] = True
        if with_bounds:
            accessor["min"] = [json_scalar(value) for value in typed.min(axis=0)]
            accessor["max"] = [json_scalar(value) for value in typed.max(axis=0)]
        self.accessors.append(accessor)
        return len(self.accessors) - 1

    def finish(self):
        self._align()
        return bytes(self.data)


def build_output_nodes(document, output_skin, anchor_node, mesh_name):
    """Keep joints and their ancestors; the merged mesh node takes the first skinned node's place."""
    kept = set()
    for joint in output_skin.joints:
        index = joint
        while index != -1 and index not in kept:
            kept.add(index)
            index = document.parents[index]
    mesh_parent = find_nearest_joint(document.parents, document.parents[anchor_node], kept)
    node_map = {}
    order = []
    for index in range(len(document.nodes)):
        if index == anchor_node:
            mesh_new_index = len(order)
            order.append(None)
        if index in kept:
            node_map[index] = len(order)
            order.append(index)

    def remap_children(old_children, owner):
        children = []
        for child in old_children:
            if child == anchor_node and mesh_parent == owner:
                children.append(mesh_new_index)
            if child in node_map:
                children.append(node_map[child])
        if mesh_parent == owner and anchor_node not in old_children:
            children.append(mesh_new_index)
        return children

    nodes = []
    for index in order:
        if index is None:
            nodes.append({"name": mesh_name, "mesh": 0, "skin": 0})
            continue
        node = {key: copy.deepcopy(value) for key, value in document.nodes[index].items() if key in NODE_KEYS_KEPT}
        children = remap_children(document.nodes[index].get("children", []), index)
        if children:
            node["children"] = children
        nodes.append(node)
    scenes = document.json.get("scenes", [])
    scene = scenes[document.json.get("scene", 0)] if scenes else {"nodes": []}
    old_roots = list(scene.get("nodes", []))
    old_roots += [index for index in sorted(kept) if document.parents[index] == -1 and index not in old_roots]
    roots = remap_children(old_roots, -1)
    return nodes, node_map, roots, scene.get("name", "Scene")


def build_output_animations(document, animation_indices, node_map, writer):
    """Copy the selected animations, keeping channels on kept skeleton nodes only."""
    animations = document.json.get("animations", [])
    input_accessors = set()
    kept_channels = {}
    dropped = {"removed node": 0, "other path": 0}
    for animation_index in animation_indices:
        animation = animations[animation_index]
        channels = []
        for channel in animation["channels"]:
            target = channel["target"]
            if target["path"] not in ANIMATED_PATHS:
                dropped["other path"] += 1
            elif target.get("node") not in node_map:
                dropped["removed node"] += 1
            else:
                channels.append(channel)
                input_accessors.add(animation["samplers"][channel["sampler"]]["input"])
        kept_channels[animation_index] = channels
    accessor_map = {}

    def copy_accessor(index):
        if index not in accessor_map:
            accessor = document.json["accessors"][index]
            accessor_map[index] = writer.add_accessor(
                document.read_accessor(index), accessor["componentType"], accessor["type"],
                with_bounds=index in input_accessors, normalized=accessor.get("normalized", False))
        return accessor_map[index]

    output = []
    channel_total = 0
    for animation_index in animation_indices:
        animation = animations[animation_index]
        name = animation.get("name", "animation%d" % animation_index)
        if not kept_channels[animation_index]:
            warn("animation '%s' has no channel on kept nodes; skipped" % name)
            continue
        sampler_map = {}
        samplers = []
        channels = []
        for channel in kept_channels[animation_index]:
            old_sampler = channel["sampler"]
            if old_sampler not in sampler_map:
                source = animation["samplers"][old_sampler]
                sampler_map[old_sampler] = len(samplers)
                samplers.append({"input": copy_accessor(source["input"]),
                                 "interpolation": source.get("interpolation", "LINEAR"),
                                 "output": copy_accessor(source["output"])})
            channels.append({"sampler": sampler_map[old_sampler],
                             "target": {"node": node_map[channel["target"]["node"]],
                                        "path": channel["target"]["path"]}})
        channel_total += len(channels)
        output.append({"name": name, "channels": channels, "samplers": samplers})
    print("Animations: %d written, %d channels kept, dropped: %d on removed nodes, %d non-TRS"
          % (len(output), channel_total, dropped["removed node"], dropped["other path"]))
    return output


def collect_material_textures(material):
    """Return the texture indices a material references, in first-use order."""
    found = []

    def visit(value, key):
        if isinstance(value, dict):
            if key.endswith("Texture") and "index" in value:
                found.append(value["index"])
            for child_key, child in value.items():
                visit(child, child_key)

    visit(material, "")
    return list(dict.fromkeys(found))


def remap_material_textures(value, key, texture_map):
    if isinstance(value, dict):
        if key.endswith("Texture") and "index" in value:
            value["index"] = texture_map[value["index"]]
            if value.get("texCoord", 0) != 0:
                warn("material texture uses texCoord %d; only TEXCOORD_0 is written" % value["texCoord"])
            if value.pop("extensions", None) is not None:
                warn("texture info extensions dropped")
        for child_key, child in value.items():
            remap_material_textures(child, child_key, texture_map)


def build_output_material(document, material_index, out_dir, name):
    """Copy the material and write its images as external files (NAME.png, NAME_1.png, ...)."""
    if material_index is None:
        return {}, []
    material = copy.deepcopy(document.json["materials"][material_index])
    if material.pop("extensions", None) is not None:
        warn("material extensions dropped")
    texture_indices = collect_material_textures(material)
    textures, images, samplers, written = [], [], [], []
    texture_map, image_map, sampler_map = {}, {}, {}
    for texture_index in texture_indices:
        source_texture = document.json["textures"][texture_index]
        texture = {}
        if "sampler" in source_texture:
            if source_texture["sampler"] not in sampler_map:
                sampler_map[source_texture["sampler"]] = len(samplers)
                samplers.append(copy.deepcopy(document.json["samplers"][source_texture["sampler"]]))
            texture["sampler"] = sampler_map[source_texture["sampler"]]
        if "source" not in source_texture:
            raise SystemExit("error: texture %d has no core image source" % texture_index)
        image_index = source_texture["source"]
        if image_index not in image_map:
            data, mime = document.read_image(image_index)
            extension = MIME_EXTENSIONS.get(mime, ".png")
            file_name = name + ("" if not images else "_%d" % len(images)) + extension
            with open(os.path.join(out_dir, file_name), "wb") as handle:
                handle.write(data)
            written.append(file_name)
            image = {"uri": urllib.parse.quote(file_name), "name": document.json["images"][image_index].get("name", name)}
            if mime:
                image["mimeType"] = mime
            image_map[image_index] = len(images)
            images.append(image)
        texture["source"] = image_map[image_index]
        if "name" in source_texture:
            texture["name"] = source_texture["name"]
        texture_map[texture_index] = len(textures)
        textures.append(texture)
    remap_material_textures(material, "", texture_map)
    parts = {"materials": [material]}
    if textures:
        parts.update({"textures": textures, "images": images})
    if samplers:
        parts["samplers"] = samplers
    return parts, written


@dataclasses.dataclass
class EmissiveAtlas:
    """The colour atlas with the emissive strips appended, and the emissive mask of the same size."""
    source_pixels: np.ndarray
    color_pixels: np.ndarray
    mask_pixels: np.ndarray
    emissive_factor: np.ndarray
    strip_colors: list
    mask_colors: list


def decode_image(data):
    """Decode PNG/JPEG bytes to an (height, width, 3 or 4) uint8 array."""
    Image, _, _ = import_pillow()
    image = Image.open(io.BytesIO(data))
    if image.mode not in ("RGB", "RGBA"):
        image = image.convert("RGBA")
    return np.array(image)


def encode_srgb(linear):
    """Linear [0, 1] colour to 8-bit sRGB."""
    linear = np.clip(np.asarray(linear, dtype=np.float64), 0.0, 1.0)
    encoded = np.where(linear <= SRGB_LINEAR_LIMIT, linear * SRGB_LINEAR_SLOPE,
                       SRGB_SCALE * linear ** (1.0 / SRGB_GAMMA) - SRGB_OFFSET)
    return np.round(encoded * MAX_CHANNEL_VALUE).astype(np.uint8)


def main_color_texture(plan):
    """Return (document, material, baseColorTexture info) of the main material; it must be its only texture."""
    document, index = plan.main
    material = document.json["materials"][index]
    texture_info = material.get("pbrMetallicRoughness", {}).get("baseColorTexture")
    if texture_info is None or collect_material_textures(material) != [texture_info["index"]]:
        raise SystemExit("error: folding emissive parts needs a main material whose only texture is baseColorTexture")
    if "emissiveTexture" in material or any(value > 0.0 for value in material.get("emissiveFactor", [])):
        raise SystemExit("error: the main material is emissive itself; emissive parts cannot be folded into a mask")
    return document, material, texture_info


def build_emissive_atlas(plan):
    """Append EMISSIVE_STRIP_ROWS rows per strip below the colour atlas and build the emissive mask."""
    document, material, texture_info = main_color_texture(plan)
    source_pixels = decode_image(document.read_image(document.json["textures"][texture_info["index"]]["source"])[0])
    height, width, channels = source_pixels.shape
    emissive_factor = np.max([strip.emissive for strip in plan.strips], axis=0)
    color_rows, mask_rows, strip_colors, mask_colors = [], [], [], []
    for strip_index, strip in enumerate(plan.strips):
        strip.first_row = height + strip_index * EMISSIVE_STRIP_ROWS
        color = encode_srgb(strip.color[:3])
        if channels == 4:
            color = np.append(color, np.uint8(round(strip.color[3] * MAX_CHANNEL_VALUE)))
        mask = encode_srgb(np.divide(strip.emissive, emissive_factor, out=np.zeros(3), where=emissive_factor > 0.0))
        strip_colors.append(color)
        mask_colors.append(mask)
        color_rows.append(np.broadcast_to(color, (EMISSIVE_STRIP_ROWS, width, channels)))
        mask_rows.append(np.broadcast_to(mask, (EMISSIVE_STRIP_ROWS, width, 3)))
    color_pixels = np.ascontiguousarray(np.concatenate([source_pixels] + color_rows), dtype=np.uint8)
    mask_pixels = np.ascontiguousarray(np.concatenate([np.zeros((height, width, 3), np.uint8)] + mask_rows))
    return EmissiveAtlas(source_pixels, color_pixels, mask_pixels, emissive_factor, strip_colors, mask_colors)


def compute_primitive_ranges(geometry):
    """(first vertex, end vertex) of every merged primitive."""
    ends = geometry.primitive_starts[1:] + [geometry.vertex_count]
    return list(zip(geometry.primitive_starts, ends))


def fold_texcoords(texcoords, geometry, plan, atlas):
    """Scale V of textured parts into the old atlas rows; put emissive parts at the centre of their strip."""
    old_height = atlas.source_pixels.shape[0]
    new_height = atlas.color_pixels.shape[0]
    folded = texcoords.copy()
    for primitive_index, ((start, end), strip_index) in enumerate(zip(compute_primitive_ranges(geometry),
                                                                      plan.primitive_strips)):
        if strip_index < 0:
            if np.any(texcoords[start:end, 1] < 0.0) or np.any(texcoords[start:end, 1] > 1.0):
                raise SystemExit("error: part '%s' has V outside [0, 1]; a wrapping V cannot be rescaled"
                                 % geometry.primitive_parts[primitive_index][0])
            folded[start:end, 1] = texcoords[start:end, 1] * (old_height / new_height)
        else:
            strip = plan.strips[strip_index]
            folded[start:end] = (STRIP_CENTER_U, (strip.first_row + EMISSIVE_STRIP_ROWS / 2.0) / new_height)
    return folded


def build_folded_material(plan, atlas, out_dir, name):
    """Write NAME.png (atlas + strips) and NAME_emissive.png (mask); return the material parts and files."""
    Image, _, _ = import_pillow()
    document, source_material, texture_info = main_color_texture(plan)
    material = copy.deepcopy(source_material)
    if material.pop("extensions", None) is not None:
        warn("material extensions dropped")
    if texture_info.get("texCoord", 0) != 0:
        warn("material texture uses texCoord %d; only TEXCOORD_0 is written" % texture_info["texCoord"])
    material["pbrMetallicRoughness"]["baseColorTexture"] = {"index": 0}
    material["emissiveTexture"] = {"index": 1}
    material["emissiveFactor"] = [float(value) for value in atlas.emissive_factor]
    source_texture = document.json["textures"][texture_info["index"]]
    color_file = name + ".png"
    mask_file = name + EMISSIVE_MASK_SUFFIX + ".png"
    Image.fromarray(atlas.color_pixels).save(os.path.join(out_dir, color_file), "PNG", optimize=True)
    Image.fromarray(atlas.mask_pixels).save(os.path.join(out_dir, mask_file), "PNG", optimize=True)
    image_name = document.json["images"][source_texture["source"]].get("name", name)
    images = [{"uri": urllib.parse.quote(color_file), "mimeType": "image/png", "name": image_name},
              {"uri": urllib.parse.quote(mask_file), "mimeType": "image/png", "name": image_name + EMISSIVE_MASK_SUFFIX}]
    textures = [{"source": 0}, {"source": 1}]
    parts = {"materials": [material], "textures": textures, "images": images}
    if "sampler" in source_texture:
        parts["samplers"] = [copy.deepcopy(document.json["samplers"][source_texture["sampler"]])]
        for texture in textures:
            texture["sampler"] = 0
    return parts, [color_file, mask_file]


def print_emissive_strips(plan, atlas, name):
    """Document the strip rows, colours and the UVs the emissive parts were moved to."""
    old_height, width = atlas.source_pixels.shape[:2]
    new_height = atlas.color_pixels.shape[0]
    print("Emissive fold: atlas %dx%d -> %dx%d; V of textured parts scaled by %d/%d = %.6f (old texels keep their"
          " pixel rows 0-%d)" % (width, old_height, width, new_height, old_height, new_height,
                                 old_height / new_height, old_height - 1))
    for strip, color, mask in zip(plan.strips, atlas.strip_colors, atlas.mask_colors):
        last_row = strip.first_row + EMISSIVE_STRIP_ROWS - 1
        print("  strip rows %d-%d (v %.6f-%.6f) <- material '%s' (parts: %s): UV (%.2f, %.6f), colour 8-bit sRGB %s,"
              " mask %s" % (strip.first_row, last_row, strip.first_row / new_height, (last_row + 1) / new_height,
                            strip.name, ", ".join(strip.part_names), STRIP_CENTER_U,
                            (strip.first_row + EMISSIVE_STRIP_ROWS / 2.0) / new_height, color.tolist(), mask.tolist()))
    print("  %s.png = baseColorTexture; %s%s.png = emissiveTexture (black above row %d); emissiveFactor %s"
          % (name, name, EMISSIVE_MASK_SUFFIX, old_height, format_vector(atlas.emissive_factor)))


def write_geometry_accessors(streams, indices, writer, joint_count):
    """Write the merged vertex streams and indices; return (attributes, indices accessor, info)."""
    vertex_count = len(streams["POSITION"])
    joint_type = COMPONENT_UNSIGNED_BYTE if joint_count <= MAX_UINT8_JOINTS else COMPONENT_UNSIGNED_SHORT
    index_type = COMPONENT_UNSIGNED_SHORT if vertex_count < MAX_UINT16_INDEXED_VERTICES \
        else COMPONENT_UNSIGNED_INT
    attributes = {
        "POSITION": writer.add_accessor(streams["POSITION"], COMPONENT_FLOAT, "VEC3", TARGET_ARRAY_BUFFER, True),
        "NORMAL": writer.add_accessor(normalize_rows(streams["NORMAL"]), COMPONENT_FLOAT, "VEC3",
                                      TARGET_ARRAY_BUFFER),
        "TEXCOORD_0": writer.add_accessor(streams["TEXCOORD_0"], COMPONENT_FLOAT, "VEC2", TARGET_ARRAY_BUFFER),
        "JOINTS_0": writer.add_accessor(streams["JOINTS_0"], joint_type, "VEC4", TARGET_ARRAY_BUFFER),
        "WEIGHTS_0": writer.add_accessor(streams["WEIGHTS_0"], COMPONENT_FLOAT, "VEC4", TARGET_ARRAY_BUFFER),
    }
    indices_accessor = writer.add_accessor(indices, index_type, "SCALAR", TARGET_ELEMENT_ARRAY_BUFFER)
    return attributes, indices_accessor, (len(indices), index_type, joint_type)


@dataclasses.dataclass
class FoldCheck:
    """Inputs of the texture check: source UVs, per-primitive vertex ranges and strips, the atlas, the files."""
    source_texcoords: np.ndarray
    primitive_ranges: list
    primitive_strips: list
    strips: list
    atlas: EmissiveAtlas
    color_file: str
    mask_file: str


@dataclasses.dataclass
class ConversionResult:
    gltf_path: str
    written_files: list
    source_parts: list
    part_ranges: list
    attach_joints: list
    fold_check: object = None


def compute_part_ranges(geometry):
    """(name, kind, first vertex, vertex count) per merged part; consecutive primitives of a part are grouped."""
    ranges = []
    for (name, kind), (start, end) in zip(geometry.primitive_parts, compute_primitive_ranges(geometry)):
        if ranges and ranges[-1][:2] == (name, kind) and sum(ranges[-1][2:]) == start:
            ranges[-1] = (name, kind, ranges[-1][2], ranges[-1][3] + end - start)
        else:
            ranges.append((name, kind, start, end - start))
    return ranges


def convert(document, infos, attachments, animation_indices, out_dir, name):
    """Merge the selected mesh nodes and attached props; write NAME.gltf / NAME.bin / textures into out_dir."""
    selected = [info for info in infos if info.is_selected]
    skinned = [info for info in selected if info.is_skinned]
    rigid = [info for info in selected if not info.is_skinned]
    if not skinned:
        raise SystemExit("error: no skinned mesh node selected")
    output_skin = build_output_skin(document, skinned)
    rest_globals = compute_global_matrices(document)
    ibm_matches = check_inverse_bind_matrices(document, output_skin, rest_globals)
    geometry = MergedGeometry()
    for info in skinned:
        append_skinned_node(document, info, output_skin, geometry)
    for info in rigid:
        append_rigid_node(document, info, output_skin, rest_globals, ibm_matches, geometry)
    for attachment in attachments:
        load_attached_prop(attachment, document, output_skin)
        append_attached_prop(attachment, output_skin, rest_globals, ibm_matches, geometry)
    warn_frozen_animated_nodes(document, [info for info in rigid if info.attach_joint >= 0], animation_indices)
    part_ranges = compute_part_ranges(geometry)
    print("Merge order: " + ", ".join(part[0] for part in part_ranges))
    plan = resolve_material_plan(geometry)
    streams, indices = geometry.concatenated()
    atlas = None
    if plan.strips:
        atlas = build_emissive_atlas(plan)
        source_texcoords = streams["TEXCOORD_0"]
        streams["TEXCOORD_0"] = fold_texcoords(source_texcoords, geometry, plan, atlas)
    os.makedirs(out_dir, exist_ok=True)
    writer = BinaryBufferWriter()
    attributes, indices_accessor, (index_count, index_type, joint_type) = \
        write_geometry_accessors(streams, indices, writer, len(output_skin.joints))
    primitive = {"attributes": attributes, "indices": indices_accessor, "mode": MODE_TRIANGLES}
    if plan.main is not None:
        primitive["material"] = 0
    nodes, node_map, roots, scene_name = build_output_nodes(document, output_skin, skinned[0].node_index, name)
    skin = {"name": output_skin.name, "joints": [node_map[joint] for joint in output_skin.joints],
            "inverseBindMatrices": writer.add_accessor(output_skin.inverse_bind_matrices.transpose(0, 2, 1),
                                                       COMPONENT_FLOAT, "MAT4")}
    if output_skin.skeleton in node_map:
        skin["skeleton"] = node_map[output_skin.skeleton]
    animations = build_output_animations(document, animation_indices, node_map, writer)
    fold_check = None
    if plan.strips:
        material_parts, written = build_folded_material(plan, atlas, out_dir, name)
        print_emissive_strips(plan, atlas, name)
        fold_check = FoldCheck(source_texcoords, compute_primitive_ranges(geometry), plan.primitive_strips,
                               plan.strips, atlas, written[0], written[1])
    elif plan.main is not None:
        material_parts, written = build_output_material(plan.main[0], plan.main[1], out_dir, name)
    else:
        material_parts, written = {}, []
    binary = writer.finish()
    asset = {"version": "2.0", "generator": "%s (from %s)" % (GENERATOR, os.path.basename(document.path))}
    if "copyright" in document.json.get("asset", {}):
        asset["copyright"] = document.json["asset"]["copyright"]
    output = {"asset": asset, "scene": 0, "scenes": [{"name": scene_name, "nodes": roots}], "nodes": nodes,
              "meshes": [{"name": name, "primitives": [primitive]}], "skins": [skin]}
    output.update(material_parts)
    if animations:
        output["animations"] = animations
    output.update({"accessors": writer.accessors, "bufferViews": writer.buffer_views,
                   "buffers": [{"uri": urllib.parse.quote(name + ".bin"), "byteLength": len(binary)}]})
    with open(os.path.join(out_dir, name + ".bin"), "wb") as handle:
        handle.write(binary)
    gltf_path = os.path.join(out_dir, name + ".gltf")
    with open(gltf_path, "w", encoding="ascii") as handle:
        json.dump(output, handle, separators=(",", ":"), ensure_ascii=True)
    print("Counts: input %d mesh nodes / %d primitives + %d attached prop(s); merged %d parts / %d primitives"
          % (len(infos), sum(info.primitive_count for info in infos), len(attachments), len(geometry.source_parts),
             geometry.primitive_count))
    print("Output: 1 primitive, %d vertices, %d indices (%d triangles), indices %s, joints %s, %d joints"
          % (geometry.vertex_count, index_count, index_count // 3, COMPONENT_NAMES[index_type],
             COMPONENT_NAMES[joint_type], len(output_skin.joints)))
    return ConversionResult(gltf_path, [name + ".gltf", name + ".bin"] + written, geometry.source_parts,
                            part_ranges, list(dict.fromkeys(attachment.joint_name for attachment in attachments)),
                            fold_check)


# ---------------------------------------------------------------------------
# Structural validation of the written file
# ---------------------------------------------------------------------------

class StructureChecker:
    """Own glTF 2.0 structural checks, used when the Khronos validator is not installed."""

    def __init__(self, path):
        self.document = GltfDocument(path)
        self.json = self.document.json
        self.problems = []
        self.stats = {}
        self.used_accessors = set()
        self.used_views = set()

    def problem(self, message):
        self.problems.append(message)

    def count(self, key):
        return len(self.json.get(key, []))

    def check_index(self, value, key, where):
        if not isinstance(value, int) or not 0 <= value < self.count(key):
            self.problem("%s: %s index %r out of range" % (where, key, value))
            return False
        return True

    def run(self):
        if self.json.get("asset", {}).get("version") != "2.0":
            self.problem("asset.version is not 2.0")
        self.check_buffers()
        self.check_accessors()
        self.check_nodes()
        self.check_skins()
        self.check_meshes()
        self.check_animations()
        self.check_materials()
        self.check_unused()
        return self.problems

    def check_buffers(self):
        for index, buffer in enumerate(self.json.get("buffers", [])):
            if len(self.document.buffers[index]) != buffer["byteLength"]:
                self.problem("buffer %d: byteLength %d != file size %d"
                             % (index, buffer["byteLength"], len(self.document.buffers[index])))
        for index, view in enumerate(self.json.get("bufferViews", [])):
            if not self.check_index(view.get("buffer"), "buffers", "bufferView %d" % index):
                continue
            if view.get("byteOffset", 0) + view["byteLength"] > self.json["buffers"][view["buffer"]]["byteLength"]:
                self.problem("bufferView %d exceeds its buffer" % index)
            stride = view.get("byteStride")
            if stride is not None and (stride < MIN_BYTE_STRIDE or stride > MAX_BYTE_STRIDE or stride % 4):
                self.problem("bufferView %d: invalid byteStride %d" % (index, stride))

    def check_accessors(self):
        for index, accessor in enumerate(self.json.get("accessors", [])):
            where = "accessor %d" % index
            if accessor.get("componentType") not in COMPONENT_DTYPES or accessor.get("type") not in TYPE_COMPONENT_COUNTS:
                self.problem(where + ": invalid componentType/type")
                continue
            if accessor.get("count", 0) < 1:
                self.problem(where + ": count < 1")
            component_size = COMPONENT_DTYPES[accessor["componentType"]].itemsize
            if "bufferView" in accessor and self.check_index(accessor["bufferView"], "bufferViews", where):
                self.used_views.add(accessor["bufferView"])
                view = self.json["bufferViews"][accessor["bufferView"]]
                if (view.get("byteOffset", 0) + accessor.get("byteOffset", 0)) % component_size:
                    self.problem(where + ": misaligned byteOffset")
            try:
                data = self.document.read_accessor(index)
            except (ValueError, IndexError) as error:
                self.problem("%s: %s" % (where, error))
                continue
            component_count = TYPE_COMPONENT_COUNTS[accessor["type"]]
            for bound, reducer in (("min", np.min), ("max", np.max)):
                if bound not in accessor:
                    continue
                if len(accessor[bound]) != component_count:
                    self.problem("%s: %s has %d values" % (where, bound, len(accessor[bound])))
                    continue
                actual = reducer(data, axis=0)
                stored = np.array(accessor[bound]).astype(data.dtype)
                if not np.array_equal(actual, stored):
                    self.problem("%s: %s %s != data %s" % (where, bound, accessor[bound], actual.tolist()))

    def check_nodes(self):
        nodes = self.json.get("nodes", [])
        for index, node in enumerate(nodes):
            where = "node %d" % index
            for child in node.get("children", []):
                self.check_index(child, "nodes", where + " child")
            if "matrix" in node and any(key in node for key in ANIMATED_PATHS):
                self.problem(where + ": both matrix and TRS")
            if "rotation" in node and abs(np.linalg.norm(node["rotation"]) - 1.0) > UNIT_LENGTH_TOLERANCE:
                self.problem(where + ": rotation is not unit length")
            if "mesh" in node:
                self.check_index(node["mesh"], "meshes", where)
            if "skin" in node:
                self.check_index(node["skin"], "skins", where)
                if "mesh" not in node:
                    self.problem(where + ": skin without mesh")
        parents = self.document.parents
        for scene_index, scene in enumerate(self.json.get("scenes", [])):
            for root in scene.get("nodes", []):
                if self.check_index(root, "nodes", "scene %d" % scene_index) and parents[root] != -1:
                    self.problem("scene %d: node %d is not a root" % (scene_index, root))

    def check_skins(self):
        for index, skin in enumerate(self.json.get("skins", [])):
            where = "skin %d" % index
            joints = skin.get("joints", [])
            if len(set(joints)) != len(joints):
                self.problem(where + ": duplicate joints")
            for joint in joints:
                self.check_index(joint, "nodes", where + " joint")
            if "inverseBindMatrices" in skin and self.check_index(skin["inverseBindMatrices"], "accessors", where):
                self.used_accessors.add(skin["inverseBindMatrices"])
                accessor = self.json["accessors"][skin["inverseBindMatrices"]]
                if accessor["type"] != "MAT4" or accessor["componentType"] != COMPONENT_FLOAT \
                        or accessor["count"] != len(joints):
                    self.problem(where + ": inverseBindMatrices must be %d float MAT4" % len(joints))
            if "skeleton" in skin:
                self.check_index(skin["skeleton"], "nodes", where + " skeleton")
            self.stats["joints"] = len(joints)

    def skin_joint_count_for_mesh(self, mesh_index):
        counts = [len(self.json["skins"][node["skin"]]["joints"]) for node in self.json.get("nodes", [])
                  if node.get("mesh") == mesh_index and "skin" in node]
        return min(counts) if counts else None

    def check_meshes(self):
        for mesh_index, mesh in enumerate(self.json.get("meshes", [])):
            joint_count = self.skin_joint_count_for_mesh(mesh_index)
            for primitive_index, primitive in enumerate(mesh["primitives"]):
                self.check_primitive(primitive, "mesh %d primitive %d" % (mesh_index, primitive_index), joint_count)

    def read_attribute(self, primitive, name, where):
        index = primitive["attributes"].get(name)
        if index is None or not self.check_index(index, "accessors", where + " " + name):
            return None, None
        self.used_accessors.add(index)
        return self.json["accessors"][index], self.document.read_accessor_float(index)

    def check_primitive(self, primitive, where, joint_count):
        if primitive.get("mode", MODE_TRIANGLES) != MODE_TRIANGLES:
            self.problem(where + ": not triangles")
        if "material" in primitive:
            self.check_index(primitive["material"], "materials", where)
        counts = {name: self.json["accessors"][index]["count"] for name, index in primitive["attributes"].items()
                  if self.check_index(index, "accessors", where + " " + name)}
        if len(set(counts.values())) > 1:
            self.problem(where + ": attribute counts differ %s" % counts)
        position_accessor, positions = self.read_attribute(primitive, "POSITION", where)
        if position_accessor is None:
            self.problem(where + ": no POSITION")
            return
        vertex_count = len(positions)
        self.stats["vertices"] = self.stats.get("vertices", 0) + vertex_count
        if "min" not in position_accessor or "max" not in position_accessor:
            self.problem(where + ": POSITION lacks min/max")
        self.check_normals(primitive, where)
        self.check_indices(primitive, where, vertex_count)
        self.check_influences(primitive, where, joint_count)
        for name in primitive["attributes"]:
            self.used_accessors.add(primitive["attributes"][name])

    def check_normals(self, primitive, where):
        accessor, normals = self.read_attribute(primitive, "NORMAL", where)
        if accessor is None:
            return
        deviation = float(np.max(np.abs(np.linalg.norm(normals, axis=1) - 1.0)))
        self.stats["normal_length_deviation"] = deviation
        if deviation > UNIT_LENGTH_TOLERANCE:
            self.problem(where + ": NORMAL not unit length (max deviation %.3g)" % deviation)

    def check_indices(self, primitive, where, vertex_count):
        if "indices" not in primitive or not self.check_index(primitive["indices"], "accessors", where + " indices"):
            return
        self.used_accessors.add(primitive["indices"])
        accessor = self.json["accessors"][primitive["indices"]]
        if accessor["type"] != "SCALAR" or accessor["componentType"] not in INDEX_COMPONENT_TYPES:
            self.problem(where + ": indices must be unsigned SCALAR")
            return
        view = self.json["bufferViews"][accessor["bufferView"]]
        if "byteStride" in view or view.get("target", TARGET_ELEMENT_ARRAY_BUFFER) != TARGET_ELEMENT_ARRAY_BUFFER:
            self.problem(where + ": index buffer view has a stride or a vertex target")
        indices = self.document.read_accessor(primitive["indices"])[:, 0].astype(np.int64)
        restart = np.iinfo(COMPONENT_DTYPES[accessor["componentType"]]).max
        self.stats["indices"] = self.stats.get("indices", 0) + len(indices)
        self.stats["max_index"] = int(indices.max())
        if len(indices) % 3:
            self.problem(where + ": index count %d is not a multiple of 3" % len(indices))
        if indices.max() >= vertex_count:
            self.problem(where + ": index %d >= vertex count %d" % (indices.max(), vertex_count))
        if np.any(indices == restart):
            self.problem(where + ": index uses the primitive restart value")

    def check_influences(self, primitive, where, joint_count):
        joints_accessor, joints = self.read_attribute(primitive, "JOINTS_0", where)
        weights_accessor, weights = self.read_attribute(primitive, "WEIGHTS_0", where)
        if joints_accessor is None or weights_accessor is None:
            if joint_count is not None:
                self.problem(where + ": skinned mesh without JOINTS_0/WEIGHTS_0")
            return
        if joints_accessor["componentType"] not in JOINT_COMPONENT_TYPES or joints_accessor["type"] != "VEC4":
            self.problem(where + ": JOINTS_0 must be uint8/uint16 VEC4")
        if weights_accessor["type"] != "VEC4":
            self.problem(where + ": WEIGHTS_0 must be VEC4")
        joints = joints.astype(np.int64)
        self.stats["max_joint_index"] = int(joints.max())
        if joint_count is not None and joints.max() >= joint_count:
            self.problem(where + ": joint index %d >= joint count %d" % (joints.max(), joint_count))
        if np.any(weights < 0.0):
            self.problem(where + ": negative weights")
        deviation = float(np.max(np.abs(weights.sum(axis=1) - 1.0)))
        self.stats["weight_sum_deviation"] = deviation
        if deviation > WEIGHT_SUM_TOLERANCE:
            self.problem(where + ": weights do not sum to 1 (max deviation %.3g)" % deviation)
        unused_nonzero = int(np.count_nonzero((weights == 0.0) & (joints != 0)))
        self.stats["zero_weight_nonzero_joint"] = unused_nonzero
        duplicates = 0
        for first in range(INFLUENCES_PER_SET):
            for second in range(first + 1, INFLUENCES_PER_SET):
                duplicates += int(np.count_nonzero((joints[:, first] == joints[:, second])
                                                   & (weights[:, first] > 0) & (weights[:, second] > 0)))
        self.stats["duplicate_joint_influences"] = duplicates

    def check_animations(self):
        nodes = self.json.get("nodes", [])
        for animation_index, animation in enumerate(self.json.get("animations", [])):
            where = "animation %d '%s'" % (animation_index, animation.get("name", ""))
            if not animation.get("channels") or not animation.get("samplers"):
                self.problem(where + ": no channels or samplers")
            targets = set()
            for channel in animation.get("channels", []):
                target = channel["target"]
                if not 0 <= channel["sampler"] < len(animation["samplers"]):
                    self.problem(where + ": channel sampler out of range")
                    continue
                if not 0 <= target.get("node", -1) < len(nodes) or target["path"] not in ANIMATED_PATHS:
                    self.problem(where + ": invalid channel target %s" % target)
                    continue
                key = (target["node"], target["path"])
                if key in targets:
                    self.problem(where + ": duplicate channel target %s" % (key,))
                targets.add(key)
                self.check_sampler(animation["samplers"][channel["sampler"]], target["path"], where)

    def check_sampler(self, sampler, path, where):
        interpolation = sampler.get("interpolation", "LINEAR")
        if interpolation not in INTERPOLATIONS:
            self.problem(where + ": invalid interpolation %s" % interpolation)
        if not self.check_index(sampler["input"], "accessors", where) or \
                not self.check_index(sampler["output"], "accessors", where):
            return
        self.used_accessors.update((sampler["input"], sampler["output"]))
        input_accessor = self.json["accessors"][sampler["input"]]
        output_accessor = self.json["accessors"][sampler["output"]]
        if input_accessor["type"] != "SCALAR" or input_accessor["componentType"] != COMPONENT_FLOAT:
            self.problem(where + ": sampler input must be float SCALAR")
        if "min" not in input_accessor or "max" not in input_accessor:
            self.problem(where + ": sampler input lacks min/max")
        times = self.document.read_accessor_float(sampler["input"])[:, 0]
        if np.any(np.diff(times) <= 0.0) or times[0] < 0.0:
            self.problem(where + ": sampler input not strictly increasing / negative")
        expected = len(times) * (CUBIC_VALUES_PER_KEY if interpolation == "CUBICSPLINE" else 1)
        if output_accessor["count"] != expected:
            self.problem(where + ": sampler output count %d != %d" % (output_accessor["count"], expected))
        if output_accessor["type"] != PATH_TYPES[path]:
            self.problem(where + ": %s output type %s" % (path, output_accessor["type"]))

    def check_materials(self):
        for index, texture in enumerate(self.json.get("textures", [])):
            if "sampler" in texture:
                self.check_index(texture["sampler"], "samplers", "texture %d" % index)
            if "source" in texture:
                self.check_index(texture["source"], "images", "texture %d" % index)
        for index, material in enumerate(self.json.get("materials", [])):
            for texture_index in collect_material_textures(material):
                self.check_index(texture_index, "textures", "material %d" % index)
        for index, image in enumerate(self.json.get("images", [])):
            if "bufferView" in image:
                self.used_views.add(image["bufferView"])
            try:
                data, mime = self.document.read_image(index)
            except OSError as error:
                self.problem("image %d: %s" % (index, error))
                continue
            signature = MIME_SIGNATURES.get(mime)
            if signature is None or not data.startswith(signature):
                self.problem("image %d: content does not match mimeType %s" % (index, mime))

    def check_unused(self):
        unused_accessors = self.count("accessors") - len(self.used_accessors)
        unused_views = self.count("bufferViews") - len(self.used_views)
        self.stats["unused_accessors"] = unused_accessors
        self.stats["unused_buffer_views"] = unused_views
        if unused_accessors or unused_views:
            self.problem("%d unused accessors, %d unused buffer views" % (unused_accessors, unused_views))


def run_khronos_validator(gltf_path):
    """Run the official gltf_validator when it is on PATH; return None when it is not installed."""
    executable = shutil.which(KHRONOS_VALIDATOR)
    if executable is None:
        print("Khronos %s not installed; using own structural checks" % KHRONOS_VALIDATOR)
        return None
    completed = subprocess.run([executable, gltf_path], capture_output=True, text=True)
    print("Khronos %s exit code %d:\n%s%s" % (KHRONOS_VALIDATOR, completed.returncode, completed.stdout,
                                             completed.stderr))
    return completed.returncode == 0


def run_structure_checks(gltf_path):
    """Run the official validator if present plus the own structural checks; True when clean."""
    is_khronos_clean = run_khronos_validator(gltf_path)
    checker = StructureChecker(gltf_path)
    problems = checker.run()
    stats = checker.stats
    print("Structure checks: %d problem(s)" % len(problems))
    print("  vertices %d, indices %d, max index %d, joints %d, max joint index %d"
          % (stats.get("vertices", 0), stats.get("indices", 0), stats.get("max_index", -1),
             stats.get("joints", 0), stats.get("max_joint_index", -1)))
    print("  weight sum max deviation %.3g, normal length max deviation %.3g, zero-weight nonzero joints %d,"
          " duplicate joint influences %d, unused accessors %d, unused buffer views %d"
          % (stats.get("weight_sum_deviation", 0.0), stats.get("normal_length_deviation", 0.0),
             stats.get("zero_weight_nonzero_joint", 0), stats.get("duplicate_joint_influences", 0),
             stats.get("unused_accessors", 0), stats.get("unused_buffer_views", 0)))
    for problem in problems[:MAX_PRINTED_PROBLEMS]:
        print("  PROBLEM: " + problem)
    return not problems and is_khronos_clean is not False


# ---------------------------------------------------------------------------
# Verification: CPU skinning of the original against the merged file
# ---------------------------------------------------------------------------

def read_skin_influences(document, attributes):
    """Return (joints, weights) over every JOINTS_n / WEIGHTS_n set."""
    joint_sets, weight_sets = [], []
    set_index = 0
    while "JOINTS_%d" % set_index in attributes and "WEIGHTS_%d" % set_index in attributes:
        joint_sets.append(document.read_accessor(attributes["JOINTS_%d" % set_index]).astype(np.int64))
        weight_sets.append(document.read_accessor_float(attributes["WEIGHTS_%d" % set_index]))
        set_index += 1
    return np.concatenate(joint_sets, axis=1), np.concatenate(weight_sets, axis=1)


def transform_vertices(matrices, positions, normals):
    """Apply per-vertex (V,4,4) or one (4,4) matrix to positions and inverse-transpose to normals."""
    if matrices.ndim == 2:
        matrices = matrices[None]
    linear = matrices[:, :3, :3]
    moved = np.einsum("vij,vj->vi", np.broadcast_to(linear, (len(positions), 3, 3)), positions) \
        + np.broadcast_to(matrices[:, :3, 3], positions.shape)
    normal_matrices = np.linalg.inv(linear).transpose(0, 2, 1)
    turned = np.einsum("vij,vj->vi", np.broadcast_to(normal_matrices, (len(normals), 3, 3)), normals)
    return moved, normalize_rows(turned)


def compute_node_vertices(document, node_index, global_matrices):
    """World positions and normals of a mesh node: LBS when skinned, node global otherwise."""
    node = document.nodes[node_index]
    positions_list, normals_list = [], []
    if "skin" in node:
        skin = document.json["skins"][node["skin"]]
        joint_matrices = global_matrices[skin["joints"]] @ document.read_inverse_bind_matrices(skin)
    for primitive in document.json["meshes"][node["mesh"]]["primitives"]:
        attributes = primitive["attributes"]
        positions = document.read_accessor_float(attributes["POSITION"])
        normals = document.read_accessor_float(attributes["NORMAL"])
        if "skin" in node:
            joints, weights = read_skin_influences(document, attributes)
            blended = np.einsum("vk,vkij->vij", weights, joint_matrices[joints])
            positions, normals = transform_vertices(blended, positions, normals)
        else:
            positions, normals = transform_vertices(global_matrices[node_index], positions, normals)
        positions_list.append(positions)
        normals_list.append(normals)
    return np.concatenate(positions_list), np.concatenate(normals_list)


def compute_attached_vertices(attachment, joint_global):
    """World positions and normals of an attached prop placed on its (animated) joint."""
    document = attachment.document
    positions_list, normals_list = [], []
    for node_index, prop_global in attachment.mesh_nodes:
        matrix = joint_global @ attachment.offset @ prop_global
        for primitive in document.json["meshes"][document.nodes[node_index]["mesh"]]["primitives"]:
            attributes = primitive["attributes"]
            positions, normals = transform_vertices(matrix, document.read_accessor_float(attributes["POSITION"]),
                                                    document.read_accessor_float(attributes["NORMAL"]))
            positions_list.append(positions)
            normals_list.append(normals)
    return np.concatenate(positions_list), np.concatenate(normals_list)


def compute_source_vertices(document, source_parts, global_matrices):
    parts = [compute_node_vertices(document, value, global_matrices) if kind == "node"
             else compute_attached_vertices(value, global_matrices[value.joint_node]) for kind, value in source_parts]
    return np.concatenate([part[0] for part in parts]), np.concatenate([part[1] for part in parts])


def find_animation(document, name):
    for index, animation in enumerate(document.json.get("animations", [])):
        if animation.get("name") == name:
            return index
    return None


def choose_verify_animations(source, merged):
    """Idle, Running_A and a 1H attack (any attack as fallback) present in both files."""
    common = [animation.get("name") for animation in merged.json.get("animations", [])
              if find_animation(source, animation.get("name")) is not None]
    chosen = [name for name in VERIFY_ANIMATIONS if name in common]
    attack = next((name for name in common if name.startswith(VERIFY_ATTACK_PREFIX) and VERIFY_ATTACK_WORD in name),
                  None) or next((name for name in common if VERIFY_ATTACK_WORD in name), None)
    if attack:
        chosen.append(attack)
    return chosen


def measure_errors(source_vertices, merged_vertices):
    position_error = float(np.max(np.linalg.norm(source_vertices[0] - merged_vertices[0], axis=1)))
    cosines = np.clip(np.sum(source_vertices[1] * merged_vertices[1], axis=1), -1.0, 1.0)
    normal_error = float(np.degrees(np.max(np.arccos(cosines))))
    return position_error, normal_error


def run_verification(source, result):
    """Compare CPU-skinned positions of the original selection with the merged file."""
    merged = GltfDocument(result.gltf_path)
    mesh_node = next(index for index, node in enumerate(merged.nodes) if "mesh" in node)
    rest_vertices = compute_source_vertices(source, result.source_parts, compute_global_matrices(source))
    height = float(np.ptp(rest_vertices[0][:, UP_AXIS]))
    tolerance = VERIFY_RELATIVE_TOLERANCE * height
    print("Verify: %d vertices, model height %.4f, tolerance %.3g (= %g * height)"
          % (len(rest_vertices[0]), height, tolerance, VERIFY_RELATIVE_TOLERANCE))
    cases = [("rest pose", None, None, [0.0])]
    for name in choose_verify_animations(source, merged):
        source_animation = AnimationEvaluator(source, find_animation(source, name))
        merged_animation = AnimationEvaluator(merged, find_animation(merged, name))
        times = np.linspace(source_animation.start, source_animation.end, VERIFY_TIMES_PER_ANIMATION)
        cases.append((name, source_animation, merged_animation, times))
    if len(cases) == 1:
        warn("no verification animation present in the output; checking the rest pose only")
    worst = 0.0
    for name, source_animation, merged_animation, times in cases:
        for time in times:
            source_overrides = source_animation.evaluate(time) if source_animation else None
            merged_overrides = merged_animation.evaluate(time) if merged_animation else None
            source_vertices = compute_source_vertices(source, result.source_parts,
                                                      compute_global_matrices(source, source_overrides))
            merged_vertices = compute_node_vertices(merged, mesh_node,
                                                    compute_global_matrices(merged, merged_overrides))
            position_error, normal_error = measure_errors(source_vertices, merged_vertices)
            motion = float(np.max(np.linalg.norm(source_vertices[0] - rest_vertices[0], axis=1)))
            worst = max(worst, position_error)
            print("  %-28s t=%7.4f s  motion vs rest %.4f  max position error %.3e (%.2e of height)"
                  "  max normal error %.4f deg"
                  % (name, time, motion, position_error, position_error / height, normal_error))
    passed = worst < tolerance
    if passed:
        print("Verify PASSED: max position error %.3e < tolerance %.3e" % (worst, tolerance))
    else:
        print("Verify FAILED: max position error %.3e >= tolerance %.3e" % (worst, tolerance))
    is_texture_clean = run_texture_check(merged, mesh_node, result)
    return passed and is_texture_clean


def read_png_file(out_dir, file_name):
    with open(os.path.join(out_dir, file_name), "rb") as handle:
        return decode_image(handle.read())


def check_atlas_pixels(fold, color, mask):
    """Problems of the written atlas and mask: old rows pixel-exact, strips filled, mask black elsewhere."""
    source = fold.atlas.source_pixels
    old_height = source.shape[0]
    problems = []
    if color.shape[1:] != source.shape[1:] or not np.array_equal(color[:old_height], source):
        problems.append("colour atlas rows 0-%d differ from the source image" % (old_height - 1))
    if color.shape[0] != fold.atlas.color_pixels.shape[0] or mask.shape[:2] != color.shape[:2]:
        problems.append("atlas %s / mask %s sizes are wrong" % (color.shape, mask.shape))
        return problems
    if np.any(mask[:old_height]):
        problems.append("mask is not black over rows 0-%d" % (old_height - 1))
    for strip, strip_color, mask_color in zip(fold.strips, fold.atlas.strip_colors, fold.atlas.mask_colors):
        rows = slice(strip.first_row, strip.first_row + EMISSIVE_STRIP_ROWS)
        if not np.all(color[rows] == strip_color) or not np.all(mask[rows] == mask_color):
            problems.append("strip '%s' rows %d-%d do not hold their colour / mask"
                            % (strip.name, rows.start, rows.stop - 1))
    return problems


def check_folded_texcoords(fold, texcoords, color, mask):
    """Problems of the written UVs; returns (problems, max texel shift of textured parts)."""
    old_height, width = fold.atlas.source_pixels.shape[:2]
    new_height = color.shape[0]
    problems = []
    worst_shift = 0.0
    for (start, end), strip_index in zip(fold.primitive_ranges, fold.primitive_strips):
        written, source = texcoords[start:end], fold.source_texcoords[start:end]
        if strip_index < 0:
            shift = max(float(np.max(np.abs(written[:, 0] - source[:, 0]))) * width,
                        float(np.max(np.abs(written[:, 1] * new_height - source[:, 1] * old_height))))
            worst_shift = max(worst_shift, shift)
            continue
        strip = fold.strips[strip_index]
        rows = np.floor(written[:, 1] * new_height).astype(np.int64)
        columns = np.clip(np.floor(written[:, 0] * width).astype(np.int64), 0, width - 1)
        if np.any(rows < strip.first_row) or np.any(rows >= strip.first_row + EMISSIVE_STRIP_ROWS):
            problems.append("strip '%s' parts sample rows outside %d-%d"
                            % (strip.name, strip.first_row, strip.first_row + EMISSIVE_STRIP_ROWS - 1))
        elif not np.all(mask[rows, columns] == fold.atlas.mask_colors[strip_index]):
            problems.append("strip '%s' parts do not sample their mask colour" % strip.name)
    if worst_shift > UV_CHECK_TOLERANCE_TEXELS:
        problems.append("textured parts moved by %.3g texels" % worst_shift)
    return problems, worst_shift


def run_texture_check(merged, mesh_node, result):
    """With folded emissive parts: compare the written atlas, mask and UVs with the source texture and UVs."""
    fold = result.fold_check
    if fold is None:
        return True
    out_dir = os.path.dirname(result.gltf_path)
    color = read_png_file(out_dir, fold.color_file)
    mask = read_png_file(out_dir, fold.mask_file)
    primitive = merged.json["meshes"][merged.nodes[mesh_node]["mesh"]]["primitives"][0]
    texcoords = merged.read_accessor_float(primitive["attributes"]["TEXCOORD_0"])
    problems = check_atlas_pixels(fold, color, mask)
    uv_problems, worst_shift = check_folded_texcoords(fold, texcoords, color, mask)
    problems += uv_problems
    print("Texture check: atlas rows 0-%d pixel-exact vs the source image, %d strip(s) of %d rows, max texel shift of"
          " textured parts %.2e (tolerance %g), emissive parts inside their strip: %d problem(s)"
          % (fold.atlas.source_pixels.shape[0] - 1, len(fold.strips), EMISSIVE_STRIP_ROWS, worst_shift,
             UV_CHECK_TOLERANCE_TEXELS, len(problems)))
    for problem in problems:
        print("  PROBLEM: " + problem)
    return not problems


# ---------------------------------------------------------------------------
# Preview: orthographic views of the merged file, coloured by part
# ---------------------------------------------------------------------------

# (label, screen right, screen up, towards the camera); the character faces +Z, its right hand is at -X
PREVIEW_VIEWS = (
    ("front", np.array([1.0, 0.0, 0.0]), np.array([0.0, 1.0, 0.0]), np.array([0.0, 0.0, 1.0])),
    ("right side", np.array([0.0, 0.0, 1.0]), np.array([0.0, 1.0, 0.0]), np.array([-1.0, 0.0, 0.0])),
)
PREVIEW_LIGHT_UP = 0.5
PREVIEW_LIGHT_RIGHT = 0.3


def parse_preview_clip(text):
    """NAME[@SECONDS] -> (name, seconds or None)."""
    name, at_sign, time_text = text.rpartition("@")
    if not at_sign:
        return text, None
    try:
        return name, float(time_text)
    except ValueError:
        raise SystemExit("error: --preview-clip expects NAME[@SECONDS], got '%s'" % text)


def collect_preview_poses(merged, clip_specification):
    """[(label, global matrices)] for the rest pose and one frame of the preview clip."""
    poses = [("rest pose", compute_global_matrices(merged))]
    clip_name, time = parse_preview_clip(clip_specification)
    clip_index = find_animation(merged, clip_name)
    if clip_index is None:
        warn("preview clip '%s' is not in the output; rest pose only" % clip_name)
        return poses
    evaluator = AnimationEvaluator(merged, clip_index)
    time = (evaluator.start + evaluator.end) / 2.0 if time is None else time
    poses.append(("%s t=%.2f s" % (clip_name, time), compute_global_matrices(merged, evaluator.evaluate(time))))
    return poses


def assign_part_colors(part_ranges):
    """Body parts in muted colours, bone props in darker ones, attached props in loud ones."""
    used = {}
    colors = []
    for _, kind, _, _ in part_ranges:
        palette = PREVIEW_PALETTES[kind]
        colors.append(palette[used.get(kind, 0) % len(palette)])
        used[kind] = used.get(kind, 0) + 1
    return colors


def load_preview_font(ImageFont):
    try:
        return ImageFont.load_default(size=PREVIEW_FONT_SIZE)
    except TypeError:
        return ImageFont.load_default()


@dataclasses.dataclass
class PreviewPanel:
    """Placement of one view in the preview image: pixel = origin + (world - center) * scale, Y flipped."""
    left: float
    top: float
    scale: float
    center_x: float
    top_y: float

    def to_pixels(self, screen_x, screen_y):
        pixel_x = self.left + PREVIEW_PANEL_WIDTH / 2.0 + (screen_x - self.center_x) * self.scale
        pixel_y = self.top + PREVIEW_TITLE_HEIGHT + PREVIEW_MARGIN_FRACTION * PREVIEW_PANEL_HEIGHT \
            + (self.top_y - screen_y) * self.scale
        return pixel_x, pixel_y


def draw_preview_panel(draw, panel, positions, triangles, triangle_colors, view, markers):
    """Painter's algorithm: flat-shaded triangles from far to near, then crosses on the marker points."""
    _, right, up, toward = view
    light = toward + PREVIEW_LIGHT_UP * up + PREVIEW_LIGHT_RIGHT * right
    light /= np.linalg.norm(light)
    corners = positions[triangles]
    normals = normalize_rows(np.cross(corners[:, 1] - corners[:, 0], corners[:, 2] - corners[:, 0]))
    shades = PREVIEW_AMBIENT + (1.0 - PREVIEW_AMBIENT) * np.abs(normals @ light)
    pixel_x, pixel_y = panel.to_pixels(positions @ right, positions @ up)
    for triangle in np.argsort((positions @ toward)[triangles].mean(axis=1)):
        color = tuple(int(channel * shades[triangle]) for channel in triangle_colors[triangle])
        draw.polygon([(pixel_x[corner], pixel_y[corner]) for corner in triangles[triangle]], fill=color)
    marker_x, marker_y = panel.to_pixels(markers @ right, markers @ up)
    for x, y in zip(marker_x, marker_y):
        draw.line([(x - PREVIEW_MARKER_SIZE, y), (x + PREVIEW_MARKER_SIZE, y)], fill=PREVIEW_INK, width=2)
        draw.line([(x, y - PREVIEW_MARKER_SIZE), (x, y + PREVIEW_MARKER_SIZE)], fill=PREVIEW_INK, width=2)


def draw_preview_legend(draw, font, part_ranges, colors, top):
    for index, ((name, kind, _, count), color) in enumerate(zip(part_ranges, colors)):
        left = (index % PREVIEW_LEGEND_COLUMNS) * PREVIEW_PANEL_WIDTH + PREVIEW_SWATCH_SIZE
        line_top = top + (index // PREVIEW_LEGEND_COLUMNS) * PREVIEW_LEGEND_LINE_HEIGHT
        draw.rectangle([left, line_top, left + PREVIEW_SWATCH_SIZE, line_top + PREVIEW_SWATCH_SIZE], fill=color)
        draw.text((left + 2 * PREVIEW_SWATCH_SIZE, line_top - 2), "%s (%s, %d verts)" % (name, kind, count),
                  fill=PREVIEW_INK, font=font)


def render_preview(result, clip_specification, out_dir):
    """Write preview.png: rows = rest pose and a clip frame, columns = front and right side; + markers at
    the joints props were attached to and a legend of the parts."""
    Image, ImageDraw, ImageFont = import_pillow()
    merged = GltfDocument(result.gltf_path)
    mesh_node = next(index for index, node in enumerate(merged.nodes) if "mesh" in node)
    primitive = merged.json["meshes"][merged.nodes[mesh_node]["mesh"]]["primitives"][0]
    triangles = merged.read_accessor(primitive["indices"])[:, 0].astype(np.int64).reshape(-1, 3)
    colors = assign_part_colors(result.part_ranges)
    starts = np.array([first for _, _, first, _ in result.part_ranges])
    triangle_colors = [colors[part] for part in np.searchsorted(starts, triangles[:, 0], side="right") - 1]
    marker_nodes = [index for index, node in enumerate(merged.nodes) if node.get("name") in result.attach_joints]
    poses = [(label, compute_node_vertices(merged, mesh_node, matrices)[0], matrices[marker_nodes, :3, 3])
             for label, matrices in collect_preview_poses(merged, clip_specification)]
    projections = [(positions @ view[1], positions @ view[2]) for _, positions, _ in poses for view in PREVIEW_VIEWS]
    width_extent = max(float(np.ptp(screen_x)) for screen_x, _ in projections)
    top_y = max(float(screen_y.max()) for _, screen_y in projections)
    height_extent = top_y - min(float(screen_y.min()) for _, screen_y in projections)
    usable = 1.0 - 2.0 * PREVIEW_MARGIN_FRACTION
    scale = min(PREVIEW_PANEL_WIDTH * usable / width_extent,
                (PREVIEW_PANEL_HEIGHT * usable - PREVIEW_TITLE_HEIGHT) / height_extent)
    legend_rows = -(-len(result.part_ranges) // PREVIEW_LEGEND_COLUMNS)
    image = Image.new("RGB", (PREVIEW_LEGEND_COLUMNS * PREVIEW_PANEL_WIDTH, len(poses) * PREVIEW_PANEL_HEIGHT
                              + (legend_rows + 1) * PREVIEW_LEGEND_LINE_HEIGHT), PREVIEW_BACKGROUND)
    draw = ImageDraw.Draw(image)
    font = load_preview_font(ImageFont)
    for row, (label, positions, markers) in enumerate(poses):
        for column, view in enumerate(PREVIEW_VIEWS):
            screen_x, _ = projections[row * len(PREVIEW_VIEWS) + column]
            panel = PreviewPanel(column * PREVIEW_PANEL_WIDTH, row * PREVIEW_PANEL_HEIGHT, scale,
                                 (float(screen_x.min()) + float(screen_x.max())) / 2.0, top_y)
            draw_preview_panel(draw, panel, positions, triangles, triangle_colors, view, markers)
            draw.text((panel.left + PREVIEW_SWATCH_SIZE, panel.top + 4), "%s - %s" % (view[0], label),
                      fill=PREVIEW_INK, font=font)
    draw_preview_legend(draw, font, result.part_ranges, colors,
                        len(poses) * PREVIEW_PANEL_HEIGHT + PREVIEW_LEGEND_LINE_HEIGHT // 2)
    image.save(os.path.join(out_dir, PREVIEW_FILE), "PNG", optimize=True)
    print("Preview: %s (%s; crosses mark %s)" % (PREVIEW_FILE, ", ".join(label for label, _, _ in poses),
                                                ", ".join(result.attach_joints) or "no joints"))
    return PREVIEW_FILE


# ---------------------------------------------------------------------------
# Command line
# ---------------------------------------------------------------------------

def parse_arguments(argv):
    parser = argparse.ArgumentParser(
        usage="%(prog)s IN.glb OUT_DIR --name NAME [--keep NODE ...] [--drop NODE ...]\n"
              "       [--attach JOINT=PROP.gltf[@TX,TY,TZ[,QX,QY,QZ,QW]] ...]\n"
              "       [--animations all|none|NAME,NAME,...] [--verify] [--preview] [--preview-clip NAME[@SECONDS]]\n"
              "       %(prog)s IN.glb --list",
        description="Merge a KayKit character GLB into one skinned primitive with one material.",
        epilog="Example: python kaykit_merge.py Skeleton_Warrior.glb out/Warrior --name Warrior "
               "--attach handslot.r=Skeleton_Axe.gltf@0,0.0333,0,0,-1,0,0 "
               "handslot.l=Skeleton_Shield_Large_A.gltf@0,0.017,0.1559 --verify --preview")
    parser.add_argument("input", help="input .glb or .gltf")
    parser.add_argument("out_dir", nargs="?", help="output directory (created if missing)")
    parser.add_argument("--name", help="output base name: NAME.gltf, NAME.bin, NAME.png")
    parser.add_argument("--keep", nargs="+", action="extend", default=[], metavar="NODE",
                        help="rigid prop nodes to keep (default: all); skinned parts are always kept")
    parser.add_argument("--drop", nargs="+", action="extend", default=[], metavar="NODE",
                        help="mesh nodes to drop (skinned or rigid)")
    parser.add_argument("--attach", nargs="+", action="extend", default=[], metavar="JOINT=PROP.gltf[@OFFSET]",
                        help="merge an external rigid prop onto a skin joint; OFFSET = TX,TY,TZ[,QX,QY,QZ,QW]"
                             " local to the joint (identity when omitted)")
    parser.add_argument("--animations", default="all", help="all | none | NAME,NAME,... (default: all)")
    parser.add_argument("--verify", action="store_true", help="CPU-skin original vs merged and compare")
    parser.add_argument("--preview", action="store_true", help="write OUT_DIR/preview.png (needs Pillow)")
    parser.add_argument("--preview-clip", default=PREVIEW_DEFAULT_CLIP, metavar="NAME[@SECONDS]",
                        help="clip of the preview's second row (default %s at mid clip)" % PREVIEW_DEFAULT_CLIP)
    parser.add_argument("--list", action="store_true", help="print the mesh node list and exit")
    args = parser.parse_args(argv)
    if not args.list and (not args.out_dir or not args.name):
        parser.error("OUT_DIR and --name are required unless --list is given")
    return args


def print_file_sizes(out_dir, files):
    print("Files:")
    for file_name in files:
        path = os.path.normpath(os.path.join(out_dir, file_name))
        print("  %-40s %10d bytes" % (path, os.path.getsize(path)))


def main(argv=None):
    args = parse_arguments(argv)
    source = GltfDocument(args.input)
    infos = collect_mesh_nodes(source)
    apply_selection(infos, args.keep, args.drop)
    print_mesh_node_table(source, infos)
    if args.list:
        return 0
    attachments = [parse_attach_specification(text) for text in args.attach]
    animation_indices = resolve_animations(source, args.animations)
    result = convert(source, infos, attachments, animation_indices, args.out_dir, args.name)
    is_valid = run_structure_checks(result.gltf_path)
    is_verified = run_verification(source, result) if args.verify else True
    if args.preview:
        result.written_files.append(render_preview(result, args.preview_clip, args.out_dir))
    print_file_sizes(args.out_dir, result.written_files)
    return 0 if is_valid and is_verified else 1


if __name__ == "__main__":
    sys.exit(main())
