"""Isolated boss review: python boss_review.py BOSS(0..2) [HERO] [SECONDS] -> shots/boss<k>_sheet.png (8 frames)."""
import json
import sys
import time

from PIL import Image

from playutil import call, start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto

boss = int(sys.argv[1]) if len(sys.argv) > 1 else 2
hero = sys.argv[2] if len(sys.argv) > 2 else "Mage"
seconds = float(sys.argv[3]) if len(sys.argv) > 3 else 24.0
FRAMES = 8
set_auto(True, hero, 20.0)
print("play", start_play())
time.sleep(0.5)
invoke("DevInvulnerable", 1000.0)
invoke("DevAutoplay", True, 1.0)
invoke("DevBossArena", boss, 12.0)
paths = []
for i in range(FRAMES):
    time.sleep(seconds / FRAMES)
    paths.append(capture_game(f"boss{boss}_{i}", 1280, 720))
print(json.loads(invoke("DevReport")).get("result"))
stop_play()
set_auto(False)
sheet = Image.new("RGB", (1280, 360 * (FRAMES // 4)))
for i, path in enumerate(paths):
    sheet.paste(Image.open(path).resize((320, 180)).resize((320, 180)), ((i % 4) * 320, (i // 4) * 180))
sheet = sheet.crop((0, 0, 1280, 180 * (FRAMES // 4)))
sheet.save(f"shots/boss{boss}_sheet.png")
print(f"shots/boss{boss}_sheet.png")
