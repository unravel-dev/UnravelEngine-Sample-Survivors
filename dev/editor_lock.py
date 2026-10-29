"""One editor, several agents: run a command while holding the editor lock.

usage: python editor_lock.py OWNER COMMAND [ARGS...]
  e.g. python editor_lock.py knight python push.py scripts
       python editor_lock.py knight python gallery.py Knight consecration

Every command that talks to the editor (push.py, gallery.py, effect builders, play mode, captures) must run through
this wrapper. It waits (up to 40 min) for the lock file editor.lock, runs the command, and always releases the lock.
A lock older than 20 minutes is treated as abandoned. Keep each locked command short (one push, one gallery run).
"""
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
LOCK = os.path.join(HERE, "editor.lock")
STALE_SECONDS = 20 * 60
WAIT_SECONDS = 40 * 60
POLL_SECONDS = 2.0


def acquire(owner):
    deadline = time.time() + WAIT_SECONDS
    announced = False
    while time.time() < deadline:
        try:
            fd = os.open(LOCK, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            with os.fdopen(fd, "w") as f:
                json.dump({"owner": owner, "pid": os.getpid(), "since": time.time()}, f)
            return True
        except FileExistsError:
            try:
                with open(LOCK) as f:
                    holder = json.load(f)
                if time.time() - holder.get("since", 0) > STALE_SECONDS:
                    os.remove(LOCK)
                    continue
                if not announced:
                    print(f"[editor_lock] waiting for {holder.get('owner')}", flush=True)
                    announced = True
            except (OSError, ValueError):
                pass
            time.sleep(POLL_SECONDS)
    return False


def release():
    try:
        os.remove(LOCK)
    except OSError:
        pass


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    owner = sys.argv[1]
    if not acquire(owner):
        raise SystemExit("[editor_lock] gave up waiting for the editor")
    try:
        code = subprocess.call(sys.argv[2:], cwd=HERE)
    finally:
        release()
    sys.exit(code)


if __name__ == "__main__":
    main()
