"""Shared helpers of the effect builders (build_effects_weapons.py, build_effects_bosses.py).

Scripted sizes: EmitParams.size is the particle's size AT BIRTH (the engine scales size_over_life so its birth value is
the size); a curve that starts at 0 is scaled to PEAK at it instead. So a scripted curve is written either with
birth 1.0 or starting at 0 with peak 1.0; from_zero() turns "grow from s0 to 1" into the second form.
"""
import sys

from build_effects import EFFECTS, effect, burst, keys, WHITE, from_zero, FROM_ZERO_PROGRESS  # noqa: F401
import build_effects


class Registry:
    """The effects one builder defines: refuses duplicates (also against build_effects.py) and unknown groups."""

    def __init__(self, groups):
        self.groups = tuple(groups)
        self.names = []

    def define(self, group, name, props, sort):
        if group not in self.groups:
            raise ValueError(f"{name}: unknown group {group}")
        if name in EFFECTS:
            raise ValueError(f"{name} is defined twice")
        effect(group, name, props, sort)
        self.names.append(name)

    def print_names(self, label):
        for group in self.groups:
            members = [n for n in self.names if EFFECTS[n][0] == group]
            print(f"{group} ({len(members)}): {', '.join(members)}")
        print(f"effects defined in {label}:", len(self.names))

    def main(self, label, argv=None):
        """`build [group...]` applies through the editor; anything else only lists the names."""
        argv = sys.argv[1:] if argv is None else argv
        if argv and argv[0] == "build":
            wanted = argv[1:]
            build_effects.apply([n for n in self.names if not wanted or EFFECTS[n][0] in wanted])
        else:
            self.print_names(label)


def shot(**props):
    """A burst-only emitter (build_effects.burst) launched at start_speed (default 0) along its shape's direction."""
    props.setdefault("start_speed", [0.0, 0.0])
    base = burst(**props)
    base.pop("velocity_gradient", None)
    if "shape_scale" in props:
        base.pop("emission_shape_scale", None)
    return base


def sizes(*points):
    """size_over_life keys: (progress, size) or (progress, min, max)."""
    return [{"progress": p[0], "min": p[1], "max": p[-1]} for p in points]


def scalar(*points):
    """A float curve (intensity_over_life, speed_over_life): (progress, value) keys."""
    return [{"progress": p, "value": v} for p, v in points]


def alpha(*points, rgb=WHITE):
    """A colour gradient of one colour with an alpha shape: (progress, alpha) keys."""
    return keys(*[(p, list(rgb) + [a]) for p, a in points])




def grow(at=0.35):
    """Scripted rings: start at 0 so the passed size is the PEAK, burst out (out-expo like) and settle at 1."""
    return sizes((0.0, 0.0), (0.05, 0.28), (at, 0.82), (1.0, 1.0))
