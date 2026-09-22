# Course submission requirements

Source: section 4 ("Thuc hien va nop do an") of the HCMUTE "Cong nghe phan mem"
project brief, 2025. This document transcribes those requirements and
tracks the repo's compliance with each one. When this file and the brief
disagree, the brief wins; update this file, not the other way round.

## 4.1 Doing the project: GitHub management

The brief requires the project be managed on GitHub, with five specific
obligations.

### a) Repository name

Required syntax: `ASPNET-<malop>-<hotenkhongdau>-<shortname>`
(example given in the brief: `ASPNET-da21tta-nguyenngocduyen-DEMO`).

This team's values (class code and name: user, asked directly; shortname:
delegated to Claude, "choose this yourself for the best shortname"):

| Field | Value |
|---|---|
| malop | `VX24TTK7` |
| hotenkhongdau | `LeBinh` |
| shortname | `applestore` |
| **Target repo name** | **`ASPNET-VX24TTK7-LeBinh-applestore`** |

**Status: done.** Renamed by `BDucky` (admin) on 2026-09-22, confirmed via
`gh repo view --json name,owner,url`. The old `BDucky/apple-store` URL
redirects to the new one, so existing clones and links keep working. Local
`origin` remotes still need updating manually:

```bash
git remote set-url origin git@github.com:BDucky/ASPNET-VX24TTK7-LeBinh-applestore.git
```

### b) Invite the instructor as a collaborator

Instructor's GitHub-linked email: `antonio86doan@gmail.com`.

**Status: invited, pending acceptance.** User confirmed sending the invite on
2026-09-22. Not independently verifiable from this session: the accepted-
collaborators list only shows `BDucky` and `binhlerowboatsoftware` (invites
don't appear there until accepted), and the pending-invitations endpoint
needs admin rights this session's account doesn't have. Confirm acceptance
once the instructor has had a chance to accept, either in
**Settings > Collaborators** (look for a "Pending" row) or by asking them
directly.

GitHub's collaborator-invite API takes a **username**, not an email, so the
instructor's GitHub username needs to be found first (ask them directly, or
search GitHub for that email if their profile exposes it). Once known,
whoever has admin runs one of:

```bash
# API, once the username is known
gh api -X PUT repos/BDucky/ASPNET-VX24TTK7-LeBinh-applestore/collaborators/<instructor-username> -f permission=pull
```

or, without needing the username up front: **Settings > Collaborators and
teams > Add people** on the repo, search by the email above, and send the
invite from there.

### c) README.md

Must be rewritten to fit this project and **continuously updated** through the
project so that, by report day, a reader can understand what has been built
and reproduce it. Tracked here: root `README.md` now has a "Repository
organization" section (this file) and a team contact section
(see `README.md`); both need to stay current as the project grows, not just
be written once.

### d) Commit regularly

At least one commit per week, "according to work done." Commit history is a
progress-grading criterion. This is stricter than but compatible with the
existing branch/PR flow in `docs/branching.md`: small, frequent commits on
feature branches satisfy both.

### e) Progress reports

A `progress-report/` folder at the repo root, with a weekly report file
uploaded each week. The brief is explicit: **if the commit history doesn't
show update activity for a week, that week's progress report is considered
not completed even if a file was uploaded.** In practice this means: commit
the report file itself into `progress-report/` (not just paste text into a
chat or LMS), on the week it's due.

## 4.2 Submitting the project

"Theo quy dinh" (per the regulations), the brief does not specify further
here. Follow whatever separate submission instructions the course gives
closer to the deadline; nothing to encode yet.

## 4.3 Repository directory tree

Required top-level layout, so instructors and classmates can understand and
rerun the project without guidance:

```
setup/            install/run instructions and test-data files matching what was
                  demoed before the defense committee. If not everything needed
                  can be stored here, include explicit setup instructions and a
                  deployment diagram instead.
scr/              source files and test data files (brief's own directory name;
                  this repo's actual source lives under src/, see scr/README.md)
progress-report/  [required] weekly progress reports
thesis/           [required] project document files
  doc/              .DOC format documents
  pdf/              .PDF format documents
  html/             web-format documents
  abs/              report abstract/slides, e.g. .PPT, .AVI
  refs/             reference material, named by citation or memorable name
  soft/             [if any] related software used during the project
  docker/           [if any] Docker deployment files
```

Plus: the root must have a `README.md` with full team contact info (email,
phone), see the root `README.md`.

`soft/` and `docker/` are created only when there is something to put in them
(the brief marks both "neu co" / if any); do not scaffold empty folders for
those ahead of need.

### Why `scr/` exists alongside `src/`

The brief's own directory guide names the source folder `scr`, which reads as
a typo for `src` (the report's diagram is the only place this spelling
appears). This project's actual source tree is `src/AppleStore.*`, matching
the layout documented in `docs/architecture.md`. Rather than duplicate the
source tree under a second name, `scr/` exists as the required top-level
folder per the brief's literal directory guide, containing only a README that
points to `src/`.

## Compliance status snapshot

| Requirement | Status |
|---|---|
| Repo named `ASPNET-<malop>-<hotenkhongdau>-<shortname>` | Done, 2026-09-22 (see 4.1a) |
| Instructor invited as collaborator | Invited 2026-09-22, pending acceptance (see 4.1b) |
| README.md kept current | In progress, ongoing by nature |
| Weekly commits | Ongoing, see git history |
| `progress-report/` folder, weekly uploads | Folder scaffolded this session, first report still due |
| `setup/`, `scr/`, `thesis/{doc,pdf,html,abs,refs}` | Scaffolded this session |
| Root README has team contact info | Placeholder added, pending real team list |
