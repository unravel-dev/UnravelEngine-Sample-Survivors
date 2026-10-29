"""One-off edit of build_audio.py: sub-bass layers through sub() at a fixed lower level, a lighter crossbow hiss and a
mid-range layer under the King's roars (they measured 7% energy between 300 Hz and 3 kHz)."""
import os
import re

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "build_audio.py")
text = open(path, encoding="utf-8").read()
count = len(re.findall(r"normalize\(sub_boom\(([^()]*)\)\)", text))
text = re.sub(r"normalize\(sub_boom\(([^()]*)\)\)", r"sub(\1)", text)
helper = '''def sub(*args, **kwargs):
    """A sub-bass thump layer (< 80 Hz) at SUB_LEVEL under a peak-normalized mix: felt, not dominating."""
    return normalize(sub_boom(*args, **kwargs)) * db(SUB_LEVEL)


def thump(seconds, f0, f1, tau, attack=0.002):'''
text = text.replace("def thump(seconds, f0, f1, tau, attack=0.002):", helper, 1)
text = text.replace("RND = dsp.RNG\n", "RND = dsp.RNG\nSUB_LEVEL = -9.0\n", 1)
text = text.replace("from dsp import (BELL_CHIME,", "from dsp import (db, BELL_CHIME,", 1)
text = text.replace('x = mix([(normalize(twang), 0, 0), (normalize(hiss), 0, -5), (S(KRPG + "metalLatch.ogg", length=0.06, fade=0.03), 0, -10)])',
                    'x = mix([(normalize(twang), 0, 0), (normalize(hiss), 0, -11), (S(KRPG + "metalLatch.ogg", length=0.06, fade=0.03), 0, -10)])', 1)
old_roar = '''             (saturate(S(R80 + "creature_roar_01.ogg", pitch=0.55, length=1.2, fade=0.4), 2.5), 0.04, -3),'''
new_roar = old_roar + '''
             (saturate(S(R80 + "creature_roar_02.ogg", pitch=0.85, length=1.2, fade=0.4, hp=300.0), 3.0), 0.02, -5),'''
assert old_roar in text
text = text.replace(old_roar, new_roar, 1)
old_enrage = '''    x = mix([(saturate(S(R80 + "creature_roar_03.ogg", pitch=0.6, length=1.6, fade=0.5), 2.0), 0, 0),'''
new_enrage = old_enrage + '''
             (saturate(S(R80 + "creature_roar_01.ogg", pitch=0.9, length=1.2, fade=0.4, hp=300.0), 3.0), 0.02, -6),'''
assert old_enrage in text
text = text.replace(old_enrage, new_enrage, 1)
open(path, "w", encoding="utf-8", newline="\n").write(text)
print(f"sub layers rerouted: {count}")
