"""Stress: auto-start, invulnerable hero, N enemies, measure stats."""
import json, sys, time
from playutil import call, start_play, stop_play, capture_game
from smoke import set_auto
from lineup import invoke

count = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
set_auto(True)
print("play", start_play())
time.sleep(0.5)
invoke("DevInvulnerable", 300.0)
print(invoke("DevSpawnMany", count, 8.0, 40.0)[:100])
time.sleep(4.0)
samples = []
for i in range(5):
    st = json.loads(call("stats_get", {}))
    samples.append((st["frame"]["frame_ms"], st["frame"]["gpu_ms"], st["frame"]["cpu_submit_ms"], st["draw"]["draw_calls"], st["draw"]["triangles"]))
    time.sleep(0.5)
for s in samples:
    print("frame %.2f gpu %.2f cpu %.2f draws %d tris %d" % s)
prof = call("profiler_get_cpu_scopes", {})
print(prof[:1500])
print(capture_game(f"stress_{count}", 1600, 900))
stop_play()
set_auto(False)
