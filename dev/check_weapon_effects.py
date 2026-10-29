"""Checks the weapon effects against the code that fires them (no editor call):

    python check_weapon_effects.py

- every effect name the weapons code finds (effects.Find("...")) and every trail in ProjectileCatalog.cs is defined
  exactly once across build_effects.py + build_effects_weapons.py;
- every property is a reflected Particle Emitter property (and named in components.md, else listed as a note);
- enum values, texture keys (project data or tools/out/weapon_textures), max_particles 64..2048;
- emitted with a size (EmitShape, EmitArc, Emit(.., size, lifetime)): size_over_life is exact (min == max) and
  normalised (birth 1, or birth 0 and peak 1) and lifetime is [1, 1];
- emitted with a colour: the gradient is neutral (r == g == b), so the script's tint sets the hue.
Exit code 1 when something fails.
"""
import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
WEAPONS = os.path.join(HERE, "..", "data", "Scripts", "Weapons")
CATALOG = os.path.join(WEAPONS, "ProjectileCatalog.cs")
BUILDERS = [os.path.join(HERE, "build_effects.py"), os.path.join(HERE, "build_effects_weapons.py")]
ENGINE = r"C:\Workspace\github\UnravelEngine\UnravelEngine"
COMPONENTS_MD = os.path.join(ENGINE, "editor_data", "data", "mcp", "skills", "components.md")
META_CPP = os.path.join(ENGINE, "engine", "engine", "meta", "ecs", "components", "particle_emitter_component.cpp")
PROJECT_DATA = r"C:\Workspace\github\UnravelEngine\UnravelEngine-Sample-Survivors\data"
GENERATED = os.path.join(HERE, "tools", "out", "weapon_textures")
GENERATED_KEY = "app:/data/Textures/weapons/"
MIN_PARTICLES, MAX_PARTICLES = 64, 2048
WHITE_TINTS = {"WHITE"}

ENUMS = {
    "render_mode": {"billboard", "horizontal_billboard", "vertical_billboard", "camera_facing", "stretched", "trail",
                    "ribbon", "mesh", "fixed_axis"},
    "blend_mode": {"normal", "additive", "multiply", "premultiplied"},
    "lighting": {"unlit", "volumetric", "surface"},
    "shape": {"sphere", "hemisphere", "circle", "box", "rect", "cone", "line", "torus", "mesh"},
    "direction": {"up", "outward", "inward"},
    "collision": {"none", "kill", "bounce"},
    "primitive": {"quad", "octagon", "cutout"},
    "simulation_space": {"world", "local"},
    "culling_mode": {"renderer_based", "always_simulate"},
}


def read(path):
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def split_arguments(text):
    """Top-level comma split of a call's argument text."""
    parts, depth, current = [], 0, ""
    for char in text:
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
        if char == "," and depth == 0:
            parts.append(current.strip())
            current = ""
        else:
            current += char
    if current.strip():
        parts.append(current.strip())
    return parts


def call_arguments(source, open_index):
    """The argument text of the call whose '(' is at open_index."""
    depth = 0
    for index in range(open_index, len(source)):
        if source[index] == "(":
            depth += 1
        elif source[index] == ")":
            depth -= 1
            if depth == 0:
                return source[open_index + 1:index]
    return ""


def scan_code():
    """name -> set of emission kinds ('sized', 'tinted', 'untinted', 'white'), and the set of used names."""
    used = set()
    kinds = {}
    for path in glob.glob(os.path.join(WEAPONS, "**", "*.cs"), recursive=True):
        source = read(path)
        variables = {}
        for variable, name in re.findall(r'(\w+)\s*=\s*(?:\w+\.)*effects\.Find\("(\w+)"\)', source):
            variables[variable] = name
            used.add(name)
        used.update(re.findall(r'Find\("(\w+)"\)', source))
        for match in re.finditer(r'\b(EmitShape|EmitArc|EmitMoving|Emit)\s*\(', source):
            arguments = split_arguments(call_arguments(source, match.end() - 1))
            if not arguments:
                continue
            inline = re.search(r'Find\("(\w+)"\)', arguments[0])
            name = inline.group(1) if inline else variables.get(arguments[0])
            if name is None:
                continue
            method = match.group(1)
            if method == "EmitShape":
                kind = {"sized", "tinted"}
                color = arguments[2]
            elif method == "EmitArc":
                kind = {"sized", "tinted"}
                color = arguments[6]
            elif method == "EmitMoving":
                kind, color = {"sized", "untinted"}, None
            else:
                color = arguments[3] if len(arguments) >= 4 else None
                kind = {"tinted"} if color else {"untinted"}
                if len(arguments) >= 6:
                    kind.add("sized")
            if color in WHITE_TINTS:
                kind = (kind - {"tinted"}) | {"white"}
            kinds.setdefault(name, set()).update(kind)
        for variable in re.findall(r'\bparticle\s*=\s*(\w+)\s*,', source):
            if variable in variables:
                kinds.setdefault(variables[variable], set()).add("tinted")
    for trail in re.findall(r'\btrail\s*=\s*"(\w+)"', read(CATALOG)):
        used.add(trail)
        kinds.setdefault(trail, set()).add("untinted")
    return used, kinds


