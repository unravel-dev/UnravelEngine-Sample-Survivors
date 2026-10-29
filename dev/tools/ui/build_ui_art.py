"""
Barrowlight UI art: item icons, hero portraits, HUD glyphs, logo, ornaments and the icon contact sheet.

Every item icon is one CC0 pixel-art glyph (496 RPG icons by 7Soul1) scaled 3x with nearest
neighbour onto a generated badge whose colours say what the item is (weapon, evolution, passive,
active, signature, fallback, meta rank). Hero portraits are matted from the lead's render of the
KayKit heroes (CC0). HUD glyphs, the logo and the ornaments are drawn here.

Usage: python build_ui_art.py [logo]   (logo: only the title logo; paths below are absolute; output goes to ../../../data/UI)
"""
import math
import os
import random
import shutil
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT_UI = os.path.join(ROOT, "..", "data", "UI")
OUT_ICONS = os.path.join(OUT_UI, "Icons")
OUT_ART = os.path.join(OUT_UI, "Art")
OUT_FONTS = os.path.join(OUT_UI, "Fonts")
OUT_GLYPHS = os.path.join(OUT_UI, "Glyphs")
RPG = r"C:\Workspace\github\UnravelEngine\_downloads\ui_assets\496_RPG_icons"
KENNEY_GLYPHS = r"C:\Workspace\github\UnravelEngine\_downloads\ui_assets\kenney_game-icons\PNG\White\2x"
INTER_STATIC = r"C:\Workspace\github\UnravelEngine\UnravelEngine\engine_data\data\fonts\Inter\static"
INTER_OFL = r"C:\Workspace\github\UnravelEngine\UnravelEngine\engine_data\data\fonts\Inter\OFL.txt"
HEROES_RENDER = os.path.join(ROOT, "heroes_front.png")

ICON = 128
SUPER = 4
GLYPH_SCALE = 3

# Badge palettes: top colour, bottom colour, rim colour, glow colour.
PALETTES = {
    "weapon": ((44, 52, 74), (13, 15, 24), (150, 165, 200), (120, 150, 220)),
    "evolution": ((92, 60, 18), (26, 15, 4), (255, 201, 76), (255, 190, 70)),
    "passive": ((30, 62, 46), (8, 20, 14), (120, 205, 150), (90, 210, 140)),
    "active": ((16, 62, 76), (4, 20, 26), (95, 211, 232), (80, 210, 240)),
    "signature": ((84, 26, 30), (24, 6, 8), (232, 110, 92), (240, 90, 70)),
    "fallback": ((72, 48, 18), (24, 14, 4), (226, 162, 80), (230, 160, 70)),
    "meta": ((58, 44, 30), (18, 13, 8), (201, 164, 92), (210, 170, 100)),
    "hero": ((40, 34, 52), (10, 8, 14), (201, 164, 92), (200, 170, 120)),
    "boon": ((24, 74, 62), (6, 22, 18), (120, 232, 190), (110, 240, 190)),
}

