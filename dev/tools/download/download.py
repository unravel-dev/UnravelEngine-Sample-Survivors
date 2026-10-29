# Usage: python -I download.py <dest_root> [pack ...]
# Downloads the approved survivors asset packs into <dest_root>/<pack>/ (one new empty
# folder per pack), keeps the archive, extracts zips safely into <pack>/extracted/ and
# writes <pack>/SOURCE_LICENSE.txt with the source page license line.
# Run from a working directory outside <dest_root>. Prints one JSON status line per pack.
import hashlib
import html
import http.cookiejar
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request
import zipfile

USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36"
CHUNK_SIZE = 1 << 16
MAX_UNPACKED_BYTES = 1 << 30
MAX_MEMBERS = 20000
TIMEOUT_SECONDS = 120
GH = "https://github.com/KayKit-Game-Assets/"
OGA_FILES = "https://opengameart.org/sites/default/files/"
OGA_CONTENT = "https://opengameart.org/content/"
FONTS = "https://raw.githubusercontent.com/google/fonts/main/ofl/"

PACKS = [
    {"name": "skeletons", "kind": "github", "repo": "KayKit-Character-Pack-Skeletons-1.0",
     "commit": "15b62b9bad122f72926c10fb14d622c73819fa54", "unpacked": 19633386, "est_zip": 6400000},
    {"name": "halloween_bits", "kind": "github", "repo": "KayKit-Halloween-Bits-1.0",
     "commit": "6dc69bf6b2fa766a985754f35ec6a0324090e6c6", "unpacked": 3269808, "est_zip": 1200000},
    {"name": "dungeon_remastered", "kind": "github", "repo": "KayKit-Dungeon-Remastered-1.0",
     "commit": "b0ca9bd96a8072ab36a3a5464f00ed1e06a16d07", "unpacked": 17013515, "est_zip": 5000000},
    {"name": "sfx_80_rpg", "kind": "oga", "file": "80-CC0-RPG-SFX_0.zip", "page": "80-cc0-rpg-sfx", "size": 1845114},
    {"name": "sfx_rpg_sound_pack", "kind": "oga", "file": "rpg_sound_pack.zip", "page": "rpg-sound-pack", "size": 12465564},
    {"name": "sfx_swishes", "kind": "oga", "file": "swishes.zip", "page": "swishes-sound-pack", "size": 385150},
    {"name": "sfx_bone_breaks", "kind": "oga", "file": "deep_breaks.zip", "page": "deep-bone-crackbreak-sfx", "size": 2610684},
    {"name": "sfx_bones_rattle", "kind": "oga", "file": "bones_rattle.zip", "page": "bones-rattle", "size": 49966},
    {"name": "sfx_gem", "kind": "oga", "file": "gem-gather-stereo.wav", "page": "gem-collect-sfx", "size": 50828},
    {"name": "sfx_soundpack01", "kind": "oga", "file": "SoundPack01.zip", "page": "level-up-power-up-coin-get-13-sounds", "size": 6780792},
    {"name": "kenney_impact_sounds", "kind": "kenney", "slug": "impact-sounds", "size": 800850},
    {"name": "kenney_interface_sounds", "kind": "kenney", "slug": "interface-sounds", "size": 834536},
    {"name": "kenney_scifi_sounds", "kind": "kenney", "slug": "sci-fi-sounds", "size": 5875104},
    {"name": "kenney_music_jingles", "kind": "kenney", "slug": "music-jingles", "size": 1239525},
    {"name": "kenney_rpg_audio", "kind": "kenney", "slug": "rpg-audio", "size": 964837},
    {"name": "kenney_particle_pack", "kind": "kenney", "slug": "particle-pack", "size": 15001764},
    {"name": "kenney_smoke_particles", "kind": "kenney", "slug": "smoke-particles", "size": 6019666},
    {"name": "music_battle", "kind": "oga", "file": "determined_pursuit_loop.wav", "page": "determined-pursuit-epic-orchestra-loop", "size": 19052830},
    {"name": "music_boss", "kind": "oga", "file": "heavy_boss_battle_1_bpm200_0.ogg", "page": "heavy-boss-battle-1", "size": 3360172},
    {"name": "music_menu", "kind": "oga", "file": "qubodup-yd-DarkShrineLoop-OpenGameArt.ogg", "page": "dark-shrine-loop", "size": 678630},
    {"name": "fonts", "kind": "fonts", "files": [
        (FONTS + "pirataone/PirataOne-Regular.ttf", "PirataOne-Regular.ttf", 56316),
        (FONTS + "pirataone/OFL.txt", "OFL_PirataOne.txt", 4427),
        (FONTS + "lilitaone/LilitaOne-Regular.ttf", "LilitaOne-Regular.ttf", 28092),
        (FONTS + "lilitaone/OFL.txt", "OFL_LilitaOne.txt", 4392)]},
]

