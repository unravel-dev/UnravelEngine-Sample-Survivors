"""
Approximate mockups of the HUD, the level-up picker and hero select at 1920 x 1080, drawn with PIL from the real
icons, fonts and colours of the RCSS (RmlUi itself only runs in the editor). Output: tools/ui/mockup_*.png.

Usage: python render_mockups.py
"""
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
UI = os.path.normpath(os.path.join(HERE, "..", "..", "..", "data", "UI"))
W, H = 1920, 1080
RARITY = {"common": (184, 184, 184), "uncommon": (76, 203, 106), "rare": (76, 141, 255), "epic": (176, 92, 255),
          "evolution": (255, 201, 76)}


def font(name, size):
    return ImageFont.truetype(os.path.join(UI, "Fonts", name), size)


def icon(name, size):
    return Image.open(os.path.join(UI, "Icons", name + ".png")).convert("RGBA").resize((size, size), Image.LANCZOS)


def world_background():
    rng = np.random.default_rng(3)
    base = np.zeros((H, W, 3), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    base[:, :, 0] = 38 + 10 * np.sin(xx / 210.0) + 8 * np.cos(yy / 160.0)
    base[:, :, 1] = 44 + 10 * np.sin(xx / 260.0 + 1.0)
    base[:, :, 2] = 40 + 6 * np.cos(yy / 120.0)
    base += rng.normal(0, 6, (H, W, 1))
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img)
    random.seed(4)
    for _ in range(420):
        x = random.uniform(0, W)
        y = random.uniform(120, H)
        if abs(x - W / 2) < 120 and abs(y - H / 2) < 120:
            continue
        r = random.uniform(9, 14)
        d.ellipse((x - r, y - r * 1.6, x + r, y + r * 0.6), fill=(214, 206, 186, 255))
        d.ellipse((x - r * 0.5, y - r * 1.3, x - r * 0.1, y - r * 0.9), fill=(30, 20, 20, 255))
        d.ellipse((x + r * 0.1, y - r * 1.3, x + r * 0.5, y - r * 0.9), fill=(30, 20, 20, 255))
    d.ellipse((W / 2 - 26, H / 2 - 70, W / 2 + 26, H / 2 + 10), fill=(200, 205, 215, 255))
    d.ellipse((W / 2 - 60, H / 2 + 10, W / 2 + 60, H / 2 + 40), outline=(90, 220, 120, 255), width=4)
    return img


def text(d, xy, s, f, fill, anchor="la", outline=2, outline_fill=(8, 6, 12)):
    d.text(xy, s, font=f, fill=fill, anchor=anchor, stroke_width=outline, stroke_fill=outline_fill)