ICONS = [
    # Class weapons
    ("oathblade_arc", "weapon", "rpg:W_Sword005"),
    ("aegis_orbit", "weapon", "rpg:E_Metal01"),
    ("verdict", "weapon", "rpg:S_Sword06"),
    ("ravager_chop", "weapon", "rpg:W_Axe013"),
    ("whirlwind", "weapon", "rpg:S_Wind01"),
    ("fault_line", "weapon", "rpg:S_Earth04"),
    ("ember_orb", "weapon", "rpg:S_Axe01"),
    ("starfall", "weapon", "rpg:S_Fire05"),
    ("rime_nova", "weapon", "rpg:S_Ice02"),
    ("repeater_crossbow", "weapon", "rpg:S_Bow11"),
    ("fan_of_knives", "weapon", "rpg:S_Shadow08"),
    ("assassins_mark", "weapon", "rpg:S_Shadow13"),
    ("consecration", "weapon", "rpg:S_Holy06"),
    ("dawn_hammer", "weapon", "rpg:W_Mace009"),
    ("war_cry", "weapon", "rpg:S_Buff14"),
    ("butchers_chains", "weapon", "rpg:W_Throw002"),
    ("arc_lash", "weapon", "rpg:S_Thunder04"),
    ("prismatic_ray", "weapon", "rpg:S_Light01"),
    ("venom_shivs", "weapon", "rpg:S_Poison02"),
    ("shadow_dancers", "weapon", "rpg:S_Shadow16"),
    # Shared weapons
    ("spirit_darts", "weapon", "rpg:S_Bow02"),
    ("ricochet_disc", "weapon", "rpg:W_Throw004"),
    ("bramble_halo", "weapon", "rpg:S_Poison07"),
    ("skewer_javelin", "weapon", "rpg:W_Spear003"),
    ("fire_flask", "weapon", "rpg:P_Orange05"),
    ("storm_totem", "weapon", "rpg:S_Thunder01"),
    ("hunting_wisps", "weapon", "rpg:S_Light01"),
    # Evolutions
    ("dawnbreaker", "evolution", "rpg:W_Gold_Sword"),
    ("bastion_halo", "evolution", "rpg:E_Gold01"),
    ("final_verdict", "evolution", "rpg:S_Holy02"),
    ("skullsplitter", "evolution", "rpg:W_Gold_Axe"),
    ("maelstrom", "evolution", "rpg:S_Fire04"),
    ("tectonic_rift", "evolution", "rpg:S_Fire06"),
    ("sunfire_orb", "evolution", "rpg:S_Light02"),
    ("cataclysm", "evolution", "rpg:S_Fire02"),
    ("absolute_zero", "evolution", "rpg:S_Ice07"),
    ("widowmaker", "evolution", "rpg:W_Gold_Bow"),
    ("storm_of_steel", "evolution", "rpg:S_Dagger05"),
    ("death_sentence", "evolution", "rpg:E_Bones03"),
    ("soul_hail", "evolution", "rpg:S_Bow06"),
    ("eclipse_disc", "evolution", "rpg:S_Light03"),
    ("briar_sanctum", "evolution", "rpg:S_Poison06"),
    ("godspear", "evolution", "rpg:W_Gold_Spear"),
    ("hellfire_cask", "evolution", "rpg:S_Fire08"),
    ("thunderspire", "evolution", "rpg:S_Thunder02"),
    ("wisp_choir", "evolution", "rpg:S_Shadow07"),
    ("eternal_sanctum", "evolution", "rpg:S_Holy10"),
    ("skybreaker", "evolution", "rpg:S_Thunder05"),
    ("warlords_bellow", "evolution", "rpg:S_Buff08"),
    ("maw_of_the_pit", "evolution", "rpg:S_Shadow05"),
    ("tempest_crown", "evolution", "rpg:S_Thunder06"),
    ("aurora_lance", "evolution", "rpg:S_Magic01"),
    ("nightshade_plague", "evolution", "rpg:S_Poison03"),
    ("legion_of_shadows", "evolution", "rpg:S_Shadow14"),
    # Passives
    ("iron_heart", "passive", "rpg:S_Holy01"),
    ("plate_pauldron", "passive", "rpg:A_Armour02"),
    ("windstride_boots", "passive", "rpg:A_Shoes02"),
    ("whetstone", "passive", "rpg:I_SilverBar"),
    ("lodestone_charm", "passive", "rpg:Ac_Medal04"),
    ("wide_sigil", "passive", "rpg:S_Shadow12"),
    ("everburn_wick", "passive", "rpg:I_Torch02"),
    ("tome_of_insight", "passive", "rpg:W_Book06"),
    ("keen_eye", "passive", "rpg:I_Eye"),
    ("lucky_knucklebone", "passive", "rpg:I_Bone"),
    ("quickening_hourglass", "passive", "rpg:I_Clock"),
    ("echo_quiver", "passive", "rpg:S_Bow08"),
    # Actives
    ("shield_rush", "active", "rpg:S_Holy09"),
    ("earthshaker_leap", "active", "rpg:S_Earth07"),
    ("blink", "active", "rpg:S_Shadow11"),
    ("smoke_roll", "active", "rpg:S_Poison09"),
    # Signature passives
    ("bulwark", "signature", "rpg:E_Metal06"),
    ("bloodlust", "signature", "rpg:S_Buff14"),
    ("arcane_echo", "signature", "rpg:S_Shadow02"),
    ("momentum", "signature", "rpg:S_Wind07"),
    # Fallback cards
    ("hearty_meal", "fallback", "rpg:I_C_Meat"),
    ("coin_purse", "fallback", "rpg:S_Magic07"),
    ("battle_hymn", "fallback", "rpg:S_Holy05"),
    # Boons (slotless, unlimited)
    ("boon_fleetfoot", "boon", "rpg:A_Shoes05"),
    ("boon_magnetism", "boon", "rpg:Ac_Medal02"),
    ("boon_vitality", "boon", "rpg:P_Red02"),
    ("boon_mending", "boon", "rpg:P_Medicine03"),
    ("boon_hide", "boon", "rpg:A_Armour03"),
    ("boon_edge", "boon", "rpg:W_Sword010"),
    ("boon_alacrity", "boon", "rpg:S_Wind05"),
    ("boon_breadth", "boon", "rpg:S_Magic03"),
    ("boon_persistence", "boon", "rpg:I_Torch01"),
    ("boon_precision", "boon", "rpg:S_Bow13"),
    ("boon_insight", "boon", "rpg:W_Book03"),
    ("boon_plunder", "boon", "rpg:I_GoldCoin"),
    ("boon_clover", "boon", "rpg:I_RabbitPaw"),
    # Meta ranks (Armory)
    ("vigor", "meta", "rpg:P_Red06"),
    ("might", "meta", "rpg:W_Gold_Mace"),
    ("plating", "meta", "rpg:A_Armor04"),
    ("mending", "meta", "rpg:S_Holy03"),
    ("swiftness", "meta", "rpg:A_Shoes04"),
    ("haste", "meta", "rpg:S_Thunder04"),
    ("reach", "meta", "rpg:S_Magic02"),
    ("magnetism", "meta", "rpg:S_Magic04"),
    ("fortune", "meta", "rpg:I_Clover"),
    ("insight", "meta", "rpg:I_Scroll"),
    ("avarice", "meta", "rpg:I_GoldBar"),
    ("second_wind", "meta", "rpg:S_Holy04"),
    ("reroll", "meta", "rpg:S_Physic01"),
    ("banish", "meta", "rpg:S_Magic05"),
    # Heroes (badge portraits; big portraits are written as <id>_portrait.png)
    ("knight", "hero", "portrait:knight"),
    ("barbarian", "hero", "portrait:barbarian"),
    ("mage", "hero", "portrait:mage"),
    ("rogue", "hero", "portrait:rogue"),
    # HUD glyphs (no badge)
    ("gold", "glyph", "vec:coin"),
    ("skull", "glyph", "vec:skull"),
    ("timer", "glyph", "vec:hourglass"),
    ("xp", "glyph", "vec:gem"),
    ("chest", "glyph", "rpg:I_Chest01"),
    ("lock", "glyph", "vec:lock"),
]

