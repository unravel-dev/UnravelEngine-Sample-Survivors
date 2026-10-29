"""Class active and signature review in slow motion: python actives.py [Hero ...] -> shots/gallery/active_<hero>.png"""
import os
import sys
import time

from PIL import Image

from playutil import start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto

WEAPON = {"Knight": "oathblade_arc", "Barbarian": "ravager_chop", "Mage": "ember_orb", "Rogue": "repeater_crossbow"}
TILE = (480, 270)


def shoot(tag):
    return Image.open(capture_game(tag, 1280, 720)).crop((200, 112, 1080, 607)).resize(TILE)


def main():
    for hero in sys.argv[1:] or list(WEAPON):
        set_auto(True, hero, 20.0)
        start_play()
        time.sleep(0.4)
        invoke("DevInvulnerable", 1000.0)
        invoke("DevCamera", 50.0, 12.0, [0.0, 0.0, 3.0])
        invoke("DevWeapon", WEAPON[hero], 1, False)
        invoke("DevCrowd", 50, 4.0, 11.0, 60.0, 110.0)
        invoke("DevFillSignature")
        time.sleep(0.6)
        invoke("DevTimeScale", 0.25)
        sheet = Image.new("RGB", (TILE[0] * 3, TILE[1] * 3))
        sheet.paste(shoot(f"act_{hero}_sig"), (0, 0))
        invoke("DevActivate")
        for i in range(8):
            time.sleep(0.45)
            k = i + 1
            sheet.paste(shoot(f"act_{hero}_{i}"), ((k % 3) * TILE[0], (k // 3) * TILE[1]))
        sheet.save(os.path.join("shots", "gallery", f"active_{hero}.png"))
        print(hero, "done")
        stop_play()
        set_auto(False)


if __name__ == "__main__":
    main()
