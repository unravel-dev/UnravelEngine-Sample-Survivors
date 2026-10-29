"""Icons for the class update: 8 class weapons, 8 evolutions, 13 boons (teal badge)."""
import os

from patchlib import edit

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tools", "ui", "build_ui_art.py")

edit(ART, [
    ("""    "hero": ((40, 34, 52), (10, 8, 14), (201, 164, 92), (200, 170, 120)),
}""", """    "hero": ((40, 34, 52), (10, 8, 14), (201, 164, 92), (200, 170, 120)),
    "boon": ((24, 74, 62), (6, 22, 18), (120, 232, 190), (110, 240, 190)),
}"""),
    ("""    ("assassins_mark", "weapon", "rpg:S_Shadow13"),
""", """    ("assassins_mark", "weapon", "rpg:S_Shadow13"),
    ("consecration", "weapon", "rpg:S_Holy06"),
    ("dawn_hammer", "weapon", "rpg:W_Mace009"),
    ("war_cry", "weapon", "rpg:S_Buff05"),
    ("butchers_chains", "weapon", "rpg:W_Throw001"),
    ("arc_lash", "weapon", "rpg:S_Thunder03"),
    ("prismatic_ray", "weapon", "rpg:S_Light04"),
    ("venom_shivs", "weapon", "rpg:S_Dagger03"),
    ("shadow_dancers", "weapon", "rpg:S_Shadow09"),
"""),
    ("""    ("wisp_choir", "evolution", "rpg:S_Shadow07"),
""", """    ("wisp_choir", "evolution", "rpg:S_Shadow07"),
    ("eternal_sanctum", "evolution", "rpg:S_Holy10"),
    ("skybreaker", "evolution", "rpg:S_Thunder05"),
    ("warlords_bellow", "evolution", "rpg:S_Buff09"),
    ("maw_of_the_pit", "evolution", "rpg:S_Shadow05"),
    ("tempest_crown", "evolution", "rpg:S_Thunder06"),
    ("aurora_lance", "evolution", "rpg:S_Magic08"),
    ("nightshade_plague", "evolution", "rpg:S_Poison03"),
    ("legion_of_shadows", "evolution", "rpg:S_Shadow14"),
"""),
    ("""    ("battle_hymn", "fallback", "rpg:S_Holy05"),
""", """    ("battle_hymn", "fallback", "rpg:S_Holy05"),
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
"""),
])
print("icon list patched")
