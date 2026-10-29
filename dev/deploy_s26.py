"""Open the project in the running editor, push the UI and deploy to the S26: python deploy_s26.py"""
import json
import sys
import time

sys.path.insert(0, "..")
from mcp import call

DEVICE = "adb-R3GL508T4SY-pTACbH._adb-tls-connect._tcp"
PROJECT = "C:/Workspace/github/UnravelEngine/UnravelEngine-Sample-Survivors"


def wait_for_project():
    status = {}
    for _ in range(240):
        try:
            status = json.loads(call("editor_get_status", {}))
            if "project" in status and not status["project"].get("open"):
                call("project_open", {"path": PROJECT})
                time.sleep(2)
                continue
            if status.get("scene", {}).get("entity_count", 0) > 1000:
                return status
        except Exception:
            pass
        time.sleep(1)
    return status


def main():
    print(wait_for_project().get("scene"))
    print(call("scripts_get_compile_status", {"wait": True, "timeout_ms": 180000})[:80])
    print(call("deploy_set_settings", {"android_device": DEVICE})[:200])
    print(call("project_deploy", {})[:300])
    last = ""
    for _ in range(900):
        state = call("deploy_get_state", {})
        if state != last:
            print(state[:300])
            last = state
        parsed = json.loads(state)
        if not parsed.get("running", parsed.get("busy", False)) and parsed.get("finished", parsed.get("done", True)):
            break
        time.sleep(2)


if __name__ == "__main__":
    main()
