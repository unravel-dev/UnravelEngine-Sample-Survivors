"""Captures the title and hero select (each class focused) for the menu stage review."""
import sys
import time

from playutil import call, start_play, stop_play, capture_game
from lineup import invoke
from smoke import set_auto


def main():
    set_auto(False)
    print("play", start_play())
    time.sleep(2.0)
    print(capture_game("menu_title"))
    for heroClass in [int(a) for a in sys.argv[1:]] or [0, 2]:
        print(invoke("DevHeroSelect", heroClass)[:120])
        time.sleep(1.2)
        print(capture_game(f"menu_select_{heroClass}"))
    stop_play()


if __name__ == "__main__":
    main()
