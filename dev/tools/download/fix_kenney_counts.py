# Usage: python -I fix_kenney_counts.py <dest_root> ; replaces the '?' left by the non-ASCII multiply sign
import glob, os, re, sys
for path in glob.glob(os.path.join(sys.argv[1], "kenney_*", "SOURCE_LICENSE.txt")):
    text = open(path, encoding="ascii").read()
    fixed = re.sub(r"^(page_files_count: \d+)\?$", r"\1 files", text, flags=re.M)
    if fixed != text:
        open(path, "w", encoding="ascii", newline="\n").write(fixed)
        print("fixed", path)