def draw_hud(img, slots_w, slots_p):
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((-20, -20, 76, 36), radius=10, fill=(26, 20, 34, 240), outline=(201, 164, 92), width=2)
    text(d, (14, 20), "LV", font("Inter-ExtraBold.ttf", 12), (201, 164, 92), "lm", 0)
    text(d, (52, 18), "21", font("Inter-Black.ttf", 24), (255, 255, 255), "mm")
    d.rounded_rectangle((84, 9, W - 12, 23), radius=7, fill=(12, 10, 18, 220))
    d.rounded_rectangle((84, 9, 84 + int((W - 96) * 0.62), 23), radius=7, fill=(70, 170, 250))
    d.line((84 + int((W - 96) * 0.62), 9, 84 + int((W - 96) * 0.62), 23), fill=(232, 248, 255), width=2)
    text(d, (W / 2, 62), "08:42", font("Inter-Black.ttf", 50), (255, 255, 255), "mm", 3)
    img.alpha_composite(icon("skull", 24), (int(W / 2 - 52), 92))
    text(d, (W / 2 - 22, 104), "3,417", font("Inter-Black.ttf", 22), (236, 228, 212), "lm")
    img.alpha_composite(icon("gold", 30), (W - 220, 46))
    text(d, (W - 184, 61), "1,284", font("Inter-Black.ttf", 26), (255, 217, 138), "lm")
    d.rounded_rectangle((W - 64, 40, W - 20, 84), radius=8, fill=(44, 36, 54, 210), outline=(90, 74, 50), width=2)
    d.rectangle((W - 50, 52, W - 45, 72), fill=(236, 228, 212))
    d.rectangle((W - 39, 52, W - 34, 72), fill=(236, 228, 212))
    y = 50
    for i, (name, rank, maxrank, evolved) in enumerate(slots_w + [(None, 0, 0, False)] * (6 - len(slots_w))):
        if name is None:
            d.rounded_rectangle((16, y, 64, y + 48), radius=9, outline=(236, 228, 212, 40), fill=(12, 10, 18, 110))
        else:
            img.alpha_composite(icon(name, 48), (16, y))
            if evolved:
                d.rounded_rectangle((14, y - 2, 66, y + 50), radius=10, outline=(255, 201, 76), width=2)
                d.ellipse((52, y + 36, 70, y + 54), fill=(255, 201, 76))
                text(d, (61, y + 45), "E", font("Inter-Black.ttf", 12), (28, 16, 6), "mm", 0)
            else:
                for p in range(maxrank):
                    cx = 19 + p * 5.6 + 2
                    on = p < rank
                    d.polygon([(cx, y + 52), (cx + 2.2, y + 54.2), (cx, y + 56.4), (cx - 2.2, y + 54.2)],
                              fill=(255, 201, 76) if on else (42, 36, 48))
        y += 58
    y += 10
    for name, rank in slots_p + [(None, 0)] * (6 - len(slots_p)):
        if name is None:
            d.rounded_rectangle((16, y, 58, y + 42), radius=8, outline=(236, 228, 212, 40), fill=(12, 10, 18, 110))
        else:
            img.alpha_composite(icon(name, 42), (16, y))
            for p in range(5):
                cx = 19 + p * 7 + 2
                d.polygon([(cx, y + 46), (cx + 2.2, y + 48.2), (cx, y + 50.4), (cx - 2.2, y + 48.2)],
                          fill=(255, 201, 76) if p < rank else (42, 36, 48))
        y += 52
    ax = W / 2 - 36
    img.alpha_composite(icon("shield_rush", 72), (int(ax), H - 102))
    wedge = Image.new("RGBA", (72, 72), (0, 0, 0, 0))
    ImageDraw.Draw(wedge).pieslice((-30, -30, 102, 102), -90, -90 + 360 * 0.35, fill=(6, 4, 10, 185))
    mask = Image.new("L", (72, 72), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, 71, 71), radius=14, fill=255)
    wedge.putalpha(Image.fromarray(np.minimum(np.array(wedge.getchannel("A")), np.array(mask))))
    img.alpha_composite(wedge, (int(ax), H - 102))
    d.rounded_rectangle((W / 2 - 30, H - 24, W / 2 + 30, H - 6), radius=4, fill=(216, 205, 184))
    text(d, (W / 2, H - 15), "SPACE", font("Inter-ExtraBold.ttf", 11), (26, 20, 32), "mm", 0)


