---
description: "Summarize merged PRs on a branch for team notification. Args: [branch] [--since=DATE]"
allowed-tools: Bash(git:*), Bash(gh:*), AskUserQuestion
---

# Summary of Merged PRs

Generate a chat-ready summary of all PRs merged into a target branch, categorized by type (Feature, Fix, Performance, Chore).

Adapted from corjl-webapp's version of this command: the Jira-ticket extraction
and the hardcoded `CorjlSoftware/corjl-webapp` repo are dropped since this
project has neither Jira nor a fixed org. The repo is resolved dynamically.

## Arguments

- `branch` - Target branch to summarize (optional, will prompt if not provided)
- `--since=DATE` - Only show merges since this date, e.g. `--since=2026-04-01` (optional, defaults to today)

## Instructions

### Step 1: Determine Target Branch

Parse `$ARGUMENTS` for the branch name and `--since` flag.

**If branch is provided in arguments**, use it directly. Default to `main` if no branch argument is given (this repo has no dev/stage split, see CLAUDE.md's git workflow section).

Do NOT prompt the user to choose a branch, just use `main` by default. Only ask if the argument is ambiguous.

### Step 2: Determine Date Range

- If `--since` flag is provided, use that date
- If not provided, default to **today** (current date)

### Step 3: Resolve the repo and fetch merged PRs

```bash
# Resolve owner/repo dynamically, don't hardcode it
gh repo view --json nameWithOwner -q .nameWithOwner

# Fetch latest
git fetch origin <branch> 2>&1 | tail -3

# Get merged PRs for the date range (use 00:00:00 to include the full day,
# plain --since="YYYY-MM-DD" can miss commits due to timezone issues)
git log origin/<branch> --format="%h %ad %s" --date=iso --merges --since="<since_date> 00:00:00" | grep "Merge pull request"
```

If no PRs found, tell the user:
> No PRs were merged into the target branch since the given date.

And stop.

### Step 4: Get PR Details

For each PR number found, fetch details from GitHub:

```bash
gh pr view <PR_NUMBER> --json title,body,labels,number
```

From the PR body, use the "Summary" section if available, otherwise use the PR title.

### Step 5: Categorize PRs

Categorize each PR based on its branch name prefix or PR title prefix:

| Prefix | Category |
|--------|----------|
| `feat/`, `feature/`, `feat(` | Feature |
| `fix/`, `hotfix/`, `fix(` | Fix |
| `perf/`, `perf(` | Performance |
| `chore/`, `chore(` | Chore/Update |
| `refactor/`, `refactor(` | Refactor |
| `docs/`, `docs(` | Documentation |

### Step 6: Generate the merge report

Generate a plain, copyable text block with the branch name and date as a
heading, then one section per non-empty category listing each PR number,
title, and a one-to-two sentence description pulled from the PR body, plus a
closing "Testing Notes" section telling the reader what to verify for each PR.

**Formatting rules:**
- Only include category sections that have PRs, skip empty sections
- Keep each PR's description to one or two sentences, written for a teammate (what changed from a user perspective), not implementation detail
- Wrap the whole thing in a markdown code block so it is easy to copy into whatever chat tool the team uses

### Step 7: Present to User

Show the report inside a code block.

## Arguments

$ARGUMENTS
