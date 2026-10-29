"""Checks the boss effects against BossFx.cs and its callers (no editor call):

    python check_boss_effects.py

- every name BossFx looks up is defined exactly once across build_effects.py + the weapons and bosses builders;
- every property is reflected, enum values are valid, textures exist (project data or tools/out/*_textures);
- sized calls (Burst with a size, Oriented, Moving, Launch, hazard impacts): exact, normalised size curves and
  lifetime [1, 1]; tinted calls: neutral gradients;
- TelegraphCone is never tinted by any script; SummonMotes holds 1024 particles.
Shared emitters of build_effects.py that the scripts size are listed with their curve problems as warnings.
Exit code 1 when something fails.
"""
import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import check_weapon_effects as common  # noqa: E402

SCRIPTS = os.path.join(HERE, "..", "data", "Scripts")
BOSS_FX = os.path.join(SCRIPTS, "Bosses", "BossFx.cs")
CALLERS = glob.glob(os.path.join(SCRIPTS, "Bosses", "*.cs"))
BUILDERS = common.BUILDERS + [os.path.join(HERE, "build_effects_bosses.py")]
GENERATED = {"app:/data/Textures/weapons/": os.path.join(HERE, "tools", "out", "weapon_textures"),
             "app:/data/Textures/bosses/": os.path.join(HERE, "tools", "out", "boss_textures")}
WHITE_TINTS = {"Color.white", "WHITE"}
SUMMON_MOTES_PARTICLES = 1024


def texture_path(key):
    for prefix, folder in GENERATED.items():
        if key.startswith(prefix):
            return os.path.join(folder, key[len(prefix):])
    return os.path.join(common.PROJECT_DATA, key[len("app:/data/"):].replace("/", os.sep))


def boss_fields():
    """BossFx field -> effect name, and the set of names it looks up."""
    source = common.read(BOSS_FX)
    constants = dict(re.findall(r'const string (\w+) = "(\w+)"', source))
    fields = {field: constants[constant] for field, constant in re.findall(r'\b(\w+) = Find\((\w+)\)', source)
              if constant in constants}
    return fields, set(fields.values())


def scan_calls(fields):
    """name -> set of kinds ('sized', 'tinted', 'white', 'untinted')."""
    kinds = {}

    def add(name, kind):
        kinds.setdefault(name, set()).update(kind)

    def tint(color):
        return {"white"} if color in WHITE_TINTS else {"tinted"}

    for path in CALLERS:
        source = common.read(path)
        for match in re.finditer(r'\b(Burst|Oriented|Moving|Launch|AddCircle)\s*\(', source):
            arguments = common.split_arguments(common.call_arguments(source, match.end() - 1))
            method = match.group(1)
            target = arguments[5] if method == "AddCircle" and len(arguments) > 6 else (arguments[0] if arguments else "")
            name = fields.get(target.replace("fx.", "").strip())
            if name is None:
                continue
            if method == "AddCircle":
                add(name, {"sized"} | tint(arguments[6]))
            elif method == "Burst":
                add(name, ({"sized"} | tint(arguments[3])) if len(arguments) >= 6 else {"untinted"})
            elif method == "Oriented":
                add(name, {"sized"} | tint(arguments[3]))
            elif method == "Moving":
                add(name, {"sized", "untinted"})
            else:
                add(name, {"sized"} | tint(arguments[3]))
    return kinds


def telegraph_cone_tints():
    """Every colour argument any script passes to TelegraphCone (Effects or BossFx calls)."""
    found = []
    for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
        source = common.read(path)
        variables = {v for v, n in re.findall(r'(\w+)\s*=\s*(?:\w+\.)*(?:effects\.)?Find\("(\w+)"\)', source)
                     if n == "TelegraphCone"} | {"telegraphCone", "fx.telegraphCone"}
        for match in re.finditer(r'\b(EmitShape|EmitMoving|Emit|Oriented|Burst|Launch)\s*\(', source):
            arguments = common.split_arguments(common.call_arguments(source, match.end() - 1))
            if not arguments or arguments[0] not in variables:
                continue
            method = match.group(1)
            color = {"EmitMoving": None, "Emit": arguments[3] if len(arguments) >= 4 else None,
                     "EmitShape": arguments[2] if len(arguments) > 2 else None}.get(method, arguments[3] if len(arguments) > 3 else None)
            found.append((os.path.relpath(path, SCRIPTS), method, color))
    return found


