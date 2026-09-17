#!/usr/bin/env python3
"""Resolve library name to Context7 library ID."""
import os
import sys
import json
import argparse
import ssl
import subprocess
import urllib.request

def get_api_key():
    """Get API key from env or 1Password."""
    key = os.environ.get("CONTEXT7_API_KEY")
    if key:
        return key

    # Fetch from 1Password
    try:
        result = subprocess.run(
            ["op", "read", "op://Personal/MCP/context7_api_key", "--account", "corjl-software.1password.com"],
            capture_output=True, text=True, check=True
        )
        return result.stdout.strip()
    except Exception:
        return None

API_KEY = get_api_key()
MCP_URL = "https://mcp.context7.com/mcp"

# SSL context for macOS
SSL_CONTEXT = ssl.create_default_context()
SSL_CONTEXT.check_hostname = False
SSL_CONTEXT.verify_mode = ssl.CERT_NONE


def mcp_request(method, params):
    data = json.dumps({
        "jsonrpc": "2.0",
        "id": 1,
        "method": method,
        "params": params
    }).encode()

    req = urllib.request.Request(
        MCP_URL,
        data=data,
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
            "CONTEXT7_API_KEY": API_KEY
        },
        method="POST"
    )

    try:
        with urllib.request.urlopen(req, context=SSL_CONTEXT, timeout=30) as response:
            return json.loads(response.read())
    except urllib.error.HTTPError as e:
        return {"error": str(e), "status": e.code}


def main():
    parser = argparse.ArgumentParser(description="Resolve library name to Context7 ID")
    parser.add_argument("library_name", help="Library name to search (e.g., 'react', 'nextjs')")
    args = parser.parse_args()

    if not API_KEY:
        print(json.dumps({"error": "CONTEXT7_API_KEY not set"}))
        sys.exit(1)

    result = mcp_request("tools/call", {
        "name": "resolve-library-id",
        "arguments": {
            "libraryName": args.library_name
        }
    })

    if "error" in result:
        print(json.dumps(result))
        sys.exit(1)

    # Extract content from MCP response
    content = result.get("result", {}).get("content", [])
    if content and len(content) > 0:
        text = content[0].get("text", "")
        print(text)
    else:
        print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
