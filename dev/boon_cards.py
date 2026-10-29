import time
from playutil import start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto
stop_play(); set_auto(True, "Mage", 120.0); start_play(); time.sleep(1.5)
invoke("DevInvulnerable", 1000.0)
invoke("DevSpawns", False)
invoke("DevBoon", "boon_edge", 3)
invoke("DevBoonCards", True)
invoke("DevScreen", "levelup")
time.sleep(1.0)
print(capture_game("boon_cards", 1600, 900))
stop_play(); set_auto(False)