# Regions of heroes_front.png holding each hero's head and chest (x0, y0, x1, y1).
PORTRAIT_CROPS = {
    "rogue": (262, 262, 438, 440),
    "mage": (510, 222, 745, 440),
    "barbarian": (842, 250, 1010, 440),
    "knight": (1135, 245, 1305, 440),
}
PORTRAIT_TINTS = {
    "knight": (255, 201, 76),
    "barbarian": (230, 80, 60),
    "mage": (176, 92, 255),
    "rogue": (70, 210, 190),
}

KENNEY_COPY = [
    "pause", "gear", "return", "exitLeft", "trophy", "locked", "unlocked", "cross", "checkmark",
    "star", "home", "warning", "right", "left", "up", "down", "buttonA", "buttonB", "buttonX",
    "buttonY", "buttonStart", "buttonSelect", "musicOn", "audioOn", "information", "plus",
    "minus", "fastForward", "target", "massiveMultiplayer",
]


def lerp(a, b, t):
    return a + (b - a) * t


def lerp_color(c0, c1, t):
    return tuple(int(round(lerp(c0[i], c1[i], t))) for i in range(len(c0)))


def rounded_mask(size, radius, inset=0):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (inset, inset, size - 1 - inset, size - 1 - inset), radius=max(1, radius - inset), fill=255)
    return mask


def vertical_gradient(size, top, bottom):
    t = np.linspace(0.0, 1.0, size)[:, None]
    top = np.array(top, dtype=np.float32)
    bottom = np.array(bottom, dtype=np.float32)
    rows = top[None, :] * (1.0 - t) + bottom[None, :] * t
    image = np.repeat(rows[:, None, :], size, axis=1)
    return Image.fromarray(np.clip(image, 0, 255).astype(np.uint8), "RGB")


def radial_alpha(size, center, radius, power=1.6, strength=1.0):
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    d = np.sqrt((xx - center[0]) ** 2 + (yy - center[1]) ** 2) / radius
    a = np.clip(1.0 - d, 0.0, 1.0) ** power * strength
    return Image.fromarray((a * 255).astype(np.uint8), "L")


