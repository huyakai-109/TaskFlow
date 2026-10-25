---
name: team-delivery
description: Coordinate a large feature or a new project across product, architecture, implementation and QA, in verified vertical slices over several sessions. User-invoked.
disable-model-invocation: true
argument-hint: "[product idea, or large feature]"
---

# Team Delivery

Work: $ARGUMENTS

You are the orchestrator for L-size work (`workflow.md` §1): a new application, a new module boundary, or work that needs several independently verifiable slices or several sessions. A single full-stack feature is M — `implement-feature`. This skill decides which stages run, delegates where it helps, and keeps the work resumable. It reuses the procedures of `plan-feature` (spec), `implement-feature` (build) and `review-change` (review) inside its stages — do not invoke them as a preamble. It never deploys: release is the user's `/ship`.

## 0. Resume or start

1. Look for `docs/ROADMAP.md`, `docs/features/*.md`, ADRs and a filled-in `CLAUDE.md`. If they exist, report what is Done and continue from the first unit that is not — **never re-plan settled decisions**; if one looks wrong, say so and ask.
2. Check the size. S or M → say so, recommend the matching skill (`workflow.md` §1), and ask whether to continue here anyway.
3. Classify the track by listing the repository: empty or README-only → **greenfield**; build files and source → **existing** (even when the user says "new project" — it may be a module).
4. State which stages you will run and which you skip, and why.

## 1. Stages

| # | Stage | Owner | Skip when | Gate to continue |
|---|---|---|---|---|
| 1 | Product scope | `product-manager` | requirements already clear, or a spec exists | MVP scope and out-of-scope written; critical questions answered by the user |
| 2 | Spec | you (plan-feature format) | spec exists | acceptance criteria testable by someone who did not write the code; **user approved** |
| 3 | Technical design | `architect` | the work follows an existing pattern with no new contract | boundaries, contracts (API, data model, error codes) and risks written down; expensive-to-reverse decisions approved by the user |
| 4 | Bootstrap | leads + `devops-lead` | existing project | checklist below complete; the skeleton builds and starts, or `BLOCKED` stated |
| 5 | Slice plan | you | one slice only | `docs/ROADMAP.md` lists slices with owner and status; riskiest assumption first |
| 6 | Build slice | `backend-lead` / `frontend-lead` / inline | — | `implement-feature` steps 4–6 met for the slice |
| 7 | QA | `qa-lead` | low-risk internal slice (say so) | acceptance criteria MET with evidence; Must-fix findings fixed or accepted by the user |
| 8 | Acceptance | you + user | — | status reported; user decides Done |

Stages 1–5 run once; 6–8 repeat per slice. Write the spec to `docs/features/` and decisions to `docs/adr/NNNN-<title>.md` yourself — `product-manager` and `architect` cannot edit files.

## 2. Greenfield bootstrap checklist

The goal is a running skeleton with one real path through it, not a feature set.

- [ ] Stack approved by the user (2–3 options with a recommendation for language/framework, database, auth — nothing else needs a meeting). .NET default: `net10.0`.
- [ ] Solution/project structure matching the architecture; dependency direction enforced by project references.
- [ ] Kit conventions adapted to the stack (`backend.md` §0: keep house style and engineering rules, map project mechanisms to the new codebase); `CLAUDE.md` Project Context filled in from the decisions, including real commands.
- [ ] Composition root, configuration keys, `.env.example`; no secrets committed.
- [ ] Database wired (local service by `devops-lead`, schema by the backend lead); first migration additive.
- [ ] Authentication boundary, if the product has users.
- [ ] Test project in place; first slice's business rules tested.
- [ ] Frontend (if any): stack approved, conventions and tokens recorded in `docs/ui.md`.
- [ ] Dockerfile/compose when they make local setup or deployment easier; one CI workflow (build + test) when a remote repository exists — `devops-lead`, in parallel with the first slice, owning only infrastructure files.
- [ ] Deployment approach written down — deciding is not deploying; `/ship setup` records it later.
- [ ] One end-to-end slice implemented and verified.

Never in bootstrap: microservices, brokers, Kubernetes/Terraform/Helm, a second datastore, paid resources, any deployment, any destructive database operation, dependencies the user has not agreed to.

## 3. Slices

A slice is one thin end-to-end path — entity → migration → service → endpoint → screen → tests — that can be run and verified alone. Never build horizontally (all entities, then all services). Order slices so each is independently verifiable and the riskiest assumption is tested earliest.

```markdown
# <Product> — Roadmap
| # | Slice | Owner | Status | Spec |
|---|---|---|---|---|
| 1 | Auth: register + login | backend-lead | Done | docs/features/auth.md |
| 2 | Product CRUD API | backend-lead | In Progress | docs/features/product.md |
| 3 | Product list screen | frontend-lead | Planned | docs/features/product.md |
## Decisions
- <date> <decision> (ADR-n)
```

Update statuses in place; the roadmap is not a journal.

## 4. Delegation

- Brief every agent with: objective · spec path and acceptance criteria · the contract · files in scope · verification expected (`workflow.md` §5). The brief is all the context it gets.
- Backend and frontend run in parallel only after the contract is written, with no shared files. `devops-lead` owns only infrastructure files.
- Treat each handoff as a claim: read the diff, re-run the key check (`workflow.md` §4).
- QA findings return to the owning lead → fix → re-verify → QA re-checks only what changed. A finding that survives two rounds goes to the user.
- Use three or four agents for most L work; never all six by habit.

## 5. Close each slice

```
## <Slice> — Built | Ready for Review

Delivered: <user-visible behaviour>
Stages run / skipped: <with reasons>
Files: <grouped by domain>
Contract: <endpoints; breaking changes>
Verification: <workflow.md §4 block; QA verdict if run>
Open items: <findings, deferred scope, limitations>
Acceptance: <what the user must confirm for Done>
Next: <next slice; ROADMAP updated>
```

Greenfield: state whether the application builds and starts, or that this could not be verified here. When a release is warranted, tell the user to run `/ship check` — and stop.
