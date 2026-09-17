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

## Branch protection: requested, not yet configured

Branch protection needs admin (or maintain) access on the GitHub repository.
The account currently pushing this work (`binhlerowboatsoftware`) has `push` and
`triage` access but not `admin`, so it cannot set protection rules through the
GitHub API (confirmed: `PUT /repos/BDucky/apple-store/branches/{branch}/protection`
returns 404, which is GitHub's way of saying "not visible to you," i.e. not an
admin here).

Whoever has admin on `BDucky/apple-store` (repo owner or an org owner) needs to
set this up once, in the GitHub web UI:

1. Go to **Settings > Branches** on the repository.
2. Add a branch protection rule for `main`:
   - Branch name pattern: `main`
   - Require a pull request before merging (check this)
   - Required approvals: 0 to start (raise to 1+ once more of the team is
     actively reviewing; 0 still blocks direct pushes, it just does not gate on
     a reviewer)
   - Do not allow force pushes
   - Do not allow deletions
3. Repeat for `dev` with the same settings.
4. Optionally, once a CI workflow exists (none is set up yet, see
   `docs/roadmap.md`), add "require status checks to pass" for both rules.

Until this is configured on GitHub's side, protection is enforced by convention:
every session working on this repo (human or Claude Code) follows the flow above
and never pushes directly to `main` or `dev`, even though the platform is not yet
stopping it.
