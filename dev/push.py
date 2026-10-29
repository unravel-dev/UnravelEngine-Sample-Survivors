"""Makes the open editor take the sources edited under ../data: python push.py scripts|ui [filter...].

scripts: the editor's asset watcher recompiles on its own when a .cs file under data/ changes; this waits for that build
and prints its errors. ui: sends the .rhtml / .rcss files through the editor MCP so open documents reload now.
"""
import os
import sys

from mcp import call

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")


def collect(sub, exts, only):
    out = []
    root = os.path.join(DATA, sub)
    for dirpath, _, files in os.walk(root):
        for name in sorted(files):
            if os.path.splitext(name)[1].lower() not in exts:
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace("\\", "/")
            if only and not any(o in rel for o in only):
                continue
            out.append((rel, full))
    return out


def check_ascii(only):
    for rel, full in collect("Scripts", {".cs"}, only):
        code = open(full, encoding="utf-8").read()
        assert all(ord(c) < 128 for c in code), rel


def wait_scripts(only):
    check_ascii(only)
    print(call("scripts_get_compile_status", {"wait": True, "timeout_ms": 180000})[:3000])


def push_ui(only):
    items = []
    for rel, full in collect("UI", {".rhtml", ".rcss"}, only):
        source = open(full, encoding="utf-8").read()
        assert all(ord(c) < 128 for c in source), rel
        items.append({"key": f"app:/data/UI/{rel}", "source": source})
    print(f"pushing {len(items)} ui files")
    print(call("ui_set_sources_batch", {"items": items})[:1500])


if __name__ == "__main__":
    what = sys.argv[1]
    only = sys.argv[2:]
    if what == "scripts":
        wait_scripts(only)
    elif what == "ui":
        push_ui(only)
