---
name: review-change
description: Use when asked to review, check, audit or sanity-check a diff, branch, pull request or recently written code, and after implementing risky work (money, permissions, personal data, concurrency, data-changing migration, public contract) or work spanning several domains. Also when deciding how to act on review feedback.
argument-hint: "[what to review — diff, branch, files, or feature]"
---

# Review Change

Review: $ARGUMENTS

Find real defects, rank them by consequence, and say which must be fixed. Re-run checks rather than trusting claims.

## 1. Establish the change and its intent

- Get the actual change: `git diff`, `git diff <base>...HEAD`, the PR, or the named files when there is no version control. Read whole changed files, not only changed lines.
- Find what it was supposed to do: the spec in `docs/features/`, acceptance criteria, or the request. Missing → you can check rules but not correctness; say so.
- Note what the author claims was verified.

## 2. Re-verify

Build, run the affected tests, and exercise the main path where cheap. A claimed pass you cannot reproduce is a finding. Record commands and results.

## 3. Review, highest consequence first

Open each changed file: its domain rules (`backend.md`, `frontend.md`, `database.md`, `devops.md`, `testing.md`) load with it and are the checklist for that domain. Compare against the two nearest comparable files too — the repository's convention wins over the rule, but report the divergence.

Across every domain, check:

1. **Correctness** — walk the main path with concrete values, then boundaries (null, empty, whitespace, zero, negative, max length, one over, empty collection) and every error path. Each declared failure reachable? Any failure falling through to a generic 500?
2. **Symmetry and lifecycle** — every create rule present on update, uniqueness excluding the current row; dependents checked before delete; illegal or repeated state transitions refused.
3. **Regression** — search the callers of everything changed (Graphify's `graphify query` / `path` when the project has `graphify-out/`). Changed response field, enum value, error code or envelope → breaking change: declared?
4. **Security** — authorization on every new endpoint at the right sensitivity, permission registered, row filtering on list endpoints, input bounded, SQL parameterised, output escaped, no secret in code/config/image/logs, no security setting weakened.
5. **Data integrity and concurrency** — partial write possible? Two simultaneous callers → duplicate, lost update, double approval? Invariant in the database or only in code? Migration safety class correct and compatible with the running version? Cache invalidated on every write path?
6. **Performance** — N+1, unbounded result sets, non-sargable filters, missing index for a new hot filter, query inside a loop, synchronous work that should be async or a job.
7. **Tests** — do they hit the branches that matter and each new error code? Does a bug fix have a test that fails without it? Any test weakened or skipped?
8. **Complexity** — abstraction with one implementation, forwarding-only layer, unused option, speculative generality, dead or commented-out code, leftover debug output.
9. **Project context** — did the change make `CLAUDE.md` or a rule wrong (command, boundary, convention)? Did a finding arise because a project-wide rule was never written? Recommend the rule change.

Stop widening the search when you stop finding things that matter.

## 4. Classify

| Class | Meaning |
|---|---|
| **Must fix** | Wrong behaviour, data or security risk, broken or undeclared contract change, boundary violation, regression, unverifiable claim on a risky path |
| **Recommendation** | A real improvement that does not block |
| **Pre-existing / out of scope** | Real, but not introduced by this change. Never blocks |

Every finding carries `file:line`, a concrete failing scenario, and the fix. "Error handling could be better" is not a finding. An empty Must-fix list is a valid result; do not pad it with style preferences, and do not soften a critical one.

## 5. Report

```
## Review — <change> — APPROVE | APPROVE WITH RECOMMENDATIONS | CHANGES REQUIRED

Re-verified: <commands → results, on which revision>

### Must fix
1. <title> — <file:line>
   Scenario: <input/state → wrong result>
   Fix:      <specific change>

### Recommendations
- <title> — <file:line> — <why>

### Pre-existing / out of scope
- <title> — <file:line>

Not reviewed: <what and why>
```

Record the result so `ship` can reuse it: add `Review: <verdict> on <commit sha>, <date>` to the feature's `docs/features/<x>.md` (or its `docs/ROADMAP.md` row). Reuse is valid only for that exact committed revision with a clean working tree.

## Acting on findings (yours or anyone's)

Before changing code for a finding, check it against this codebase: is it correct here, does it break something, is there a reason for the current code? Push back with evidence when it is wrong; ask when it is unclear instead of half-implementing. Fix one item at a time, re-run the affected checks, then re-review only what changed. A finding still open after two rounds goes to the user.

For an independent pass on L-size or high-risk work, delegate to `qa-lead` with the diff, the acceptance criteria and the author's verification claims.