LICENSE_NAME = re.compile(r"(licen[cs]e|copying|ofl|readme|credits)", re.I)


def make_opener():
    jar = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    opener.addheaders = [("User-Agent", USER_AGENT)]
    return opener


def fetch_text(opener, url, referer=None):
    req = urllib.request.Request(url)
    if referer:
        req.add_header("Referer", referer)
    with opener.open(req, timeout=TIMEOUT_SECONDS) as resp:
        return resp.read().decode("utf-8", errors="replace")


def download_file(opener, url, path, referer=None):
    """Stream url to path; returns (bytes, sha256, final_url, content_disposition)."""
    req = urllib.request.Request(url)
    if referer:
        req.add_header("Referer", referer)
    digest = hashlib.sha256()
    total = 0
    with opener.open(req, timeout=TIMEOUT_SECONDS) as resp, open(path, "xb") as out:
        final_url = resp.geturl()
        disposition = resp.headers.get("Content-Disposition", "")
        while True:
            block = resp.read(CHUNK_SIZE)
            if not block:
                break
            out.write(block)
            digest.update(block)
            total += len(block)
    return total, digest.hexdigest(), final_url, disposition


def make_pack_dir(root, name):
    path = os.path.join(root, name)
    if os.path.exists(path) and os.listdir(path):
        raise RuntimeError("destination exists and is not empty: " + path)
    os.makedirs(path, exist_ok=True)
    return path


def is_safe_member(name):
    if not name or name.startswith(("/", "\\")) or re.match(r"^[A-Za-z]:", name):
        return False
    parts = re.split(r"[\\/]+", name)
    return all(p not in ("..",) and ":" not in p for p in parts)


def extract_zip(archive, pack_dir):
    """Extract archive into pack_dir/extracted; every member must stay inside it."""
    target = os.path.realpath(os.path.join(pack_dir, "extracted"))
    os.makedirs(target, exist_ok=False)
    files = 0
    unpacked = 0
    with zipfile.ZipFile(archive) as zf:
        infos = zf.infolist()
        if len(infos) > MAX_MEMBERS:
            raise RuntimeError("too many members: %d" % len(infos))
        if sum(i.file_size for i in infos) > MAX_UNPACKED_BYTES:
            raise RuntimeError("unpacked size too large")
        for info in infos:
            if not is_safe_member(info.filename):
                raise RuntimeError("unsafe member path: %r" % info.filename)
            mode = (info.external_attr >> 16) & 0o170000
            if mode == 0o120000:
                raise RuntimeError("symlink member: %r" % info.filename)
            dest = os.path.realpath(os.path.join(target, info.filename))
            if os.path.commonpath([target, dest]) != target:
                raise RuntimeError("member escapes target: %r" % info.filename)
            if info.is_dir():
                os.makedirs(dest, exist_ok=True)
                continue
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with zf.open(info) as src, open(dest, "xb") as out:
                while True:
                    block = src.read(CHUNK_SIZE)
                    if not block:
                        break
                    out.write(block)
                    unpacked += len(block)
            files += 1
    return files, unpacked


def find_license_files(pack_dir):
    found = []
    for base, _dirs, names in os.walk(pack_dir):
        for n in names:
            if LICENSE_NAME.search(n) and n != "SOURCE_LICENSE.txt":
                found.append(os.path.relpath(os.path.join(base, n), pack_dir).replace("\\", "/"))
    return sorted(found)


def strip_tags(s):
    return re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]+>", " ", s))).strip()


