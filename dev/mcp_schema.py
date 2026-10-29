"""Prints the description and input schema of MCP tools: python mcp_schema.py <tool> [<tool> ...]."""
import json
import sys
import urllib.request

body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": "tools/list"}).encode()
request = urllib.request.Request("http://127.0.0.1:27182/mcp", data=body, headers={
    "Content-Type": "application/json", "Accept": "application/json, text/event-stream"})
text = urllib.request.urlopen(request).read().decode()
for line in text.splitlines():
    if line.startswith("data:"):
        text = line[5:]
for tool in json.loads(text)["result"]["tools"]:
    if tool["name"] in sys.argv[1:]:
        print("==", tool["name"])
        print(tool["description"])
        print(json.dumps(tool["inputSchema"]))
        print()
