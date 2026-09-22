# Branching and pull requests

## Branch structure

- `main`: stable branch. Represents a working, reviewed state of the project.
- `dev`: integration branch. Feature branches merge here first.
- `feat/<slug>`, `fix/<slug>`, `chore/<slug>`: short-lived branches for one use case
  or one task each, branched from `dev`.

Flow: `feat/<slug>` opens a pull request into `dev`. When a set of features on
`dev` is ready, `dev` opens a pull request into `main`. Neither `main` nor `dev`
should ever receive a direct push once branch protection is turned on (see below).

## Origin of the scaffold commits

The initial 82+ scaffold commits (solution structure, all entities, EF
configuration, docs, ported tooling) were pushed directly to `main` before this
branching rule existed. That is the one documented exception. Everything after
`dev` was created follows the flow above.

## Branch protection: configured

Set up by `BDucky` (repo admin) on 2026-09-22 via GitHub's Rulesets UI
(**Settings > Rules > Rulesets**), one ruleset named `main` targeting both
`main` and `dev`. The account this session pushes as (`binhlerowboatsoftware`)
still only has `push`/`triage`, not `admin`, and could not have set this up
(confirmed earlier via a 404 on the branch-protection API); the ruleset had to
be created by the admin account instead.

Verified active via `gh api repos/BDucky/ASPNET-VX24TTK7-LeBinh-applestore/rules/branches/<branch>`
for both `main` and `dev`, which lists all three rule types below.

Current rules, both branches:

- **Require a pull request before merging**, 0 required approvals for now
  (raise to 1+ once more of the team is actively reviewing; 0 still blocks
  direct pushes, it just does not gate on a reviewer).
- **Block force pushes**.
- **Restrict deletions**.

Not yet added: "require status checks to pass," since no CI workflow exists
yet (see `docs/roadmap.md`). Add that rule to the same ruleset once CI lands.

To change these settings later: **Settings > Rules > Rulesets > main** on the
repository.