def card(img, x, y, w, h, rarity, name, rarity_line, tag, icon_name, desc, evo, evo_icon, key):
    d = ImageDraw.Draw(img)
    color = RARITY[rarity]
    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((x, y + 16, x + w, y + h + 16), radius=12, fill=(0, 0, 0, 200))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(18)))
    panel = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    grad = np.zeros((h, w, 4), np.uint8)
    t = np.linspace(0, 1, h)[:, None]
    grad[:, :, 0] = (34 * (1 - t) + 13 * t).astype(np.uint8)
    grad[:, :, 1] = (27 * (1 - t) + 10 * t).astype(np.uint8)
    grad[:, :, 2] = (44 * (1 - t) + 18 * t).astype(np.uint8)
    grad[:, :, 3] = 250
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    glow = np.clip(1 - np.sqrt(((xx - w / 2) / (w * 0.7)) ** 2 + ((yy - h * 0.16) / (h * 0.32)) ** 2), 0, 1) * 0.32
    for c in range(3):
        grad[:, :, c] = np.clip(grad[:, :, c] + glow * color[c], 0, 255).astype(np.uint8)
    panel = Image.fromarray(grad, "RGBA")
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius=12, fill=255)
    img.paste(panel, (x, y), mask)
    d.rounded_rectangle((x, y, x + w, y + h), radius=12, outline=color, width=3)
    tf = font("Inter-ExtraBold.ttf", 13)
    tw = d.textlength(tag, font=tf) + 20
    rank_tag = tag.startswith("RANK")
    d.rounded_rectangle((x + w / 2 - tw / 2, y + 16, x + w / 2 + tw / 2, y + 40), radius=4,
                        fill=(46, 38, 56) if rank_tag else (255, 201, 76), outline=(107, 85, 48) if rank_tag else None)
    text(d, (x + w / 2, y + 28), tag, tf, (236, 228, 212) if rank_tag else (28, 16, 6), "mm", 0)
    d.ellipse((x + w - 26, y + 10, x + w + 4, y + 40), fill=(42, 14, 14), outline=(122, 42, 34), width=2)
    text(d, (x + w - 11, y + 25), "x", font("Inter-Black.ttf", 16), (255, 138, 122), "mm", 0)
    img.alpha_composite(icon(icon_name, 128), (int(x + w / 2 - 64), y + 54))
    text(d, (x + w / 2, y + 210), name, font("Inter-Black.ttf", 25), (255, 255, 255), "mm")
    text(d, (x + w / 2, y + 238), rarity_line, font("Inter-ExtraBold.ttf", 12), color, "mm", 0)
    div = Image.open(os.path.join(UI, "Art", "divider.png")).convert("RGBA").resize((w - 40, 16), Image.LANCZOS)
    img.alpha_composite(div, (x + 20, y + 252))
    dy = y + 290
    for line in desc:
        parts = line.split("->")
        f = font("Inter-SemiBold.ttf", 16)
        if len(parts) == 2:
            left = parts[0].rstrip()
            right = parts[1].strip()
            words = left.split(" ")
            label = " ".join(w for w in words if not any(ch.isdigit() for ch in w))
            old = " ".join(w for w in words if any(ch.isdigit() for ch in w))
            segs = [(label + " ", (216, 207, 191)), (old + " ", (169, 159, 143)), ("> ", (255, 201, 76)),
                    (right, (127, 227, 154))]
            total = sum(d.textlength(s, font=f) for s, _ in segs)
            cx = x + w / 2 - total / 2
            for s, c in segs:
                d.text((cx, dy), s, font=f, fill=c, anchor="lm")
                cx += d.textlength(s, font=f)
        else:
            d.text((x + w / 2, dy), line, font=f, fill=(216, 207, 191), anchor="mm")
        dy += 24
    if evo:
        d.line((x + 16, y + h - 88, x + w - 16, y + h - 88), fill=(107, 85, 48), width=1)
        img.alpha_composite(icon(evo_icon, 30), (x + 34, y + h - 80))
        d.text((x + 72, y + h - 65), evo, font=font("Inter-Medium.ttf", 13), fill=(255, 217, 138), anchor="lm")
    d.rounded_rectangle((x + w / 2 - 10, y + h - 36, x + w / 2 + 10, y + h - 14), radius=4, fill=(216, 205, 184))
    text(d, (x + w / 2, y + h - 25), key, font("Inter-ExtraBold.ttf", 12), (26, 20, 32), "mm", 0)


def button(img, x, y, w, h, label, primary=False, key=None, count=None):
    d = ImageDraw.Draw(img)
    top, bottom = ((255, 210, 122), (192, 138, 46)) if primary else ((44, 36, 54), (23, 18, 30))
    for i in range(h):
        t = i / h
        c = tuple(int(top[k] * (1 - t) + bottom[k] * t) for k in range(3))
        d.line((x + 2, y + i, x + w - 2, y + i), fill=c)
    d.rounded_rectangle((x, y, x + w, y + h), radius=6, outline=(255, 231, 168) if primary else (90, 74, 50), width=2)
    f = font("Inter-ExtraBold.ttf", 18)
    s = label
    tw = d.textlength(s, font=f)
    cx = x + w / 2 - tw / 2
    if key:
        kf = font("Inter-ExtraBold.ttf", 11)
        d.rounded_rectangle((cx - 34, y + h / 2 - 10, cx - 12, y + h / 2 + 10), radius=4, fill=(216, 205, 184))
        d.text((cx - 23, y + h / 2), key, font=kf, fill=(26, 20, 32), anchor="mm")
    d.text((cx, y + h / 2), s, font=f, fill=(28, 16, 6) if primary else (236, 228, 212), anchor="lm")
    if count:
        d.text((cx + tw + 8, y + h / 2), count, font=font("Inter-Black.ttf", 18), fill=(255, 201, 76), anchor="lm")