def oga_page_info(opener, page_url, file_url):
    s = fetch_text(opener, page_url)
    title = re.search(r"<title>([^<|]*)", s)
    author = re.search(r"field-name-author-submitter.*?<a[^>]*>([^<]+)</a>", s, re.S)
    lic = re.search(r"field-name-field-art-licenses(.*?)field-name-collect", s, re.S)
    names = re.findall(r"class=.license-name.>([^<]+)<", lic.group(1)) if lic else []
    hrefs = re.findall(r"<a[^>]*href=['\"](https?://creativecommons[^'\"]+)['\"]", lic.group(1)) if lic else []
    listed = re.findall(r"<a href=\"(https://opengameart.org/sites/default/files/[^\"]+)\"[^>]*length=(\d+)", s)
    listed_size = next((int(n) for u, n in listed if u == file_url), None)
    return {
        "title": html.unescape(title.group(1).strip()) if title else None,
        "author": author.group(1) if author else None,
        "license_line": "License(s): " + ", ".join(names) + (" (" + ", ".join(hrefs) + ")" if hrefs else ""),
        "file_listed": listed_size is not None,
        "listed_size": listed_size,
    }


def kenney_page_info(opener, page_url):
    s = fetch_text(opener, page_url)
    lic = re.search(r"License</td>\s*<td[^>]*>(.*?)</td>", s, re.S)
    href = re.search(r"href='([^']+)'", lic.group(1)) if lic else None
    files = re.search(r"Files</td>\s*<td[^>]*>([^<]*)</td>", s, re.S)
    title = re.search(r"<title>([^<]*)", s)
    dl = re.search(r"id='donate-text'[^>]*data-download-id='([0-9a-f]+)'", s)
    return {
        "title": html.unescape(title.group(1).strip()) if title else None,
        "license_line": "License: " + (strip_tags(lic.group(1)) if lic else "NOT FOUND") + (" (" + href.group(1) + ")" if href else ""),
        "files_count": files.group(1).strip() if files else None,
        "download_id": dl.group(1) if dl else None,
    }


def github_page_info(opener, pack):
    api = json.loads(fetch_text(opener, "https://api.github.com/repos/KayKit-Game-Assets/" + pack["repo"]))
    lic = api.get("license") or {}
    line = "GitHub repo license: %s (%s)" % (lic.get("name"), lic.get("spdx_id")) if lic else "GitHub repo license: not detected"
    readme_lines = []
    try:
        readme = fetch_text(opener, "https://raw.githubusercontent.com/KayKit-Game-Assets/%s/%s/README.md" % (pack["repo"], pack["commit"]))
        readme_lines = [ln.strip() for ln in readme.splitlines() if re.search(r"licen|cc0|creative commons|public domain", ln, re.I)]
    except Exception as err:  # README is optional context
        readme_lines = ["README fetch failed: %s" % err]
    return {"title": api.get("full_name"), "license_line": line, "readme_license_lines": readme_lines[:5]}


def write_source(pack_dir, record):
    lines = ["%s: %s" % (k, v) for k, v in record.items()]
    with open(os.path.join(pack_dir, "SOURCE_LICENSE.txt"), "x", encoding="ascii", errors="replace") as out:
        out.write("\n".join(lines) + "\n")


def filename_from(disposition, url):
    m = re.search(r"filename\*?=(?:UTF-8'')?\"?([^\";]+)", disposition or "")
    name = m.group(1) if m else os.path.basename(urllib.parse.urlparse(url).path)
    name = os.path.basename(urllib.parse.unquote(name).replace("\\", "/"))
    if not re.match(r"^[A-Za-z0-9._ -]+$", name):
        raise RuntimeError("unexpected file name: %r" % name)
    return name


def run_github(opener, root, pack):
    pack_dir = make_pack_dir(root, pack["name"])
    url = GH + pack["repo"] + "/archive/" + pack["commit"] + ".zip"
    page = github_page_info(opener, pack)
    archive = os.path.join(pack_dir, pack["name"] + ".zip")
    size, sha, final_url, _ = download_file(opener, url, archive)
    files, unpacked = extract_zip(archive, pack_dir)
    rec = {"pack": pack["name"], "download_url": url, "final_url": final_url, "bytes": size, "sha256": sha,
           "source_page": GH + pack["repo"], "page_license_line": page["license_line"],
           "readme_license_lines": " | ".join(page["readme_license_lines"]),
           "expected": "zip est. %d B, unpacked exactly %d B" % (pack["est_zip"], pack["unpacked"]),
           "extracted_files": files, "extracted_bytes": unpacked, "downloaded": time.strftime("%Y-%m-%d %H:%M")}
    rec["size_ok"] = unpacked == pack["unpacked"]
    return pack_dir, rec


