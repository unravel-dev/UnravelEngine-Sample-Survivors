"""Weapon effect gallery in slow motion: python gallery.py HERO weapon_id [weapon_id ...] -> shots/gallery/<id>.png

Each sheet: rank 1 on the top two rows, evolved on the bottom two; frames 0.2 s of game time apart from the first
cast (the game runs at TIME_SCALE), center crops around the hero.
"""
import json
import os
import sys
import time

from PIL import Image

from playutil import call, start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "shots", "gallery")
COLUMNS = 3
ROWS_PER_VARIANT = 2
CROP = (200, 112, 1080, 607)
TILE = (480, 270)
TIME_SCALE = 0.25
GAP = 0.8


def shoot(tag):
    path = capture_game(tag, 1280, 720)
    return Image.open(path).crop(CROP).resize(TILE)


def main():
    hero = sys.argv[1]
    weapons = sys.argv[2:]
    os.makedirs(OUT, exist_ok=True)
    set_auto(True, hero, 20.0)
    print("play", start_play())
    time.sleep(0.4)
    invoke("DevInvulnerable", 10000.0)
    invoke("DevCamera", 50.0, float(os.environ.get("GALLERY_DISTANCE", "11")), [0.0, 0.0, float(os.environ.get("GALLERY_FOCUS", "6.5"))])
    frames = COLUMNS * ROWS_PER_VARIANT
    for weapon in weapons:
        sheet = Image.new("RGB", (TILE[0] * COLUMNS, TILE[1] * ROWS_PER_VARIANT * 2))
        for variant, (rank, evolve) in enumerate(((1, False), (8, True))):
            invoke("DevTimeScale", 1.0)
            invoke("DevCrowd", 60, float(os.environ.get("GALLERY_INNER", "5")), 12.0, 60.0, 110.0)
            time.sleep(0.3)
            invoke("DevTimeScale", TIME_SCALE)
            print(weapon, rank, evolve, invoke("DevWeapon", weapon, rank, evolve)[:40])
            for frame in range(frames):
                row = variant * ROWS_PER_VARIANT + frame // COLUMNS
                sheet.paste(shoot(f"g_{weapon}_{variant}_{frame}"), ((frame % COLUMNS) * TILE[0], row * TILE[1]))
                time.sleep(GAP)
        target = os.path.join(OUT, f"{weapon}.png")
        sheet.save(target)
        print(target)
    stop_play()
    set_auto(False)


if __name__ == "__main__":
    main()
