---
name: implement-feature
description: Use when adding or changing behaviour whose expected result is clear — a new or changed endpoint, screen, field, business rule, background job, integration or migration, or a refactor across several files — including implementing an approved spec in docs/features/. Not for bugs with an unknown cause (fix-bug), unclear requirements (plan-feature), or a one-place quick change.
argument-hint: "[feature, or path to docs/features/<feature>.md]"
---

# Implement Feature

Request: $ARGUMENTS

Build the change the way the rest of this codebase is built, across every domain it touches, and prove it works.

## 1. Understand

- If a spec exists (`docs/features/<feature>.md`, or the path given), read it and treat it as the requirements. Do not re-plan it. Where the user's message contradicts it, the message wins — say so.
- Restate the outcome in one sentence. List QUESTIONS (block) and ASSUMPTIONS (state and continue).
- Whole shape unclear → stop and switch to `plan-feature`. A couple of gaps → continue with everything that does not depend on them and ask specifically.
- L-size (a new application or module boundary, more than one independently verifiable slice, or more than one session — `workflow.md` §1) → recommend `/team-delivery` and wait. One entity with its API, migration and screen is M: stay here.
- No acceptance criteria and the change is on the risk list (`workflow.md` §1) → write 2–6 Given/When/Then criteria first and show them.

Done when you can state the outcome and how it will be checked.

## 2. Survey the code

For each domain the change touches (backend, database, frontend, infrastructure), read two comparable implementations and trace one end to end — route → service → query → entity, or page → API client → component. Note naming, signatures, error handling, validation, response shape and test style. Opening these files loads that domain's rules; follow them, and follow the code where it differs from them (say so).

Optional: if the project uses Graphify (a code knowledge-graph CLI; `graphify-out/graph.json` exists), `graphify query "<question>"` and `graphify path A B` find callers and neighbours faster than grepping.

Done when you can name the files you will create or change and the existing file each one is modelled on.

## 3. Plan

List the files per domain, in dependency order, one line each. Then mark:

- **Contract** — for frontend + backend work: route, verb, request and response fields, error codes, permission. Write it before either side is built.
- **Database** — the migration's safety class (`database.md` §5): additive / risky / destructive.
- **Operations** — a new setting, secret, external service, scheduled job or container change → a deployment requirement in the report; infrastructure files go to `devops-lead` or follow `devops.md`.
- **Verification needs** — will checking the endpoint or screen need the migration applied to the local database, a running app, or a test login? Ask for all of it now, in one question, not mid-task.
- **Approval** — anything in `workflow.md` §2–§3 (more than ~5 files, a boundary, a dependency, applying a migration) → show the plan and wait.

Delegation is optional: work inline by default. Use `backend-lead` / `frontend-lead` in parallel only when the contract is written and their files do not overlap; give each the brief from `workflow.md` §5.

## 4. Build in a vertical slice

Order: entity + configuration + migration → persistence/query → service rules → controller/endpoint → API client and types → screen and states. Keep it compiling as you go; build after each layer.

Recipes for common .NET work are in `.claude/references/dotnet-patterns.md`: paged list (1), create (2), update (3), delete (4), entity (5), permission (6), DI (7), transaction (8), cache (9), background job (10), file upload/export (11), configuration (12), status transitions (13), cross-row limits / concurrency (14), append-only history (15). Read only the recipe you need.

Stay inside the request: no adjacent refactoring, no speculative options. A real defect found outside scope goes in the report.

## 5. Test

Per `testing.md`: in a project with tests, cover each new business rule branch, each new error code, calculations, and the create/update symmetry. In an existing project without tests, say so and ask. Frontend: the project's component or e2e tests where they exist.

## 6. Verify

Run what applies, after the last edit:

| Domain | Evidence |
|---|---|
| Backend | Build; affected tests; the endpoint exercised (test, `.http` / `curl` against the running app) including one failure path |
| Database | Migration generated and its SQL read; `has-pending-model-changes` clean; applied to the local database only if approved in the plan |
| Frontend | Typecheck, lint, build, tests; the screen rendered in each state at the three widths (`frontend.md` §7, §11) |
| Infrastructure | `devops.md` §7 |

Then read your own diff top to bottom: every line traces to the request; create and update rules match; authorization on every new endpoint and permission registered; UI states present; no debug output or orphaned code.

## 7. Review and report

Run `review-change` when the change is on the risk list or spans several domains; otherwise the self-review above suffices — say which.

```
## <Feature> — Built | Ready for Review

What works: <behaviour, in user terms>
Files: <path — reason>, grouped by domain
Contract: <endpoints added/changed; breaking changes stated>
Database: <migration name, safety class, applied? (normally: not applied)>
Deployment requirements: <new settings/secrets/services, or none>
Decisions and deviations: <judgement calls; where code was followed over a rule>

Verification
  Build / Tests / Checked / Not covered — per workflow.md §4
```

Ready for Review needs the verification; otherwise Built. Update the row in `docs/ROADMAP.md` if one exists. If a durable project decision changed (stack, command, boundary, convention), propose a `CLAUDE.md` diff. Recommend `/ship` only if the user wants a release — never start it.
