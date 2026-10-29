"""Wait for the editor, open the survivors project and wait for its scripts: python open_project.py"""
import sys

sys.path.insert(0, "..")
from mcp import call
from deploy_s26 import wait_for_project

print(wait_for_project().get("scene"))
print(call("scripts_get_compile_status", {"wait": True, "timeout_ms": 180000})[:120])
