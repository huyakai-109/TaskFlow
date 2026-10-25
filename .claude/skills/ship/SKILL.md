---
name: ship
description: Release to the project's one configured target — `/ship setup` records the target once, `/ship check` is a read-only readiness report, `/ship` checks, builds, asks for approval, deploys and verifies. User-invoked.
disable-model-invocation: true
argument-hint: "[setup | check]  (empty = full release)"
---

# Ship

Mode: $ARGUMENTS (empty → full release)

The user invoked this; no skill or agent may. Invoking it **requests** the workflow — it does not approve a deployment. Finishing a feature, a passed review or an earlier approval never authorizes this one.

## Who does what

| Step | Done by | Needs the user's explicit approval |
|---|---|---|
| Read target from `CLAUDE.md` → Deployment | you | — |
| Readiness checks, local build | you, or `devops-lead` for infrastructure questions | — |
| Write the Deployment section (`setup`) | you / `devops-lead` | yes — shown as a diff first |
| Commit, push, tag, open/merge PR, publish an image | you | yes, each one |
| Back up, apply a migration to the target | you / `devops-lead` | yes; destructive migration needs a confirmed backup too |
| Deploy | you | yes — the approval block below |
| Post-deploy smoke checks | you, or `qa-lead` for an independent pass | — |
| Rollback, restore, data repair | you / `devops-lead` | yes — even mid-incident |

`devops-lead` is delegated to when infrastructure must be created or changed (`setup` with nothing in place, a broken pipeline, an incident); the release itself runs here. Rules for infrastructure files: `.claude/rules/devops.md`. Background and CI hardening: `.claude/references/deployment-standards.md`.

**One deployment target.** No staging/production promotion, no environment selector. A dev compose file is not a target.

---

## `/ship setup` — once per project

1. Inspect what exists (Dockerfile, compose, CI, host, scripts) and report it. Change nothing yet.
2. Ask the user — never choose for them — where it deploys and in which mode:

   | | **local** | **CI** |
   |---|---|---|
   | Who deploys | the user's machine runs the deploy command | the pipeline, on push or trigger |
   | `/ship` does | runs the recorded command | commit → push → watch the run → environment gate |
   | Credentials | on the user's machine | CI secrets or OIDC |

3. Agree how migrations are applied (a reviewed SQL script, `dotnet ef database update` with a connection to the target, or on startup for a single instance — `database.md` §6) and how the database is backed up and restored.
4. Missing infrastructure for that target and mode (no Dockerfile, no workflow, no host setup) → build it now with `devops-lead` under `devops.md`, verified as far as possible without touching the target.
5. Propose this `CLAUDE.md` → Deployment block as a diff, and write it after approval — names and commands only, never secret values:

   | Field | Example |
   |---|---|
   | Mode | local · CI |
   | Target | VPS `app-01` via SSH · container host · hosting service |
   | Deploy command | `IMAGE_TAG=<sha> docker compose -f compose.prod.yml up -d` · workflow `deploy.yml` on push to `main` |
   | Migration step | `dotnet ef migrations script --idempotent` → applied with … · on startup |
   | Backup / restore | `pg_dump …` / `pg_restore …` · provider snapshot — and where backups are kept |
   | Health check | `curl -fsS https://<host>/health` → 200 |
   | Rollback command | `IMAGE_TAG=<previous> docker compose -f compose.prod.yml up -d` (first release: stop + restore backup) |
   | Smoke checks | 2–3 concrete: home loads · login works · one read API returns data |

**Setup is unfinished without a rollback command.** Compose has no built-in rollback: deploy immutable tags (commit SHA or date) and keep the previous one reachable.

---

## Step 1 — Check (`/ship check` stops here)

Run things; never repeat a previous claim as a result. Checks run against the repository and the recorded commands. Anything that needs access to the target (its configuration, its current schema version) is checked only with the user's approval for that access; otherwise it is `NOT VERIFIED`.

- **Target** — read from `CLAUDE.md`. Missing → stop: "run `/ship setup`". Never infer a target from a development compose file (`Development` environment, `host.docker.internal`, user-profile secret mounts, hardcoded dev passwords).
- **Git** — the release must be a committed revision: uncommitted changes are a blocker (commit them with approval, or stash). `git log <last-release-tag>..HEAD` for the contents; no tag yet → first release. No repository → say the release cannot be identified or diffed, and continue only if the user accepts.
- **Build** — the real build; errors and warning count. **Vulnerability warnings** (`NU1901`–`NU1904`, `npm audit`): a high or critical advisory is a release finding.
- **Tests** — run them. No test suite is a finding, not a pass.
- **Review** — a `Review:` line in `docs/features/` or `docs/ROADMAP.md` for this exact commit is reused; otherwise list the changes on the risk list that were not reviewed. Open Must-fix or QA Critical/Major findings block.
- **Migrations** — pending ones, their safety class (`database.md` §5), whether each is reversible and compatible with the version currently running.
- **Configuration** — settings and secrets the release needs (from `.env.example` and handoffs), by **name and purpose** only; presence on the target verified only with access approval.
- **CI** — status, if a pipeline exists and is reachable.

Result: `Ready to Ship`, or `Blocked` with each blocker named.

## Step 2 — Build and package

Use the project's real tooling (publish, frontend build, image build). Record the exact revision and the artifact name, tag and size you actually saw. Confirm the rollback command exists. Building locally needs no approval; committing, tagging, pushing or publishing does.

## Step 3 — Approval: stop and wait

```
Deploy:    <revision / artifact tag>
To:        <target>
Changes:   <what users will see differ>
Migration: <none | names, safety class, applied how, backup taken?>
Config:    <new settings/secrets required on the target>
Risks:     <what could go wrong>
Rollback:  <command>
```

An explicit yes authorizes exactly the actions listed in this block — the backup, the migration step and the deploy shown — for this release only. Commit, push and tag are asked separately in Step 2 and Step 5.

## Step 4 — Deploy

Run only what was approved, with the recorded mechanism — never improvise a new one mid-release.

Order: backup (if any migration is risky or destructive) → migration → application → verification. A destructive migration is not part of a normal release: it ships separately after expand/contract (`database.md` §5).

- **local** — run the recorded deploy command.
- **CI** — commit and push (each approved), open or merge the PR if that is the flow, wait for the pipeline, pass the platform's environment gate where it exists, watch the deploy job. A red pipeline ends the release; never re-run hoping for green, never merge past a failing required check, never force-push a shared branch.

On failure: capture the real error, state whether the app is up, partly up or down and whether data is affected, preserve logs, then stop and propose the next action (rollback command, fix forward). Rollback and data recovery wait for approval.

## Step 5 — Verify and report

Evidence from the target: health check, each recorded smoke check, container/process status, error logs since the deploy. Delegate an independent smoke pass to `qa-lead` when the release is risky.

```
## Ship — <target> — Ready to Ship | Blocked | Shipped | Failed

Revision: <sha / tag>
Check:    Build <result> · Vulns <none | list> · Tests <result> · Review <reused rev | not reviewed> · Config <missing names | ok>
Deploy:   Approved <when, by the user> · Ran <command> → <result>
Verify:   Health <command → result> · Smoke <each → result> · Logs <clean | errors>
Rollback: <command>, <tested? no>
Risks remaining: …
```

`Shipped` only when the deploy **and** its verification succeeded; a partial deploy is `Failed` with details. Offer to tag the release (`git tag release-<yyyymmdd>-<shortsha>`) — tagging and pushing the tag need approval. Shipped is not Done: acceptance is the user's. After release, monitoring, logs and incidents are ordinary requests handled with `devops-lead` under the same approval rules.
