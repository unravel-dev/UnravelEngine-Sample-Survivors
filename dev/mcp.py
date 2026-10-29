"""Calls one tool of the editor's MCP server: python mcp.py <tool> '<json arguments>'."""
import json
import sys
import urllib.request

URL = "http://127.0.0.1:27182/mcp"


def call(tool, arguments):
    body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": "tools/call",
                       "params": {"name": tool, "arguments": arguments}}).encode("utf-8")
    request = urllib.request.Request(URL, data=body, headers={
        "Content-Type": "application/json", "Accept": "application/json, text/event-stream"})
    with urllib.request.urlopen(request, timeout=600) as response:
        text = response.read().decode("utf-8")
    for line in text.splitlines():
        if line.startswith("data:"):
            text = line[5:].strip()
    result = json.loads(text)
    if "error" in result:
        return json.dumps(result["error"])
    lines = []
    for index, item in enumerate(result["result"].get("content", [])):
        if item.get("type") == "image":
            import base64
            import os
            path = os.path.join(os.path.dirname(os.path.abspath(__file__)), f"capture_{tool}_{index}.png")
            with open(path, "wb") as image:
                image.write(base64.b64decode(item["data"]))
            lines.append(f"image saved: {path}")
        else:
            lines.append(item.get("text", ""))
    return "\n".join(lines)


if __name__ == "__main__":
    print(call(sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}))
