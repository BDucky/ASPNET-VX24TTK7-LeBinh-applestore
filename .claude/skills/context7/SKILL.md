# Context7 Skill

## Triggers

Automatically use this skill when user mentions (case-insensitive):
- `context7`, `c7` - Direct reference to Context7
- `fetch docs`, `get documentation` - Documentation requests
- `library docs`, `framework docs` - Library/framework documentation
- `latest docs for [library]` - Up-to-date documentation needs

Fetch up-to-date documentation for any library/framework with intelligent fallback.

## Recommended: Smart Fetch (with Fallback)

### smart_fetch.py
Intelligent wrapper that tries Context7 first, falls back to web URLs if needed.

```bash
# Basic usage - tries Context7, provides fallback URLs if not available
python .claude/skills/context7/scripts/smart_fetch.py "react"

# With topic
python .claude/skills/context7/scripts/smart_fetch.py "nextjs" --topic "routing"

# Check for specific version
python .claude/skills/context7/scripts/smart_fetch.py "vue" --version "3.4"

# Info mode (conceptual guides)
python .claude/skills/context7/scripts/smart_fetch.py "fastapi" --mode info

# Force fallback URLs only (skip Context7)
python .claude/skills/context7/scripts/smart_fetch.py "react" --fallback-only
```

**Output format:**
- If Context7 has docs: Returns documentation directly
- If Context7 unavailable or version mismatch: Returns fallback URLs for WebFetch
- Always includes suggestions for next steps

## Direct Context7 Scripts (Advanced)

### resolve_library.py
Find Context7 library ID from a name.

```bash
python .claude/skills/context7/scripts/resolve_library.py "react"
python .claude/skills/context7/scripts/resolve_library.py "nextjs"
python .claude/skills/context7/scripts/resolve_library.py "fastapi"
```

### get_docs.py
Fetch documentation for a library (requires library ID).

```bash
# Get docs (requires library ID from resolve_library.py)
python .claude/skills/context7/scripts/get_docs.py "/vercel/next.js"

# With topic filter
python .claude/skills/context7/scripts/get_docs.py "/vercel/next.js" --topic "routing"

# Info mode (conceptual guides)
python .claude/skills/context7/scripts/get_docs.py "/vercel/next.js" --mode info

# Pagination
python .claude/skills/context7/scripts/get_docs.py "/vercel/next.js" --topic "hooks" --page 2
```

## Authentication

Scripts auto-fetch token from 1Password: `op://Personal/MCP/context7_api_key`

Or set environment variable:
```bash
export CONTEXT7_API_KEY=<your-key>
```

## Workflow

### Simple Workflow (Recommended)
Use `smart_fetch.py` - it handles everything automatically:
```bash
python .claude/skills/context7/scripts/smart_fetch.py "library-name" --topic "optional-topic"
```

If it returns fallback URLs, use Claude Code's WebFetch:
```
<result from smart_fetch>
{
  "source": "fallback",
  "fallback_urls": ["https://..."],
  "suggestion": "Use WebFetch with these URLs..."
}
```

### Advanced Workflow
1. Resolve library name to ID:
   ```bash
   python .claude/skills/context7/scripts/resolve_library.py "react query"
   ```

2. Fetch docs using the returned ID:
   ```bash
   python .claude/skills/context7/scripts/get_docs.py "/tanstack/query"
   ```

## Fallback Strategy

When Context7 doesn't have docs or version doesn't match:
1. `smart_fetch.py` returns official documentation URLs
2. Use Claude Code's `WebFetch` tool with suggested URLs
3. Or use `WebSearch` with suggested search query

Supported libraries with fallback URLs:
- React, Next.js, Vue, Nuxt, Svelte, Angular
- FastAPI, Django, Flask, Express, NestJS
- TypeScript, Python, Node.js
- Tailwind CSS
- And more...