def mockup_levelup():
    img = world_background()
    draw_hud(img, [("oathblade_arc", 5, 8, False), ("verdict", 3, 8, False), ("dawnbreaker", 8, 8, True)],
             [("iron_heart", 3), ("whetstone", 2)])
    dim = Image.new("RGBA", (W, H), (6, 4, 10, 180))
    img.alpha_composite(dim)
    d = ImageDraw.Draw(img)
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((W / 2, 150), "LEVEL UP", font=font("Inter-Black.ttf", 56), fill=(255, 106, 42, 160), anchor="mm")
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(10)))
    text(d, (W / 2, 150), "LEVEL UP", font("Inter-Black.ttf", 56), (255, 217, 138), "mm", 3, (26, 13, 5))
    d.text((W / 2, 196), "L E V E L   2 2", font=font("Inter-SemiBold.ttf", 16), fill=(169, 159, 143), anchor="mm")
    cw, ch = 300, 470
    xs = [W / 2 - cw * 1.5 - 32, W / 2 - cw / 2, W / 2 + cw / 2 + 32]
    card(img, int(xs[0]), 240, cw, ch, "common", "Whetstone", "COMMON PASSIVE", "RANK 2 > 3", "whetstone",
         ["Might +16% -> +24%"], "Evolves Ravager Chop", "skullsplitter", "1")
    card(img, int(xs[1]), 228, cw, ch, "rare", "Storm Totem", "RARE WEAPON", "NEW", "storm_totem",
         ["Zaps every 0.5 s, chains 1", "Damage 9, range 6 m", "Lasts 5 s"], "Evolves with Quickening Hourglass",
         "quickening_hourglass", "2")
    card(img, int(xs[2]), 240, cw, ch, "uncommon", "Aegis Orbit", "UNCOMMON WEAPON", "RANK 4 > 5", "aegis_orbit",
         ["Shields 3 -> 4", "Duration 4.0 s"], "Evolves with Plate Pauldron", "plate_pauldron", "3")
    ImageDraw.Draw(img).rounded_rectangle((int(xs[1]) - 2, 226, int(xs[1]) + cw + 2, 228 + ch + 2), radius=13,
                                         outline=(255, 255, 255, 200), width=2)
    button(img, int(W / 2 - 420), 750, 230, 52, "REROLL", key="R", count="2")
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((W / 2 - 140, 766, W / 2 - 118, 786), radius=4, fill=(216, 205, 184))
    d.text((W / 2 - 129, 776), "X", font=font("Inter-ExtraBold.ttf", 11), fill=(26, 20, 32), anchor="mm")
    d.text((W / 2 - 108, 776), "BANISH", font=font("Inter-ExtraBold.ttf", 16), fill=(169, 159, 143), anchor="lm")
    d.text((W / 2 - 30, 776), "1", font=font("Inter-Black.ttf", 16), fill=(255, 138, 122), anchor="lm")
    button(img, int(W / 2 + 40), 750, 230, 52, "SKIP", key="Q", count="+5")
    img.convert("RGB").save(os.path.join(HERE, "mockup_levelup.png"))


