# Workflow

Always loaded. How a request becomes verified, handed-off work. Project facts live in `CLAUDE.md`; code constraints in the other rule files; step-by-step procedures in skills.

## What each part of the kit does

| Part | Answers | Loaded |
|---|---|---|
| `CLAUDE.md` | What is this project — scope, stack, commands, deployment target | every session |
| `rules/workflow.md`, `rules/coding-standards.md` | How work moves; what all code must respect | every session |
| `rules/backend.md` · `frontend.md` · `database.md` · `devops.md` · `testing.md` | What code in that domain must and must not do | automatically, when a matching file is read or edited |
| `skills/<name>/SKILL.md` | What to do, in order, and when it is finished | on a matching request, or `/name` |
| `agents/<name>.md` | Who does delegated work, with which tools, returning which handoff | only when you delegate |
| `references/*.md` | How exactly — recipes, examples, background. Never a constraint | only when a rule or skill points to it |

Each fact lives in one place. **Precedence on conflict:** (1) the user's explicit instruction for this task → (2) correctness and security rules, including the known-defect lists, which no codebase convention overrides → (3) the repository's established style and structure → (4) the kit's house style → (5) references. Say which one you followed when they differ.

Domain rules load when you open a file of that domain. Before writing the first file in a domain, read an existing comparable file there: that loads the rules and shows the local convention in one step.

## 1. Size the work

Judge by risk and reversibility, not file count. State the size in one line, then follow its path.

| Size | Signals | Path |
|---|---|---|
| **S — quick change** | Cause known, one place, low risk: a message, a bound, a config value, a typo | Inline: locate → change → verify → report |
| **Bug** | Something is broken and the cause is not yet proven | `fix-bug` |
| **M — feature** | Expected behaviour can be stated in one sentence without guessing — including a full-stack change such as one new entity with its CRUD API, migration and screen | `implement-feature` |
| **M — unclear** | You would be guessing behaviour, rules or scope | `plan-feature` → user approves the spec → `implement-feature` |
| **L — large** | A new application or a new module boundary; or work that needs more than one independently verifiable slice; or more than one session | Recommend `/team-delivery` and wait — it is user-invoked |
| **Review** | Code already written | `review-change` |
| **Release** | The user wants it deployed | The user runs `/ship`. Never start it yourself |

**Risk raises the bar, not the size.** The risk list — used everywhere in the kit: money · auth or permissions · personal data · concurrency · a migration that rewrites or drops data · a breaking change to a public contract. Any of these → write acceptance criteria before coding, and finish with `review-change`.

**Re-size when the evidence changes.** An S that reaches a second module, or a bug whose fix needs a schema or contract change, is now M — stop and say so.

## 2. Phases and gates

Every size runs the same five phases; an S runs them in a minute. Leave a phase only when its gate holds.

| Phase | Gate to leave it |
|---|---|
| **1 Understand** | Goal restated as an observable outcome. Each unknown labelled QUESTION (blocks the work) or ASSUMPTION (stated, cheap to correct). An existing spec in `docs/features/` has been read. |
| **2 Plan** | Files to touch known — S: in your head; M: a short file list in the reply, one line per file; L: `docs/ROADMAP.md`. M and up: acceptance criteria. Frontend + backend: the API contract written down before either side starts. |
| **3 Build** | Compiles. Every changed line traces to the request. No new dependency, pattern, setting or migration without saying why. |
| **4 Verify** | Verification block (below) filled from commands run after the last edit. Each acceptance criterion mapped to evidence. |
| **5 Hand off** | Answer first: what now works. Then files changed, verification, what was not checked, risks. Status set. |

**Get the user's approval before:** building from a spec that `plan-feature` wrote · a plan that touches more than ~5 files or crosses a module boundary · a new dependency, framework upgrade or architectural pattern · anything in *Approvals*.

### When a gate fails

