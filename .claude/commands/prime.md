---
description: Gain a general understanding of the apple-store codebase
---

# Prime

Execute the `Workflow` and `Report` sections to understand the codebase then summarize your understanding.

## Workflow

- Run `git ls-files | grep -v -E '^(\.claude/|\.ai/)' | head -100` to get an overview of repository files.
- Read these key documentation files:
  - `README.md`
  - `CLAUDE.md`
  - `docs/requirements.md`
  - `docs/data-model.md`
  - `docs/architecture.md`
  - `docs/roadmap.md`

## Report

Summarize your understanding of the codebase including:
- Overall architecture and how the four projects relate to each other
- The data model and its known gaps (see docs/data-model.md)
- Development workflow and conventions
- Which roadmap milestone, if any, the current branch or working state is on