def mockup_hud():
    img = world_background()
    vignette = Image.open(os.path.join(UI, "Art", "vignette.png")).convert("RGBA").resize((W, H), Image.LANCZOS)
    a = np.array(vignette)
    a[:, :, 3] = (a[:, :, 3] * 0.45).astype(np.uint8)
    img.alpha_composite(Image.fromarray(a, "RGBA"))
    draw_hud(img, [("oathblade_arc", 8, 8, False), ("verdict", 6, 8, False), ("dawnbreaker", 8, 8, True),
                   ("spirit_darts", 4, 8, False), ("fire_flask", 2, 8, False)],
             [("iron_heart", 5), ("whetstone", 3), ("plate_pauldron", 2), ("wide_sigil", 1)])
    d = ImageDraw.Draw(img)
    bx0, bx1, by = W * 0.2, W * 0.8, 175
    d.text((W / 2, by - 30), "T H E   H O L L O W   K I N G", font=font("Inter-ExtraBold.ttf", 13), fill=(255, 138, 106), anchor="mm")
    text(d, (W / 2, by - 8), "MALGRATH, THE UNBURIED", font("Inter-Black.ttf", 28), (255, 224, 192), "mm")
    d.rounded_rectangle((bx0, by + 12, bx1, by + 36), radius=4, fill=(12, 6, 8, 230), outline=(107, 85, 48), width=2)
    d.rectangle((bx0 + 2, by + 14, bx0 + (bx1 - bx0) * 0.58, by + 34), fill=(255, 244, 232))
    for i in range(20):
        t = i / 20
        c = (int(255 * (1 - t) + 168 * t), int(106 * (1 - t) + 16 * t), int(74 * (1 - t) + 28 * t))
        d.line((bx0 + 2, by + 14 + i, bx0 + (bx1 - bx0) * 0.52, by + 14 + i), fill=c)
    for mark, broken in ((0.65, True), (0.30, False)):
        mx = bx0 + (bx1 - bx0) * mark
        d.line((mx, by + 14, mx, by + 34), fill=(74, 58, 42) if broken else (255, 201, 76), width=2)
        d.polygon([(mx, by + 4), (mx + 6, by + 10), (mx, by + 16), (mx - 6, by + 10)],
                  fill=(74, 58, 42) if broken else (255, 201, 76))
    text(d, (W / 2, 260), "THE LAST TIDE", font("Inter-Black.ttf", 38), (255, 176, 160), "mm", 3, (26, 2, 2))
    for (x, y, s, c, size) in ((780, 420, "36", (255, 255, 255), 22), (1130, 380, "112!", (255, 225, 74), 29),
                               (1010, 640, "24", (255, 154, 60), 22), (870, 600, "-12", (255, 74, 58), 26),
                               (960, 470, "+8", (110, 240, 138), 24), (1210, 560, "48", (255, 255, 255), 22)):
        text(d, (x, y), s, font("Inter-Black.ttf", size), c, "mm", 2, (20, 12, 12))
    d.rounded_rectangle((W / 2 - 35, H / 2 - 104, W / 2 + 35, H / 2 - 95), radius=4, fill=(12, 10, 18, 200))
    d.rounded_rectangle((W / 2 - 35, H / 2 - 104, W / 2 - 35 + 70 * 0.28, H / 2 - 95), radius=4, fill=(208, 60, 50))
    chevron = Image.open(os.path.join(UI, "Art", "chevron.png")).convert("RGBA").resize((90, 90))
    img.alpha_composite(chevron, (W - 150, H // 2 - 45))
    marker = Image.open(os.path.join(UI, "Art", "marker.png")).convert("RGBA").resize((56, 56))
    m = np.array(marker).astype(np.float32)
    m[:, :, 0] *= 1.0
    m[:, :, 1] *= 0.72
    m[:, :, 2] *= 0.29
    marker = Image.fromarray(np.clip(m, 0, 255).astype(np.uint8), "RGBA").rotate(-30, resample=Image.BICUBIC)
    img.alpha_composite(marker, (1700, 860))
    img.alpha_composite(icon("skull", 30), (1713, 873))
    img.convert("RGB").save(os.path.join(HERE, "mockup_hud.png"))


def mockup_heroes():
    img = world_background()
    img.alpha_composite(Image.new("RGBA", (W, H), (6, 4, 10, 185)))
    d = ImageDraw.Draw(img)
    text(d, (W / 2, 76), "CHOOSE YOUR HERO", font("Inter-Black.ttf", 42), (255, 217, 138), "mm", 3, (26, 13, 5))
    d.text((W / 2, 118), "E V E R Y   O A T H   B R E A K S   D I F F E R E N T L Y", font=font("Inter-SemiBold.ttf", 15),
           fill=(169, 159, 143), anchor="mm")
    heroes = [("knight", "KNIGHT", "THE OATH THAT HOLDS", 7, 6, 3, False), ("barbarian", "BARBARIAN", "THE RED STORM", 8, 4, 5, True),
              ("mage", "MAGE", "THE LAST CANDLE", 2, 2, 6, True), ("rogue", "ROGUE", "THE KNIFE IN THE FOG", 3, 2, 8, True)]
    cw, ch = 360, 720
    x = W / 2 - (cw * 4 + 22 * 3) / 2
    for i, (hid, name, title, hp, armor, speed, locked) in enumerate(heroes):
        y = 160 - (12 if i == 0 else 0)
        d.rounded_rectangle((x, y, x + cw, y + ch), radius=12, fill=(24, 19, 32, 245),
                            outline=(255, 201, 76) if i == 0 else (74, 62, 44), width=2)
        portrait = icon(hid + "_portrait", 332)
        if locked:
            g = np.array(portrait).astype(np.float32)
            lum = (g[:, :, 0] * 0.3 + g[:, :, 1] * 0.59 + g[:, :, 2] * 0.11) * 0.45
            g[:, :, 0] = g[:, :, 1] = g[:, :, 2] = lum
            portrait = Image.fromarray(g.astype(np.uint8), "RGBA")
        crop = portrait.crop((0, 36, 332, 296))
        img.alpha_composite(crop, (int(x + 14), int(y + 14)))
        if locked:
            img.alpha_composite(icon("lock", 96), (int(x + cw / 2 - 48), int(y + 96)))
        text(d, (x + cw / 2, y + 310), name, font("Inter-Black.ttf", 32), (255, 217, 138) if i == 0 else (255, 255, 255), "mm")
        d.text((x + cw / 2, y + 342), title, font=font("Inter-SemiBold.ttf", 14), fill=(169, 159, 143), anchor="mm")
        for r, (label, value) in enumerate((("HEALTH", hp), ("ARMOR", armor), ("SPEED", speed))):
            ry = y + 372 + r * 22
            d.text((x + 14, ry), label, font=font("Inter-ExtraBold.ttf", 12), fill=(201, 164, 92), anchor="lm")
            for s in range(8):
                sx = x + 94 + s * 25
                d.rounded_rectangle((sx, ry - 4, sx + 21, ry + 4), radius=2, fill=(255, 196, 96) if s < value else (42, 36, 48))
        if locked:
            d.rounded_rectangle((x + 14, y + 460, x + cw - 14, y + 560), radius=8, fill=(0, 0, 0, 120), outline=(107, 85, 48), width=1)
            text(d, (x + cw / 2, y + 488), "LOCKED", font("Inter-Black.ttf", 18), (169, 159, 143), "mm", 0)
            d.text((x + cw / 2, y + 522), "Defeat the Gravewarden (5:00) once.", font=font("Inter-Medium.ttf", 15),
                   fill=(236, 228, 212), anchor="mm")
        else:
            for a, (kind, iname, nm, desc) in enumerate((("STARTING WEAPON", "oathblade_arc", "Oathblade Arc", ""),
                                                        ("SIGNATURE", "bulwark", "Bulwark", "15% chance to block any hit."),
                                                        ("ACTIVE", "shield_rush", "Shield Rush", "Dash 6 m, stun and knock aside."))):
                ay = y + 450 + a * 80
                img.alpha_composite(icon(iname, 44), (int(x + 14), int(ay)))
                d.text((x + 70, ay + 6), kind, font=font("Inter-ExtraBold.ttf", 11), fill=(138, 125, 102), anchor="lm")
                d.text((x + 70, ay + 24), nm, font=font("Inter-ExtraBold.ttf", 15), fill=(255, 217, 138), anchor="lm")
                if desc:
                    d.text((x + 70, ay + 44), desc, font=font("Inter-Medium.ttf", 12), fill=(200, 191, 175), anchor="lm")
        x += cw + 22
    button(img, int(W / 2 - 330), 960, 200, 52, "BACK", key="ESC")
    button(img, int(W / 2 - 100), 960, 400, 52, "BEGIN AS KNIGHT", primary=True)
    img.convert("RGB").save(os.path.join(HERE, "mockup_heroes.png"))


if __name__ == "__main__":
    mockup_levelup()
    mockup_hud()
    mockup_heroes()
    print("mockups written")
