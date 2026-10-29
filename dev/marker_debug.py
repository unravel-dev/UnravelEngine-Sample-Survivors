import json, time
from playutil import call, start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto
stop_play(); set_auto(True, "Knight", 30.0); start_play(); time.sleep(1.5)
invoke("DevInvulnerable", 1000.0)
invoke("DevSpawns", False)
invoke("DevDropChest", 3, 6.0)
time.sleep(1.0)
r = json.loads(call("ui_query_elements", {"selector": ".marker"}))
for e in r["elements"][:3]:
    print(e["element"], e["visible"], e["rect_game"])
print(json.loads(invoke("DevReport")).get("result")[:80])
print(capture_game("marker_debug", 1600, 900))
stop_play(); set_auto(False)
