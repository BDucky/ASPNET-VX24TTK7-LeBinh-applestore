#!/usr/bin/env python3
"""Intelligent documentation fetcher with Context7 + Web fallback."""
import os
import sys
import json
import argparse
import ssl
import subprocess
import urllib.request
from typing import Dict, Optional

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

# Fallback documentation URLs for popular libraries
FALLBACK_DOCS = {
    "react": "https://react.dev/reference",
    "nextjs": "https://nextjs.org/docs",
    "next.js": "https://nextjs.org/docs",
    "vue": "https://vuejs.org/api/",
    "nuxt": "https://nuxt.com/docs",
    "svelte": "https://svelte.dev/docs",
    "angular": "https://angular.io/docs",
    "fastapi": "https://fastapi.tiangolo.com/",
    "django": "https://docs.djangoproject.com/",
    "flask": "https://flask.palletsprojects.com/",
    "express": "https://expressjs.com/en/api.html",
    "nestjs": "https://docs.nestjs.com/",
    "typescript": "https://www.typescriptlang.org/docs/",
    "python": "https://docs.python.org/3/",
    "javascript": "https://developer.mozilla.org/en-US/docs/Web/JavaScript",
    "node": "https://nodejs.org/docs/latest/api/",
    "nodejs": "https://nodejs.org/docs/latest/api/",
    "tailwind": "https://tailwindcss.com/docs",
    "tailwindcss": "https://tailwindcss.com/docs",
}


def mcp_request(method: str, params: dict) -> dict:
    """Make MCP request to Context7."""
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


def try_context7(library_name: str, topic: Optional[str] = None,
                 mode: str = "code", page: int = 1) -> Optional[str]:
    """Try to fetch docs from Context7. Returns docs text or None."""
    if not API_KEY:
        return None

    # Step 1: Resolve library ID
    resolve_result = mcp_request("tools/call", {
        "name": "resolve-library-id",
        "arguments": {"libraryName": library_name}
    })

    if "error" in resolve_result:
        return None

    content = resolve_result.get("result", {}).get("content", [])
    if not content:
        return None

    library_id = content[0].get("text", "").strip()
    if not library_id or "not found" in library_id.lower():
        return None

    # Step 2: Fetch docs
    arguments = {
        "context7CompatibleLibraryID": library_id,
        "mode": mode,
        "page": page
    }
    if topic:
        arguments["topic"] = topic

    docs_result = mcp_request("tools/call", {
        "name": "get-library-docs",
        "arguments": arguments
    })

    if "error" in docs_result:
        return None

    docs_content = docs_result.get("result", {}).get("content", [])
    if docs_content and len(docs_content) > 0:
        return docs_content[0].get("text", "")

    return None


def get_fallback_urls(library_name: str, topic: Optional[str] = None) -> list:
    """Get fallback documentation URLs."""
    urls = []
    base_url = FALLBACK_DOCS.get(library_name.lower())

    if base_url:
        if topic:
            urls.append(f"{base_url}/{topic}")
        urls.append(base_url)

    # Add search URL as last resort
    search_query = f"{library_name} documentation"
    if topic:
        search_query += f" {topic}"

    return urls


def main():
    parser = argparse.ArgumentParser(
        description="Smart documentation fetcher with Context7 + web fallback"
    )
    parser.add_argument("library_name", help="Library name (e.g., 'react', 'nextjs')")
    parser.add_argument("--topic", help="Specific topic to focus on")
    parser.add_argument("--version", help="Required version (for validation)")
    parser.add_argument("--mode", choices=["code", "info"], default="code",
                        help="Mode: 'code' for API/examples, 'info' for guides")
    parser.add_argument("--page", type=int, default=1, help="Page number (1-10)")
    parser.add_argument("--fallback-only", action="store_true",
                        help="Skip Context7 and return fallback URLs only")
    args = parser.parse_args()

    result = {
        "library": args.library_name,
        "source": None,
        "docs": None,
        "fallback_urls": None,
        "suggestion": None
    }

    # Try Context7 first (unless --fallback-only)
    if not args.fallback_only:
        docs = try_context7(args.library_name, args.topic, args.mode, args.page)

        # Check if docs contain errors
        if docs and not ("error" in docs.lower() or "not found" in docs.lower()):
            result["source"] = "context7"
            result["docs"] = docs

            # Check version if specified
            if args.version and args.version.lower() not in docs.lower():
                result["warning"] = f"Requested version {args.version} not found in docs"
                result["suggestion"] = "Consider using fallback URLs for latest version"
                result["fallback_urls"] = get_fallback_urls(args.library_name, args.topic)

            print(json.dumps(result, indent=2))
            return

    # Fallback: provide URLs for WebFetch
    fallback_urls = get_fallback_urls(args.library_name, args.topic)

    result["source"] = "fallback"
    result["fallback_urls"] = fallback_urls
    result["suggestion"] = (
        f"Context7 docs not available. Use WebFetch with these URLs:\n"
        f"{json.dumps(fallback_urls, indent=2)}\n\n"
        f"Or use WebSearch: '{args.library_name} documentation"
        f"{' ' + args.topic if args.topic else ''}'"
    )

    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
