"""Generated weapon meshes for Barrowlight (license-free): crescent.gltf (a sword-arc blade lying flat, its middle
pointing +Z) and thorn_ring.gltf (a ring of thorns lying flat). Written as .gltf + .bin."""
import base64
import json
import math
import os

import numpy as np

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "meshes")


def write_gltf(name, positions, normals, indices):
    positions = np.asarray(positions, dtype=np.float32)
    normals = np.asarray(normals, dtype=np.float32)
    indices = np.asarray(indices, dtype=np.uint16 if len(positions) < 65535 else np.uint32)
    blobs = [positions.tobytes(), normals.tobytes(), indices.tobytes()]
    data = b""
    offsets = []
    for blob in blobs:
        offsets.append(len(data))
        data += blob + b"\0" * ((4 - len(blob) % 4) % 4)
    with open(os.path.join(OUT, f"{name}.bin"), "wb") as file:
        file.write(data)
    doc = {
        "asset": {"version": "2.0", "generator": "barrowlight gen_meshes.py"},
        "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0, "name": name}],
        "meshes": [{"name": name, "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1}, "indices": 2, "material": 0}]}],
        "materials": [{"name": name, "doubleSided": True, "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0.0, "roughnessFactor": 0.4}}],
        "buffers": [{"uri": f"{name}.bin", "byteLength": len(data)}],
        "bufferViews": [{"buffer": 0, "byteOffset": offsets[i], "byteLength": len(blobs[i]), "target": 34962 if i < 2 else 34963} for i in range(3)],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": len(positions), "type": "VEC3",
             "min": positions.min(axis=0).tolist(), "max": positions.max(axis=0).tolist()},
            {"bufferView": 1, "componentType": 5126, "count": len(normals), "type": "VEC3"},
            {"bufferView": 2, "componentType": 5123 if indices.dtype == np.uint16 else 5125, "count": len(indices), "type": "SCALAR"},
        ],
    }
    with open(os.path.join(OUT, f"{name}.gltf"), "w") as file:
        json.dump(doc, file)


def add_triangle(positions, normals, indices, a, b, c):
    normal = np.cross(np.subtract(b, a), np.subtract(c, a))
    length = np.linalg.norm(normal)
    normal = normal / length if length > 1e-9 else np.array([0.0, 1.0, 0.0])
    base = len(positions)
    positions += [a, b, c]
    normals += [normal, normal, normal]
    indices += [base, base + 1, base + 2]


def crescent():
    """A blade of light: an arc band 150 degrees wide, radius 0.55 to 1.0, thickest in the middle, tapering to
    points at both ends; a ridge along the middle so it catches light from above."""
    segments = 40
    span = math.radians(150.0)
    positions, normals, indices = [], [], []
    rows = []
    for i in range(segments + 1):
        t = i / segments
        angle = -span / 2 + span * t
        taper = math.sin(math.pi * t) ** 0.7
        outer = 1.0
        inner = 1.0 - 0.45 * taper
        middle = (outer + inner) * 0.5
        height = 0.08 * taper
        direction = (math.sin(angle), math.cos(angle))
        rows.append(((direction[0] * outer, 0.0, direction[1] * outer),
                     (direction[0] * middle, height, direction[1] * middle),
                     (direction[0] * inner, 0.0, direction[1] * inner)))
    for i in range(segments):
        o0, m0, n0 = rows[i]
        o1, m1, n1 = rows[i + 1]
        add_triangle(positions, normals, indices, o0, m0, m1)
        add_triangle(positions, normals, indices, o0, m1, o1)
        add_triangle(positions, normals, indices, m0, n0, n1)
        add_triangle(positions, normals, indices, m0, n1, m1)
    write_gltf("crescent", positions, normals, indices)


def thorn_ring():
    """A ring of radius 1 with 28 thorns pointing outward and up; a low twisted vine body."""
    positions, normals, indices = [], [], []
    segments, sides, tube = 56, 6, 0.06
    ring = []
    for i in range(segments + 1):
        angle = i / segments * math.tau
        cx, cz = math.sin(angle), math.cos(angle)
        twist = 0.03 * math.sin(angle * 9.0)
        row = []
        for k in range(sides):
            phi = k / sides * math.tau
            radius = 1.0 + (tube + twist) * math.cos(phi)
            row.append((cx * radius, tube * math.sin(phi) + tube, cz * radius))
        ring.append(row)
    for i in range(segments):
        for k in range(sides):
            a, b = ring[i][k], ring[i][(k + 1) % sides]
            c, d = ring[i + 1][k], ring[i + 1][(k + 1) % sides]
            add_triangle(positions, normals, indices, a, c, b)
            add_triangle(positions, normals, indices, b, c, d)
    thorns = 28
    for i in range(thorns):
        angle = (i + 0.5) / thorns * math.tau
        cx, cz = math.sin(angle), math.cos(angle)
        tx, tz = math.cos(angle), -math.sin(angle)
        base = (cx * 1.0, tube, cz * 1.0)
        length = 0.32 if i % 2 == 0 else 0.22
        tip = (cx * (1.0 + length), tube + length * 0.6, cz * (1.0 + length))
        width = 0.07
        left = (base[0] + tx * width, base[1], base[2] + tz * width)
        right = (base[0] - tx * width, base[1], base[2] - tz * width)
        top = (base[0], base[1] + width, base[2])
        add_triangle(positions, normals, indices, left, tip, top)
        add_triangle(positions, normals, indices, top, tip, right)
        add_triangle(positions, normals, indices, right, tip, left)
    write_gltf("thorn_ring", positions, normals, indices)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    crescent()
    thorn_ring()
    print(os.listdir(OUT))
