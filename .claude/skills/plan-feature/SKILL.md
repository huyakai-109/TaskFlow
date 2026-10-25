---
name: plan-feature
description: Use when a request is an idea or is too ambiguous to implement without guessing — "I want to add…", "how should we build…", "plan / spec out…", unclear business rules, unknown scope, or several reasonable behaviours. Produces a short spec with acceptance criteria for the user to approve. Not when the behaviour is already clear (implement-feature).
argument-hint: "[idea or feature]"
---

# Plan Feature

Idea: $ARGUMENTS

Produce the smallest specification that removes the guessing, get it approved, and stop. This skill does not write code.

## 1. Decide whether to plan

If the behaviour can be stated in one sentence and the risk is low, say "no plan needed" in one line and continue with `implement-feature`. Plan when behaviour, rules or scope would otherwise be guessed.

## 2. Ground it in the product

Restate the goal in one sentence. Search the repository for the feature area — endpoints, entities, enums and statuses, permissions, error codes, screens — and read one comparable feature end to end. State what exists; it answers many questions before you ask them.

If the direction is genuinely open ("what should we do about X"), give 2–4 distinct options with one line of trade-off each and a recommendation. Delegate to `product-manager` only when product framing is the hard part.

## 3. Write the spec

Size it to the work: a small change is 3–6 lines (before, after, acceptance criteria); an ordinary feature fits one page; a large one adds MVP vs later and a slice list, still under two pages.

```markdown
# <Feature>
Status: Planned · Spec owner: user

## Goal
<one sentence, user-visible outcome>

## Scope
In:  …
Out: …   (what someone might assume is included)

## Rules and behaviour
R1 …     (numbered, each testable)

## Acceptance criteria
AC1 Given …, when …, then … (exact value / error code / state)
— cover: happy path, each validation failure, empty/zero, permission denied, effect on existing data

## Contract            (when there is an API or a frontend + backend split)
<verb route · permission · request fields with bounds · response · error codes>

## Data changes        (when the schema changes)
<entities/columns/indexes · safety class: additive | risky | destructive · existing data>

## UI                  (when there is a screen)
<screens, states (loading/empty/error/success/denied), permissions, responsive notes>

## Operations          (only if any: new setting/secret, external service, job, storage, cost)

## Open questions
- QUESTION: …  /  ASSUMPTION: …
```

Before finishing, check the spec against: who may do this and what others see · what happens to existing records · reversibility · duplicates and concurrent edits · audit or notification needs · contracts or exports that change shape. Each gap becomes a QUESTION, not an invented answer.

Consult `architect` when the work adds a module, dependency or integration, changes the data model or a contract, or touches auth, caching, transactions or background work.

## 4. Save and hand over

Write the spec to `docs/features/<kebab-name>.md` (or the project's location) when implementation will happen later, in another session, or in several units. Add a row to `docs/ROADMAP.md` if it exists. A spec the user will act on immediately can stay in the reply.

End with: the path, the open questions that block implementation, and the next command:

```
/implement-feature docs/features/<kebab-name>.md
```

Then stop. The user reviews and corrects the spec before any code is written.