def run_oga(opener, root, pack):
    pack_dir = make_pack_dir(root, pack["name"])
    url = OGA_FILES + pack["file"]
    page_url = OGA_CONTENT + pack["page"]
    page = oga_page_info(opener, page_url, url)
    path = os.path.join(pack_dir, pack["file"])
    size, sha, final_url, _ = download_file(opener, url, path, referer=page_url)
    rec = {"pack": pack["name"], "download_url": url, "final_url": final_url, "bytes": size, "sha256": sha,
           "source_page": page_url, "page_title": page["title"], "page_author": page["author"],
           "page_license_line": page["license_line"], "file_listed_on_page": page["file_listed"],
           "page_listed_size": page["listed_size"], "expected": pack["size"],
           "downloaded": time.strftime("%Y-%m-%d %H:%M")}
    rec["size_ok"] = size == pack["size"]
    if path.lower().endswith(".zip"):
        rec["extracted_files"], rec["extracted_bytes"] = extract_zip(path, pack_dir)
    return pack_dir, rec


def run_kenney(opener, root, pack):
    page_url = "https://kenney.nl/assets/" + pack["slug"]
    page = kenney_page_info(opener, page_url)
    if not page["download_id"]:
        raise RuntimeError("download id not found on " + page_url)
    link = json.loads(fetch_text(opener, "https://kenney.nl/download-link/" + page["download_id"], referer=page_url))
    signed = link.get("url")
    if not signed:
        raise RuntimeError("no signed url from download-link: %r" % link)
    pack_dir = make_pack_dir(root, pack["name"])
    tmp = os.path.join(pack_dir, "download.part")
    size, sha, final_url, disposition = download_file(opener, signed, tmp, referer=page_url)
    name = filename_from(disposition, final_url)
    path = os.path.join(pack_dir, name)
    os.rename(tmp, path)
    rec = {"pack": pack["name"], "download_url": page_url + " (Download -> Continue without donating -> signed link)",
           "final_url": urllib.parse.urlparse(final_url)._replace(query="").geturl(), "bytes": size, "sha256": sha,
           "file": name, "source_page": page_url, "page_title": page["title"], "page_files_count": page["files_count"],
           "page_license_line": page["license_line"], "expected": pack["size"],
           "downloaded": time.strftime("%Y-%m-%d %H:%M")}
    rec["size_ok"] = size == pack["size"]
    if name.lower().endswith(".zip"):
        rec["extracted_files"], rec["extracted_bytes"] = extract_zip(path, pack_dir)
    return pack_dir, rec


def run_fonts(opener, root, pack):
    pack_dir = make_pack_dir(root, pack["name"])
    rec = {"pack": pack["name"], "downloaded": time.strftime("%Y-%m-%d %H:%M"), "size_ok": True, "bytes": 0}
    for url, name, expected in pack["files"]:
        size, sha, _final, _ = download_file(opener, url, os.path.join(pack_dir, name))
        rec[name] = "%s | %d B (expected %d) | sha256 %s" % (url, size, expected, sha)
        rec["bytes"] += size
        rec["size_ok"] = rec["size_ok"] and size == expected
    lines = []
    for ofl in ("OFL_PirataOne.txt", "OFL_LilitaOne.txt"):
        text = open(os.path.join(pack_dir, ofl), encoding="utf-8", errors="replace").read().splitlines()
        lines.append(ofl + ": " + " / ".join(ln.strip() for ln in text[:6] if ln.strip()))
    rec["source_page"] = "https://github.com/google/fonts/tree/main/ofl/pirataone , https://github.com/google/fonts/tree/main/ofl/lilitaone"
    rec["page_license_line"] = " || ".join(lines)
    return pack_dir, rec


RUNNERS = {"github": run_github, "oga": run_oga, "kenney": run_kenney, "fonts": run_fonts}


def main():
    root = os.path.realpath(sys.argv[1])
    wanted = set(sys.argv[2:])
    if os.path.commonpath([root, os.path.realpath(os.getcwd())]) == root:
        raise SystemExit("run from a directory outside the download root")
    os.makedirs(root, exist_ok=True)
    opener = make_opener()
    for pack in PACKS:
        if wanted and pack["name"] not in wanted:
            continue
        try:
            pack_dir, rec = RUNNERS[pack["kind"]](opener, root, pack)
            rec["license_files"] = ", ".join(find_license_files(pack_dir))
            write_source(pack_dir, rec)
            print(json.dumps({"status": "ok", **rec}), flush=True)
        except Exception as err:
            print(json.dumps({"status": "failed", "pack": pack["name"], "error": "%s: %s" % (type(err).__name__, err)}), flush=True)


if __name__ == "__main__":
    main()
