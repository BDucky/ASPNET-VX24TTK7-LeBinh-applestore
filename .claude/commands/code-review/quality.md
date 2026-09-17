---
description: "Run parallel quality review with 3 specialized code-reviewer agents"
---

Run a comprehensive quality review using 3 parallel code-reviewer agents. Each agent focuses on a different aspect of code quality.

## Instructions

### Step 1: Determine review scope

- Run `git diff --name-only origin/main...HEAD -- . ':!.ai/' ':!.claude/'` to get changed files
- Run `git diff origin/main...HEAD -- . ':!.ai/' ':!.claude/'` to get the full diff

If there are no changes, inform the user and stop.

### Step 2: Launch 3 code-reviewer agents in parallel

Launch 3 code-reviewer agents in parallel with different focuses. Each agent receives the list of changed files and the full diff content.

**Agent 1 — Simplicity, DRY & Elegance:**
```
Review the following code changes focusing ONLY on simplicity, DRY principles, and code elegance. Ignore bugs, conventions, and architecture concerns.

Changed files:
<list of changed files>

Diff:
<full diff content>
```

**Agent 2 — Bugs & Functional Correctness:**
```
Review the following code changes focusing ONLY on bugs and functional correctness. Ignore style, conventions, and architecture concerns.

Changed files:
<list of changed files>

Diff:
<full diff content>
```

**Agent 3 — Project Conventions & Architecture:**
```
Review the following code changes focusing ONLY on project conventions and architecture compliance. Read CLAUDE.md first. Ignore bugs and simplicity concerns.

Changed files:
<list of changed files>

Diff:
<full diff content>
```

### Step 3: Consolidate results

After all 3 agents complete, consolidate their findings into a single report:

1. **Deduplicate** — remove issues reported by multiple agents (keep the most detailed version)
2. **Group by severity** — Critical issues first, then Important
3. **Format** the report as:

```
## Quality Review Report

**Scope:** branch changes vs origin/main
**Files reviewed:** <count>

### Critical Issues
- [Issue description] — `file:line` — [Agent focus area]
  - Fix: [specific suggestion]

### Important Issues
- [Issue description] — `file:line` — [Agent focus area]
  - Fix: [specific suggestion]

### Summary
[Brief overall assessment]
```

If no issues are found, state that the code looks good with a brief summary.

### Step 4: Ask user for next steps

Ask the user what they'd like to do:
1. **Fix now** — address the issues immediately
2. **Fix later** — acknowledge and move on
3. **Proceed as-is** — dismiss the findings