def static_counts():
    counts = {}
    for path in BUILDERS:
        for name in re.findall(r'\b(?:effect|define)\(\s*"\w+",\s*"(\w+)"', common.read(path)):
            counts[name] = counts.get(name, 0) + 1
    return counts


def check_effect(name, props, kinds, documented, reflected, undocumented):
    problems = []
    for key, value in props.items():
        if key not in reflected:
            problems.append(f"{name}: unknown property {key}")
        elif key not in documented:
            undocumented.add(key)
        if key in common.ENUMS and value not in common.ENUMS[key]:
            problems.append(f"{name}: {key} = {value!r} is not one of {sorted(common.ENUMS[key])}")
    texture = props.get("texture")
    if texture and not os.path.exists(texture_path(texture)):
        problems.append(f"{name}: texture not found {texture}")
    if props.get("max_particles", 0) <= 0:
        problems.append(f"{name}: no max_particles")
    how = kinds.get(name, set())
    if "sized" in how:
        issue = common.check_size_curve(props)
        if issue:
            problems.append(f"{name}: {issue}")
    if "tinted" in how:
        for key in props.get("color_gradient", []):
            r, g, b = key["color"][:3]
            if not (abs(r - g) < 1e-6 and abs(g - b) < 1e-6):
                problems.append(f"{name}: tinted by the script but its gradient has a hue {key['color'][:3]}")
                break
    return problems


def main():
    import build_effects_weapons  # noqa: F401 (the builders share build_effects.EFFECTS)
    import build_effects_bosses as bosses
    from build_effects import EFFECTS
    fields, used = boss_fields()
    kinds = scan_calls(fields)
    counts = static_counts()
    documented, reflected = common.documented_names(), common.reflected_names()
    undocumented = set()
    problems = [f"missing: {n}" for n in sorted(used) if n not in EFFECTS]
    problems += [f"defined {counts[n]} times: {n}" for n in sorted(used) if counts.get(n, 0) > 1]
    problems += [f"defined here but never looked up: {n}" for n in bosses.NAMES if n not in used]
    for name in bosses.NAMES:
        problems += check_effect(name, EFFECTS[name][1], kinds, documented, reflected, undocumented)
    summon = EFFECTS["SummonMotes"][1].get("max_particles")
    if summon != SUMMON_MOTES_PARTICLES:
        problems.append(f"SummonMotes: max_particles {summon}, expected {SUMMON_MOTES_PARTICLES}")
    cone_calls = telegraph_cone_tints()
    tinted_cone = [c for c in cone_calls if c[2] not in (None,) and c[2] not in WHITE_TINTS]
    problems += [f"TelegraphCone tinted with {color} in {path} ({method})" for path, method, color in tinted_cone]
    warnings = []
    shared = sorted(n for n in used if n not in bosses.NAMES)
    for name in shared:
        if "sized" in kinds.get(name, set()) and name in EFFECTS:
            issue = common.check_size_curve(EFFECTS[name][1])
            if issue:
                warnings.append(f"{name} (build_effects.py): {issue}")
    print(f"names BossFx looks up: {len(used)}; defined in build_effects_bosses.py: {len(bosses.NAMES)}; "
          f"shared from build_effects.py: {', '.join(shared)}")
    print("sized by the scripts:", ", ".join(sorted(n for n in used if "sized" in kinds.get(n, set()))))
    print("tinted:", ", ".join(sorted(n for n in used if "tinted" in kinds.get(n, set()))))
    print("white / untinted:", ", ".join(sorted(n for n in used if kinds.get(n, set()) & {"white", "untinted"}
                                                and "tinted" not in kinds.get(n, set()))))
    print("TelegraphCone calls:", "; ".join(f"{p} {m} color={c}" for p, m, c in cone_calls) or "none")
    print(f"SummonMotes max_particles: {summon}")
    print(f"properties reflected but not named in components.md: {', '.join(sorted(undocumented)) or 'none'}")
    if warnings:
        print("\nWARNINGS (shared emitters in build_effects.py whose scripted size is not exact):")
        for warning in warnings:
            print("  " + warning)
    if problems:
        print(f"\nFAILED ({len(problems)}):")
        for problem in problems:
            print("  " + problem)
        return 1
    print("\nOK: every BossFx name defined exactly once; properties, enums, textures, sized curves, tints, "
          "TelegraphCone untinted, SummonMotes 1024.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
