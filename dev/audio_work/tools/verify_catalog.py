"""Cross-checks SoundCatalog.cs against the library and the game code:
- every key the catalog builds exists under src/Audio;
- every sound name the scripts play (Play / PlayAt / PlaySound / PlaySting / castSound / AddCircle / AddRing) is registered;
- every manifest name (ui, weapons, bosses) is registered; every library file is used by some registration or MusicPlayer."""
import os
import re
import sys

SURV = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(SURV, "..", "data", "Scripts")
AUDIO = os.path.join(SURV, "..", "data", "Audio")
catalog = open(os.path.join(SCRIPTS, "Audio", "SoundCatalog.cs"), encoding="utf-8").read()
music = open(os.path.join(SCRIPTS, "Audio", "MusicPlayer.cs"), encoding="utf-8").read()

registered = {}
for m in re.finditer(r'c\.Add\("(\w+)", "(\w+)", (\d+),', catalog):
    registered[m.group(2)] = (m.group(1), int(m.group(3)))
for m in re.finditer(r'c\.Boss\("(\w+)", (\d+),', catalog):
    registered[m.group(1)] = ("boss", int(m.group(2)))
for m in re.finditer(r'c\.Ui\("(\w+)", (\d+),', catalog):
    registered[m.group(1)] = ("ui", int(m.group(2)))
aliases = dict(re.findall(r'c\.Alias\("(\w+)", "(\w+)"\)', catalog))
problems = []
used_files = set()
for name, (folder, variants) in registered.items():
    files = [f"{folder}/{name}.wav"] if variants <= 1 else [f"{folder}/{name}_{i}.wav" for i in range(1, variants + 1)]
    for f in files:
        used_files.add(f)
        if not os.path.exists(os.path.join(AUDIO, f)):
            problems.append(f"missing file {f} for {name}")
    extra = f"{folder}/{name}_{variants + 1}.wav"
    if os.path.exists(os.path.join(AUDIO, extra)):
        problems.append(f"{name}: library has more variants than registered ({extra})")
for alias, original in aliases.items():
    if original not in registered:
        problems.append(f"alias {alias} -> unknown {original}")
for key in re.findall(r'ROOT \+ "([\w/.]+)"', music):
    used_files.add(key)
    if not os.path.exists(os.path.join(AUDIO, key)):
        problems.append(f"missing music file {key}")
names = set(registered) | set(aliases)

played = {}
patterns = [r'(?:Play|PlayAt|PlaySound|PlaySting)\(([^;]*)', r'castSound\s*=\s*"(\w+)"', r'(?:AddCircle|AddRing)\(([^;]*)']
for root, _, files in os.walk(SCRIPTS):
    if os.path.basename(root) == "Audio":
        continue
    for name in files:
        if not name.endswith(".cs"):
            continue
        text = open(os.path.join(root, name), encoding="utf-8").read()
        for pattern in patterns:
            for m in re.finditer(pattern, text):
                for literal in re.findall(r'"([a-z][a-z0-9_]*)"', m.group(0)):
                    played.setdefault(literal, set()).add(name)
ignore = {"Locomotion"}
for literal, where in sorted(played.items()):
    if literal not in names and literal not in ignore:
        problems.append(f"played but not registered: {literal} ({', '.join(sorted(where))})")

for manifest, section in (("manifest_ui.md", "## 3. Sounds"), ("manifest_weapons.md", "## 2. Sounds"), ("manifest_bosses.md", "## 2. Sounds")):
    text = open(os.path.join(SURV, manifest), encoding="utf-8").read()
    body = text[text.index(section):]
    body = body[:body.index("\n## ", 5)] if "\n## " in body[5:] else body
    for row in re.findall(r'^\| ([a-z][a-z0-9_]+) \|', body, re.M):
        if row not in names:
            problems.append(f"{manifest}: {row} not registered")

for root, _, files in os.walk(AUDIO):
    for name in files:
        rel = os.path.relpath(os.path.join(root, name), AUDIO).replace(os.sep, "/")
        if rel not in used_files:
            problems.append(f"library file not referenced: {rel}")

print(f"{len(registered)} sounds + {len(aliases)} aliases registered; {len(played)} literals in play calls; {len(used_files)} files referenced")
print("\n".join(problems) if problems else "all consistent")
sys.exit(1 if problems else 0)