def static_definitions():
    counts = {}
    for path in BUILDERS:
        for name in re.findall(r'\b(?:effect|define)\(\s*"\w+",\s*"(\w+)"', read(path)):
            counts[name] = counts.get(name, 0) + 1
    return counts


def documented_names():
    text = read(COMPONENTS_MD)
    start = text.index("## Particle Emitter")
    end = text.index("\n## ", start + 5)
    return set(re.findall(r'[a-z][a-z0-9_]*', text[start:end]))


def reflected_names():
    names = set(re.findall(r'>\("([a-z_0-9]+)"_hs\)', read(META_CPP)))
    return {n for n in names if not n.startswith("component_")}


def texture_path(key):
    if key.startswith(GENERATED_KEY):
        return os.path.join(GENERATED, key[len(GENERATED_KEY):])
    return os.path.join(PROJECT_DATA, key[len("app:/data/"):].replace("/", os.sep))


def check_size_curve(props):
    curve = sorted(props.get("size_over_life", []), key=lambda k: k["progress"])
    if not curve:
        return "no size_over_life"
    if any(abs(k["min"] - k["max"]) > 1e-6 for k in curve):
        return "size_over_life has min != max (the passed size would not be exact)"
    birth = curve[0]["min"] if curve[0]["progress"] <= 0.0 else None
    if birth is None:
        return "size_over_life has no key at progress 0"
    peak = max(k["max"] for k in curve)
    if birth > 0.0 and abs(birth - 1.0) > 1e-6:
        return f"birth size {birth} (the script's size is the birth size: author it as 1.0)"
    if birth == 0.0 and abs(peak - 1.0) > 1e-6:
        return f"starts at 0 but peaks at {peak} (the script's size becomes the peak: author it as 1.0)"
    if props.get("lifetime") != [1.0, 1.0]:
        return f"lifetime {props.get('lifetime')} (the script passes the lifetime: leave [1, 1])"
    return None


def main():
    import build_effects_weapons as weapons
    from build_effects import EFFECTS
    used, kinds = scan_code()
    counts = static_definitions()
    problems = []
    missing = sorted(n for n in used if n not in EFFECTS)
    twice = sorted(n for n in used if counts.get(n, 0) > 1)
    not_static = sorted(n for n in used if n in EFFECTS and counts.get(n, 0) == 0)
    unused = sorted(n for n in weapons.NAMES if n not in used)
    problems += [f"missing: {n}" for n in missing] + [f"defined {counts[n]} times: {n}" for n in twice]
    problems += [f"defined here but never used by the code: {n}" for n in unused]
    documented, reflected = documented_names(), reflected_names()
    undocumented = set()
    for name in weapons.NAMES:
        props = EFFECTS[name][1]
        for key, value in props.items():
            if key not in reflected:
                problems.append(f"{name}: unknown property {key}")
            elif key not in documented:
                undocumented.add(key)
            if key in ENUMS and value not in ENUMS[key]:
                problems.append(f"{name}: {key} = {value!r} is not one of {sorted(ENUMS[key])}")
        texture = props.get("texture")
        if texture and not os.path.exists(texture_path(texture)):
            problems.append(f"{name}: texture not found {texture}")
        if not MIN_PARTICLES <= props.get("max_particles", 0) <= MAX_PARTICLES:
            problems.append(f"{name}: max_particles {props.get('max_particles')} outside {MIN_PARTICLES}..{MAX_PARTICLES}")
        how = kinds.get(name, set())
        if "sized" in how:
            issue = check_size_curve(props)
            if issue:
                problems.append(f"{name}: {issue}")
        if "tinted" in how:
            for key in props.get("color_gradient", []):
                r, g, b = key["color"][:3]
                if not (abs(r - g) < 1e-6 and abs(g - b) < 1e-6):
                    problems.append(f"{name}: tinted by the script but its gradient has a hue {key['color'][:3]}")
                    break
    in_core = sorted(n for n in used if n in EFFECTS and n not in weapons.NAMES)
    by_kind = {k: sorted(n for n in used if k in kinds.get(n, set())) for k in ("sized", "tinted", "white", "untinted")}
    print(f"names used by the weapons code: {len(used)} (Find: {len(used) - sum(1 for n in used if n in weapons.NAMES and weapons.EFFECTS[n][0] == 'Trails')}, "
          f"trails: {sum(1 for n in weapons.NAMES if weapons.EFFECTS[n][0] == 'Trails')})")
    print(f"defined in build_effects_weapons.py: {len(weapons.NAMES)}; already in build_effects.py: {', '.join(in_core)}")
    print(f"emitted with a size (curve normalised, lifetime [1,1]): {len(by_kind['sized'])}: {', '.join(by_kind['sized'])}")
    print(f"tinted (neutral gradient): {len(by_kind['tinted'])}; untinted: {len(by_kind['untinted'])}; "
          f"tinted WHITE (gradient carries the colour): {', '.join(by_kind['white'])}")
    print(f"properties reflected but not named in components.md: {', '.join(sorted(undocumented)) or 'none'}")
    if not_static:
        print("defined only at run time (no literal definition):", ", ".join(not_static))
    if problems:
        print(f"\nFAILED ({len(problems)}):")
        for problem in problems:
            print("  " + problem)
        return 1
    print("\nOK: every used name defined exactly once; properties, enums, textures, budgets, sized curves and tints check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
