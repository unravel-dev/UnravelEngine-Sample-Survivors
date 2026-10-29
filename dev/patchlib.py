"""Exact-text patching that keeps a file's line endings: edit(path, [(old, new), ...]) asserts each old occurs once."""


def edit(path, pairs):
    with open(path, newline="") as f:
        text = f.read()
    newline = "\r\n" if "\r\n" in text else "\n"
    for old, new in pairs:
        old = old.replace("\n", newline)
        new = new.replace("\n", newline)
        count = text.count(old)
        if count != 1:
            raise AssertionError(f"{path}: expected one match, found {count}:\n{old}")
        text = text.replace(old, new)
    with open(path, "w", newline="") as f:
        f.write(text)
