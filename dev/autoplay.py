"""Unattended run: auto-start a class, autopilot + autopick at a game speed, sample stats and captures.

python autoplay.py CLASS SECONDS SPEED [invulnerable 0|1] [start_seconds] [tag]
"""
import json, sys, time
from playutil import call, start_play, stop_play, capture_game
from smoke import set_auto, recent_errors
from lineup import invoke

hero = sys.argv[1] if len(sys.argv) > 1 else "Knight"
seconds = float(sys.argv[2]) if len(sys.argv) > 2 else 60.0
speed = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
invulnerable = len(sys.argv) > 4 and sys.argv[4] == "1"
start = float(sys.argv[5]) if len(sys.argv) > 5 else 0.0
tag = sys.argv[6] if len(sys.argv) > 6 else f"auto_{hero}"
set_auto(True, hero, start)
_, after = recent_errors()
print("play", start_play())
time.sleep(0.3)
invoke("DevAutoplay", True, speed)
if invulnerable:
    invoke("DevInvulnerable", 100000.0)
t0 = time.time()
shot = 0
while time.time() - t0 < seconds:
    time.sleep(min(10.0, seconds / 6))
    st = json.loads(call("stats_get", {}))
    status = json.loads(call("editor_get_status", {}))
    print("t=%5.1f frame %.2f gpu %.2f draws %d tris %.1fM" % (time.time() - t0, st["frame"]["frame_ms"], st["frame"]["gpu_ms"], st["draw"]["draw_calls"], st["draw"]["triangles"] / 1e6))
    print(json.loads(invoke("DevReport")).get("result"))
    print(capture_game(f"{tag}_{shot}", 1280, 720)); shot += 1
lines, _ = recent_errors(after)
stop_play()
set_auto(False)
filtered = [l for l in lines if "no sound" not in l and "no emitter" not in l and "data-for" not in l]
print("\n".join(filtered[-40:]))