def noise_layer(size, seed, amount):
    rng = np.random.default_rng(seed)
    n = rng.normal(0.0, 1.0, (size // 8, size // 8)).astype(np.float32)
    img = Image.fromarray(((n * 0.5 + 0.5).clip(0, 1) * 255).astype(np.uint8), "L")
    img = img.resize((size, size), Image.BICUBIC)
    arr = (np.array(img).astype(np.float32) / 255.0 - 0.5) * amount
    return arr


def make_badge(category, seed):
    top, bottom, rim, glow = PALETTES[category]
    s = ICON * SUPER
    radius = 26 * SUPER
    base = vertical_gradient(s, top, bottom).convert("RGBA")
    arr = np.array(base).astype(np.float32)
    arr[:, :, :3] += noise_layer(s, seed, 14.0)[:, :, None]
    base = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    # Glow behind the glyph.
    glow_layer = Image.new("RGBA", (s, s), glow + (0,))
    glow_layer.putalpha(radial_alpha(s, (s * 0.5, s * 0.47), s * 0.48, 1.4, 0.55))
    base = Image.alpha_composite(base, glow_layer)
    # Edge darkening.
    edge = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    inv = ImageChops.invert(radial_alpha(s, (s * 0.5, s * 0.45), s * 0.78, 0.8, 1.0))
    edge.putalpha(inv.point(lambda v: int(v * 0.65)))
    base = Image.alpha_composite(base, edge)
    # Top sheen.
    sheen = Image.new("RGBA", (s, s), (255, 255, 255, 0))
    sheen_mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(sheen_mask).ellipse((-s * 0.3, -s * 0.75, s * 1.3, s * 0.42), fill=26)
    sheen.putalpha(sheen_mask.filter(ImageFilter.GaussianBlur(s * 0.02)))
    base = Image.alpha_composite(base, sheen)
    # Clip to the rounded shape.
    shape = rounded_mask(s, radius, 0)
    out = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    out.paste(base, (0, 0), shape)
    draw = ImageDraw.Draw(out)
    # Borders: dark outer, rim, thin bright inner line.
    draw.rounded_rectangle((2, 2, s - 3, s - 3), radius=radius, outline=(6, 5, 10, 255), width=4 * SUPER)
    rim_width = 3 * SUPER if category != "evolution" else 4 * SUPER
    inset = 4 * SUPER
    draw.rounded_rectangle((inset, inset, s - 1 - inset, s - 1 - inset), radius=radius - inset,
                           outline=rim + (255,), width=rim_width)
    hi = lerp_color(rim, (255, 255, 255), 0.55)
    inset2 = inset + rim_width
    draw.rounded_rectangle((inset2, inset2, s - 1 - inset2, s - 1 - inset2), radius=radius - inset2,
                           outline=hi + (90,), width=SUPER)
    if category == "evolution":
        # Small corner studs mark evolved items at a glance.
        for cx, cy in ((inset2 + 10 * SUPER, inset2 + 10 * SUPER), (s - inset2 - 10 * SUPER, inset2 + 10 * SUPER)):
            r = 3 * SUPER
            draw.polygon([(cx, cy - r * 1.6), (cx + r, cy), (cx, cy + r * 1.6), (cx - r, cy)],
                         fill=(255, 236, 170, 255), outline=(90, 50, 0, 255))
    return out.resize((ICON, ICON), Image.LANCZOS)


def load_rpg_glyph(name):
    path = os.path.join(RPG, name + ".png")
    img = Image.open(path).convert("RGBA")
    return img.resize((img.width * GLYPH_SCALE, img.height * GLYPH_SCALE), Image.NEAREST)


def drop_shadow(glyph, offset, blur, alpha):
    shadow = Image.new("RGBA", glyph.size, (0, 0, 0, 0))
    a = glyph.getchannel("A").point(lambda v: int(v * alpha / 255))
    shadow.putalpha(a)
    shadow = shadow.filter(ImageFilter.GaussianBlur(blur))
    canvas = Image.new("RGBA", (glyph.width + abs(offset[0]) * 2, glyph.height + abs(offset[1]) * 2), (0, 0, 0, 0))
    return shadow, canvas


def compose_icon(badge, glyph):
    out = badge.copy() if badge is not None else Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    gx = (ICON - glyph.width) // 2
    gy = (ICON - glyph.height) // 2
    shadow = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    mask = Image.new("L", (ICON, ICON), 0)
    mask.paste(glyph.getchannel("A"), (gx + 2, gy + 4))
    shadow.putalpha(mask.filter(ImageFilter.GaussianBlur(3)).point(lambda v: int(v * 0.75)))
    out = Image.alpha_composite(out, shadow)
    layer = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    layer.paste(glyph, (gx, gy), glyph)
    return Image.alpha_composite(out, layer)


# ---------------------------------------------------------------------------------------------
# Vector glyphs (drawn at 4x, downsampled)
# ---------------------------------------------------------------------------------------------

def vec_canvas():
    s = ICON * SUPER
    return Image.new("RGBA", (s, s), (0, 0, 0, 0)), s


def outline_and_shadow(img):
    """Adds a dark outline and a soft drop shadow under a drawn glyph."""
    s = img.width
    a = img.getchannel("A")
    outline = a.filter(ImageFilter.MaxFilter(4 * SUPER + 1))
    shadow = outline.filter(ImageFilter.GaussianBlur(4 * SUPER))
    base = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sh = Image.new("RGBA", (s, s), (0, 0, 0, 255))
    sh.putalpha(shadow.point(lambda v: int(v * 0.6)))
    base = Image.alpha_composite(base, ImageChops.offset(sh, 0, 3 * SUPER))
    ol = Image.new("RGBA", (s, s), (12, 8, 14, 255))
    ol.putalpha(outline)
    base = Image.alpha_composite(base, ol)
    return Image.alpha_composite(base, img)


def shade_vertical(img, top_mul, bottom_mul):
    arr = np.array(img).astype(np.float32)
    t = np.linspace(top_mul, bottom_mul, img.height)[:, None, None]
    arr[:, :, :3] *= t
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def vec_coin():
    img, s = vec_canvas()
    d = ImageDraw.Draw(img)
    c = s / 2
    r = s * 0.36
    d.ellipse((c - r, c - r, c + r, c + r), fill=(255, 196, 64, 255))
    r2 = r * 0.78
    d.ellipse((c - r2, c - r2, c + r2, c + r2), outline=(170, 110, 20, 255), width=5 * SUPER)
    # A small skull-crown stamp: a diamond.
    k = r * 0.38
    d.polygon([(c, c - k), (c + k * 0.75, c), (c, c + k), (c - k * 0.75, c)], fill=(170, 110, 20, 255))
    img = shade_vertical(img, 1.15, 0.8)
    hl = Image.new("RGBA", (s, s), (255, 255, 230, 0))
    m = Image.new("L", (s, s), 0)
    ImageDraw.Draw(m).ellipse((c - r * 0.75, c - r * 0.9, c + r * 0.2, c - r * 0.2), fill=90)
    hl.putalpha(m.filter(ImageFilter.GaussianBlur(6 * SUPER)))
    img = Image.alpha_composite(img, hl)
    return outline_and_shadow(img)


def vec_skull(color=(236, 228, 210)):
    img, s = vec_canvas()
    d = ImageDraw.Draw(img)
    c = s / 2
    w = s * 0.62
    top = s * 0.16
    d.ellipse((c - w / 2, top, c + w / 2, top + w * 0.92), fill=color + (255,))
    jaw_w = w * 0.62
    d.rounded_rectangle((c - jaw_w / 2, top + w * 0.62, c + jaw_w / 2, top + w * 1.08), radius=10 * SUPER,
                        fill=color + (255,))
    hole = (24, 16, 22, 255)
    ew = w * 0.24
    eh = w * 0.26
    ey = top + w * 0.42
    d.ellipse((c - w * 0.29 - ew / 2, ey - eh / 2, c - w * 0.29 + ew / 2, ey + eh / 2), fill=hole)
    d.ellipse((c + w * 0.29 - ew / 2, ey - eh / 2, c + w * 0.29 + ew / 2, ey + eh / 2), fill=hole)
    ny = top + w * 0.66
    d.polygon([(c, ny - w * 0.07), (c + w * 0.06, ny + w * 0.06), (c - w * 0.06, ny + w * 0.06)], fill=hole)
    for i in range(-2, 3):
        x = c + i * w * 0.1
        d.line((x, top + w * 0.86, x, top + w * 1.06), fill=hole, width=3 * SUPER)
    img = shade_vertical(img, 1.05, 0.78)
    return outline_and_shadow(img)


def vec_hourglass():
    img, s = vec_canvas()
    d = ImageDraw.Draw(img)
    c = s / 2
    w = s * 0.5
    h = s * 0.66
    t = c - h / 2
    b = c + h / 2
    frame = (214, 176, 98, 255)
    glass = (210, 225, 240, 200)
    sand = (255, 205, 90, 255)
    d.polygon([(c - w * 0.42, t + h * 0.1), (c + w * 0.42, t + h * 0.1), (c + w * 0.06, c),
               (c + w * 0.42, b - h * 0.1), (c - w * 0.42, b - h * 0.1), (c - w * 0.06, c)], fill=glass)
    d.polygon([(c - w * 0.22, t + h * 0.3), (c + w * 0.22, t + h * 0.3), (c, c)], fill=sand)
    d.polygon([(c - w * 0.36, b - h * 0.12), (c + w * 0.36, b - h * 0.12), (c, b - h * 0.32)], fill=sand)
    d.line((c, c, c, b - h * 0.2), fill=sand, width=2 * SUPER)
    d.rounded_rectangle((c - w / 2, t, c + w / 2, t + h * 0.1), radius=4 * SUPER, fill=frame)
    d.rounded_rectangle((c - w / 2, b - h * 0.1, c + w / 2, b), radius=4 * SUPER, fill=frame)
    img = shade_vertical(img, 1.1, 0.85)
    return outline_and_shadow(img)


def vec_gem():
    img, s = vec_canvas()
    d = ImageDraw.Draw(img)
    c = s / 2
    w = s * 0.5
    h = s * 0.66
    t = c - h / 2
    pts_top = [(c - w / 2, t + h * 0.3), (c - w * 0.25, t), (c + w * 0.25, t), (c + w / 2, t + h * 0.3)]
    d.polygon(pts_top + [(c, c + h / 2)], fill=(63, 169, 255, 255))
    d.polygon([(c - w * 0.25, t), (c + w * 0.25, t), (c + w * 0.12, t + h * 0.3), (c - w * 0.12, t + h * 0.3)],
              fill=(150, 215, 255, 255))
    d.polygon([(c - w / 2, t + h * 0.3), (c - w * 0.12, t + h * 0.3), (c, c + h / 2)], fill=(40, 120, 220, 255))
    d.polygon([(c + w / 2, t + h * 0.3), (c + w * 0.12, t + h * 0.3), (c, c + h / 2)], fill=(30, 95, 190, 255))
    d.polygon([(c - w * 0.12, t + h * 0.3), (c + w * 0.12, t + h * 0.3), (c, c + h / 2)], fill=(90, 190, 255, 255))
    return outline_and_shadow(img)


def vec_lock():
    img, s = vec_canvas()
    d = ImageDraw.Draw(img)
    c = s / 2
    w = s * 0.5
    d.arc((c - w * 0.32, s * 0.18, c + w * 0.32, s * 0.62), 180, 360, fill=(190, 190, 205, 255), width=9 * SUPER)
    d.line((c - w * 0.32 + 4 * SUPER, s * 0.4, c - w * 0.32 + 4 * SUPER, s * 0.48), fill=(190, 190, 205, 255), width=9 * SUPER)
    d.line((c + w * 0.32 - 4 * SUPER, s * 0.4, c + w * 0.32 - 4 * SUPER, s * 0.48), fill=(190, 190, 205, 255), width=9 * SUPER)
    d.rounded_rectangle((c - w / 2, s * 0.44, c + w / 2, s * 0.84), radius=8 * SUPER, fill=(214, 176, 98, 255))
    d.ellipse((c - 5 * SUPER, s * 0.56, c + 5 * SUPER, s * 0.56 + 10 * SUPER), fill=(40, 26, 10, 255))
    d.rectangle((c - 2 * SUPER, s * 0.6, c + 2 * SUPER, s * 0.74), fill=(40, 26, 10, 255))
    img = shade_vertical(img, 1.1, 0.8)
    return outline_and_shadow(img)


VECTORS = {"coin": vec_coin, "skull": vec_skull, "hourglass": vec_hourglass, "gem": vec_gem, "lock": vec_lock}


# ---------------------------------------------------------------------------------------------
# Hero portraits
# ---------------------------------------------------------------------------------------------

def matte_hero(render, box):
    x0, y0, x1, y1 = box
    c = render[y0:y1, x0:x1]
    h, w, _ = c.shape
    mx = c.max(2)
    mn = c.min(2)
    sat = mx - mn
    rows = np.arange(h)[:, None] + y0
    cand = ((mx > 196) & (sat < 34)) | ((mx > 140) & (sat < 70) & (rows < 292))
    bg = np.zeros((h, w), bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if cand[y, x]:
                bg[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if cand[y, x] and not bg[y, x]:
                bg[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not bg[ny, nx] and cand[ny, nx] \
                    and np.abs(c[ny, nx] - c[y, x]).sum() < 14:
                bg[ny, nx] = True
                q.append((ny, nx))
    alpha = Image.fromarray(((~bg) * 255).astype(np.uint8))
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    img = Image.fromarray(c.astype(np.uint8)).convert("RGBA")
    img.putalpha(alpha)
    return img


def make_portrait(hero, figure, size):
    """A framed portrait: dark backdrop, class-coloured rim light, the matted figure, vignette."""
    tint = PORTRAIT_TINTS[hero]
    s = size * 2
    bg = vertical_gradient(s, lerp_color(tint, (20, 16, 26), 0.78), (8, 6, 12)).convert("RGBA")
    glow = Image.new("RGBA", (s, s), tint + (0,))
    glow.putalpha(radial_alpha(s, (s * 0.5, s * 0.42), s * 0.55, 1.5, 0.55))
    bg = Image.alpha_composite(bg, glow)
    scale = (s * 0.98) / figure.height
    fig = figure.resize((int(figure.width * scale), int(figure.height * scale)), Image.LANCZOS)
    rim = Image.new("RGBA", fig.size, tint + (0,))
    rim.putalpha(fig.getchannel("A").filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(6)).point(lambda v: int(v * 0.8)))
    layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    fx = (s - fig.width) // 2
    fy = s - fig.height + int(s * 0.06)
    layer.paste(rim, (fx, fy), rim)
    bg = Image.alpha_composite(bg, layer)
    layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    layer.paste(fig, (fx, fy), fig)
    bg = Image.alpha_composite(bg, layer)
    shade = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    shade.putalpha(ImageChops.invert(radial_alpha(s, (s * 0.5, s * 0.4), s * 0.8, 0.9, 1.0)).point(lambda v: int(v * 0.7)))
    bg = Image.alpha_composite(bg, shade)
    bottom = Image.new("RGBA", (s, s), (8, 6, 12, 0))
    bottom.putalpha(Image.fromarray((np.clip((np.linspace(0, 1, s) - 0.7) / 0.3, 0, 1)[:, None].repeat(s, 1) * 230).astype(np.uint8)))
    bg = Image.alpha_composite(bg, bottom)
    return bg.resize((size, size), Image.LANCZOS)


def make_hero_badge(portrait):
    badge = make_badge("hero", 7)
    inner = portrait.resize((ICON - 20, ICON - 20), Image.LANCZOS)
    mask = rounded_mask(ICON - 20, 18)
    out = badge.copy()
    out.paste(inner, (10, 10), mask)
    d = ImageDraw.Draw(out)
    d.rounded_rectangle((9, 9, ICON - 10, ICON - 10), radius=19, outline=(201, 164, 92, 255), width=2)
    return out


# ---------------------------------------------------------------------------------------------
# Logo, ornaments, misc art
# ---------------------------------------------------------------------------------------------

def font(name, size):
    return ImageFont.truetype(os.path.join(INTER_STATIC, name), size)


GAME_TITLE = "BARROWLIGHT"


def make_logo():
    text = GAME_TITLE
    f = font("Inter-Black.ttf", 300)
    spacing = 26
    widths = [f.getbbox(ch)[2] - f.getbbox(ch)[0] for ch in text]
    total = sum(widths) + spacing * (len(text) - 1)
    w, h = total + 240, 520
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    x = 120
    for ch, cw in zip(text, widths):
        bb = f.getbbox(ch)
        d.text((x - bb[0], 120), ch, font=f, fill=255)
        x += cw + spacing
    # Bone-cracks through the letters.
    rng = random.Random(7)
    cracks = Image.new("L", (w, h), 0)
    cd = ImageDraw.Draw(cracks)
    for _ in range(14):
        px = rng.uniform(140, w - 140)
        py = rng.uniform(170, 400)
        for _ in range(4):
            nx = px + rng.uniform(-40, 40)
            ny = py + rng.uniform(15, 55)
            cd.line((px, py, nx, ny), fill=255, width=rng.choice((3, 4, 5)))
            px, py = nx, ny
    letters = ImageChops.subtract(mask, cracks)
    # Gold metal fill: vertical gradient with a bright band.
    t = np.linspace(0, 1, h)[:, None]
    band = np.exp(-((t - 0.47) ** 2) / 0.004)
    r = np.clip(150 + 105 * (1 - t) + 60 * band, 0, 255)
    g = np.clip(100 + 110 * (1 - t) + 70 * band, 0, 255)
    b = np.clip(40 + 60 * (1 - t) + 80 * band, 0, 255)
    fill = np.dstack([r.repeat(w, 1), g.repeat(w, 1), b.repeat(w, 1), np.array(letters)]).astype(np.uint8)
    fill_img = Image.fromarray(fill, "RGBA")
    # Bevel: lighten top edges, darken bottom edges.
    emboss = letters.filter(ImageFilter.GaussianBlur(3))
    up = ImageChops.subtract(emboss, ImageChops.offset(emboss, 0, 4))
    down = ImageChops.subtract(emboss, ImageChops.offset(emboss, 0, -4))
    hi = Image.new("RGBA", (w, h), (255, 245, 210, 0))
    hi.putalpha(ImageChops.multiply(down, letters))
    lo = Image.new("RGBA", (w, h), (60, 30, 0, 0))
    lo.putalpha(ImageChops.multiply(up, letters))
    outline = mask.filter(ImageFilter.MaxFilter(15))
    glow = mask.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(28))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gl = Image.new("RGBA", (w, h), (200, 40, 30, 0))
    gl.putalpha(glow.point(lambda v: int(v * 0.85)))
    out = Image.alpha_composite(out, gl)
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sh.putalpha(ImageChops.offset(outline, 0, 10).filter(ImageFilter.GaussianBlur(6)))
    out = Image.alpha_composite(out, sh)
    ol = Image.new("RGBA", (w, h), (18, 10, 8, 0))
    ol.putalpha(outline)
    out = Image.alpha_composite(out, ol)
    out = Image.alpha_composite(out, fill_img)
    out = Image.alpha_composite(out, hi)
    out = Image.alpha_composite(out, lo)
    bbox = out.getbbox()
    out = out.crop((bbox[0] - 8, bbox[1] - 8, bbox[2] + 8, bbox[3] + 8))
    target_w = 1280
    out = out.resize((target_w, int(out.height * target_w / out.width)), Image.LANCZOS)
    pad_h = (out.height + 3) // 4 * 4
    canvas = Image.new("RGBA", (target_w, pad_h), (0, 0, 0, 0))
    canvas.paste(out, (0, 0))
    return canvas


def make_corner():
    """Gold filigree for the top-left corner of a panel (flip it for the other corners)."""
    s = 96 * SUPER
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gold = (214, 176, 98, 255)
    w = 3 * SUPER
    d.line((6 * SUPER, 6 * SUPER, s * 0.9, 6 * SUPER), fill=gold, width=w)
    d.line((6 * SUPER, 6 * SUPER, 6 * SUPER, s * 0.9), fill=gold, width=w)
    d.line((14 * SUPER, 14 * SUPER, s * 0.55, 14 * SUPER), fill=gold, width=2 * SUPER)
    d.line((14 * SUPER, 14 * SUPER, 14 * SUPER, s * 0.55), fill=gold, width=2 * SUPER)
    d.arc((14 * SUPER, 14 * SUPER, 50 * SUPER, 50 * SUPER), 180, 270, fill=gold, width=2 * SUPER)
    c = 14 * SUPER
    r = 7 * SUPER
    d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=(255, 220, 140, 255), outline=(80, 50, 10, 255))
    for k in range(3):
        x = (30 + k * 16) * SUPER
        d.ellipse((x - 2 * SUPER, 6 * SUPER - 2 * SUPER, x + 2 * SUPER, 6 * SUPER + 2 * SUPER), fill=gold)
        d.ellipse((6 * SUPER - 2 * SUPER, x - 2 * SUPER, 6 * SUPER + 2 * SUPER, x + 2 * SUPER), fill=gold)
    img = shade_vertical(img, 1.15, 0.9)
    return img.resize((96, 96), Image.LANCZOS)


def make_divider():
    w, h = 1024 * 2, 48 * 2
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cy = h / 2
    for x in range(w):
        t = abs(x - w / 2) / (w / 2)
        a = int(255 * max(0.0, 1.0 - t) ** 0.7)
        d.line((x, cy - 2, x, cy + 2), fill=(214, 176, 98, a))
    r = 18
    d.polygon([(w / 2, cy - r), (w / 2 + r * 1.4, cy), (w / 2, cy + r), (w / 2 - r * 1.4, cy)],
              fill=(255, 210, 120, 255), outline=(60, 34, 6, 255), width=3)
    for side in (-1, 1):
        x = w / 2 + side * 60
        d.polygon([(x, cy - 9), (x + 12, cy), (x, cy + 9), (x - 12, cy)], fill=(214, 176, 98, 255))
    return img.resize((1024, 48), Image.LANCZOS)


def make_vignette():
    s = 512
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    nx = (xx - s / 2) / (s / 2)
    ny = (yy - s / 2) / (s / 2)
    d = np.sqrt(nx * nx * 0.9 + ny * ny * 1.1)
    a = np.clip((d - 0.55) / 0.75, 0, 1) ** 1.4
    rgba = np.dstack([np.full((s, s), 150), np.full((s, s), 8), np.full((s, s), 12), a * 255]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def make_chevron():
    s = 128 * SUPER
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i, alpha in ((0, 255), (1, 170)):
        ox = i * 34 * SUPER
        pts = [(30 * SUPER + ox, 22 * SUPER), (64 * SUPER + ox, 64 * SUPER), (30 * SUPER + ox, 106 * SUPER),
               (48 * SUPER + ox, 106 * SUPER), (82 * SUPER + ox, 64 * SUPER), (48 * SUPER + ox, 22 * SUPER)]
        d.polygon(pts, fill=(255, 74, 42, alpha))
    glow = img.getchannel("A").filter(ImageFilter.GaussianBlur(8 * SUPER))
    g = Image.new("RGBA", (s, s), (255, 40, 20, 0))
    g.putalpha(glow.point(lambda v: int(v * 0.8)))
    out = Image.alpha_composite(g, img)
    return out.resize((128, 128), Image.LANCZOS)


def make_marker():
    """An arrowhead pointing right with a round socket for an icon (off-screen markers)."""
    s = 128 * SUPER
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = s / 2
    d.polygon([(c + 58 * SUPER, c), (c + 18 * SUPER, c - 32 * SUPER), (c + 18 * SUPER, c + 32 * SUPER)],
              fill=(255, 255, 255, 255))
    d.ellipse((c - 38 * SUPER, c - 38 * SUPER, c + 38 * SUPER, c + 38 * SUPER), fill=(255, 255, 255, 255))
    a = img.getchannel("A")
    ol = Image.new("RGBA", (s, s), (10, 8, 12, 255))
    ol.putalpha(a.filter(ImageFilter.MaxFilter(6 * SUPER + 1)))
    inner = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(inner).ellipse((c - 30 * SUPER, c - 30 * SUPER, c + 30 * SUPER, c + 30 * SUPER), fill=(18, 14, 22, 255))
    out = Image.alpha_composite(ol, img)
    out = Image.alpha_composite(out, inner)
    return out.resize((128, 128), Image.LANCZOS)


def make_sweep():
    """The dark wedge of the active ability's radial cooldown (fill-image of a clockwise progress)."""
    s = 128 * SUPER
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((0, 0, s - 1, s - 1), radius=26 * SUPER, fill=(6, 4, 10, 185))
    return img.resize((128, 128), Image.LANCZOS)


def make_contact_sheet(entries, path):
    cols = 10
    cell_w, cell_h = 168, 178
    rows = (len(entries) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell_w + 40, rows * cell_h + 110), (14, 11, 18, 255))
    d = ImageDraw.Draw(sheet)
    title = font("Inter-Black.ttf", 40)
    label = font("Inter-SemiBold.ttf", 14)
    d.text((20, 22), f"{GAME_TITLE} - UI ICONS (128 px)", font=title, fill=(255, 201, 76))
    for i, (icon_id, category, img) in enumerate(entries):
        x = 20 + (i % cols) * cell_w + (cell_w - ICON) // 2
        y = 90 + (i // cols) * cell_h
        if category == "glyph":
            d.rounded_rectangle((x - 2, y - 2, x + ICON + 2, y + ICON + 2), radius=10, fill=(30, 26, 38))
        sheet.alpha_composite(img, (x, y))
        tw = d.textlength(icon_id, font=label)
        d.text((20 + (i % cols) * cell_w + (cell_w - tw) / 2, y + ICON + 8), icon_id, font=label, fill=(220, 210, 190))
        cw = d.textlength(category, font=label)
        d.text((20 + (i % cols) * cell_w + (cell_w - cw) / 2, y + ICON + 26), category, font=label, fill=(140, 130, 150))
    sheet.save(path)


def main():
    for folder in (OUT_ICONS, OUT_ART, OUT_FONTS, OUT_GLYPHS):
        os.makedirs(folder, exist_ok=True)
    render = np.array(Image.open(HEROES_RENDER).convert("RGB")).astype(np.int32)
    portraits = {}
    for hero, box in PORTRAIT_CROPS.items():
        figure = matte_hero(render, box)
        portrait = make_portrait(hero, figure, 384)
        portrait.save(os.path.join(OUT_ICONS, hero + "_portrait.png"))
        portraits[hero] = portrait
    entries = []
    badge_cache = {}
    for index, (icon_id, category, source) in enumerate(ICONS):
        kind, name = source.split(":", 1)
        if kind == "portrait":
            img = make_hero_badge(portraits[name])
        else:
            if kind == "rpg":
                glyph = load_rpg_glyph(name)
            else:
                glyph = VECTORS[name]().resize((ICON, ICON), Image.LANCZOS)
            if category == "glyph":
                img = compose_icon(None, glyph) if kind == "rpg" else glyph
            else:
                if category not in badge_cache:
                    badge_cache[category] = make_badge(category, 11 + len(badge_cache))
                img = compose_icon(badge_cache[category], glyph)
        img.save(os.path.join(OUT_ICONS, icon_id + ".png"))
        entries.append((icon_id, category, img))
    make_contact_sheet(entries, os.path.join(HERE, "icons_sheet.png"))
    make_logo().save(os.path.join(OUT_ART, "logo.png"))
    make_corner().save(os.path.join(OUT_ART, "corner.png"))
    make_divider().save(os.path.join(OUT_ART, "divider.png"))
    make_vignette().save(os.path.join(OUT_ART, "vignette.png"))
    make_chevron().save(os.path.join(OUT_ART, "chevron.png"))
    make_marker().save(os.path.join(OUT_ART, "marker.png"))
    make_sweep().save(os.path.join(OUT_ART, "sweep.png"))
    skull_red = vec_skull((235, 70, 60)).resize((ICON, ICON), Image.LANCZOS)
    skull_red.save(os.path.join(OUT_ICONS, "dread.png"))
    for name in KENNEY_COPY:
        src = os.path.join(KENNEY_GLYPHS, name + ".png")
        if os.path.exists(src):
            shutil.copyfile(src, os.path.join(OUT_GLYPHS, name + ".png"))
    for name in ("Inter-Black.ttf", "Inter-ExtraBold.ttf", "Inter-SemiBold.ttf", "Inter-Medium.ttf"):
        shutil.copyfile(os.path.join(INTER_STATIC, name), os.path.join(OUT_FONTS, name))
    shutil.copyfile(INTER_OFL, os.path.join(OUT_FONTS, "Inter-OFL.txt"))
    print("icons:", len(entries) + 1, "portraits:", len(portraits))


if __name__ == "__main__":
    if sys.argv[1:] == ["logo"]:
        make_logo().save(os.path.join(OUT_ART, "logo.png"))
        sys.exit(0)
    sys.exit(main())