| Situation | Do |
|---|---|
| Your change breaks the build or a test | Read the whole output, fix the cause, re-run. Same failure after **2** attempts → stop patching; reproduce and gather evidence as in `fix-bug` steps 2–3. |
| **3** fixes have failed on one problem | Stop. Report what you tried and what the evidence shows; question the approach with the user. |
| A failure that was there before you started | Do not fix it silently. Report it as pre-existing, with output, and verify your change around it. |
| A check cannot run here (no database, no test project, no access) | Mark it `BLOCKED` or `NOT VERIFIED` with the reason; run everything that can run. |
| Review returns Must fix | Back to Build for those items only; re-run the affected checks; re-review only what changed. Still failing after two rounds → the user decides. |
| A requirement turns out ambiguous mid-build | Finish what does not depend on it, then ask one specific question. |
| A deployment or post-deploy check fails | `ship` handles it: stop, diagnose from real output; rollback or data recovery only with approval. |

## 3. Approvals — never implied

Ask and wait before: deploying or publishing · committing (unless the user asked for commits), `git push`, tag, merge, release · applying a migration to any database, including a local one (ask once per task, in the plan; generating and reading migration files needs no approval) · destructive operations on data or files (drop, truncate, delete, `reset --hard`, removing a volume) · changing CI, infrastructure or secrets beyond what the task asked · provisioning paid resources · installing a new dependency.

Approval covers one action and does not carry over. Finishing a feature, passing review or editing this kit never authorizes a deployment. `.claude/settings.json` enforces part of this list; the rest is on you.

## 4. Verification — evidence or it did not happen

Climb as high as the environment allows: compiles → lint / typecheck → existing tests → new tests → behaviour exercised (request sent, page rendered, job run, container healthy).

- Report only what you ran in this session after your last edit. "Should pass" is not a result.
- Never delete, skip or weaken a test, a validation or a security check to get green.
- A subagent's "done" is a claim: read its diff and re-run the key check before you report it.
- Migrations, deployments and shared environments are never part of "verification" unless the user asked.

```
Verification
  Build:   PASS | FAIL | BLOCKED          <command> → <result>
  Tests:   PASS | FAIL | NOT VERIFIED     <command> → <passed / failed / skipped>
  Checked: <behaviour exercised, and how> | BLOCKED <reason>
  Not covered: <what you did not check>
```

**Status:** `Planned → In Progress → Built` (written) `→ Ready for Review` (verified) `→ Done` (accepted by the user — only the user moves work to Done). `ship` adds `Ready to Ship · Blocked · Shipped · Failed`. Shipped is not Done; generated is not Done.

## 5. Delegation

You are the orchestrator. Work inline unless delegating buys something: a separate context for a large read, independent parallel work, or an independent check.

| Agent | Delegate when | Edits code |
|---|---|---|
| `product-manager` | Scope, MVP or acceptance criteria are open-ended | no |
| `architect` | A new boundary, dependency, data-model or contract decision; a design review | no |
| `backend-lead` | Substantial server work: API, services, data access, migration contents, backend tests | yes |
| `frontend-lead` | Substantial client work: screens, components, UI states, accessibility, frontend tests | yes |
| `devops-lead` | Docker, CI, environment and secret configuration, deploy procedure, operations | infra files only |
| `qa-lead` | Independent verification of risky or user-facing work | no — runs tests |

A brief carries: objective · acceptance criteria or spec path · the contract · files in scope · how it will be verified. Never two agents on the same files. Agents cannot spawn agents (their tool lists exclude it). Escalate product decisions, hard-to-reverse architecture, and any finding that survived two fix rounds.

## 6. Continuity across sessions

Durable project decisions → `CLAUDE.md` Project Context, by proposing a diff — never a silent edit. Requirements → `docs/features/<feature>.md`. Multi-slice progress → `docs/ROADMAP.md`. When these exist, read them at the start and continue from the first item that is not Done; never re-plan what is settled.
