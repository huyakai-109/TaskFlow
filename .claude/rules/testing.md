---
paths:
  - "**/*Test*/**"
  - "**/*Tests/**"
  - "**/*.test.*"
  - "**/*.spec.*"
  - "**/test/**"
  - "**/tests/**"
  - "**/__tests__/**"
---

# Testing Rules

Writing and running tests. Verification reporting and the evidence rules are in `workflow.md` §4 and apply always; this file is about the tests themselves.

## 1. Use the project's setup

- **[M]** Find the real setup first: test projects, framework (xUnit / NUnit / MSTest / Vitest / Jest / Playwright …), naming, folders, fixtures, mocking library, how the database is handled, and the command. Follow it.
- **[M]** An existing project with no tests → say so and ask whether tests are in scope. Do not add a framework, project or pipeline unprompted.
- **[M]** Greenfield is the exception: the test project is part of bootstrap, and business rules with branches are tested from the first slice.

## 2. What deserves a test — by risk, not coverage

- **[R]** Test: business rules with branches; each distinct error code; state transitions (one per allowed and per forbidden move); calculations with money, rounding or dates; permission-dependent behaviour; every bug fix.
- **[R]** Skip: DTOs, mappers without logic, framework behaviour, thin pass-throughs.
- **[R]** Known misses in error-code services: update not enforcing a create rule; uniqueness on update not excluding the current row; delete without dependency check; missing cache invalidation after a write.

## 3. How to write one

- **[M]** Test behaviour through the public seam (service method, HTTP endpoint, rendered component), not private methods or internals.
- **[M]** One behaviour per test; the name states it: `Update_ReturnsCodeAlreadyExisted_WhenAnotherProductHasCode`.
- **[M]** Arrange / Act / Assert, visibly separated. Assert the exact outcome — the error code, the value, the rendered text — not merely "not null" or "did not throw".
- **[M]** Expected values come from the spec or a worked example, never recomputed the way the code computes them.
- **[M]** Deterministic and independent: no wall clock (inject `TimeProvider` / a fake clock), no locale, network, ordering or shared state between tests.
- **[M]** A bug-fix test must fail without the fix. Confirm it once (run before applying the fix, or revert and re-run).
- **[R]** Mock only what crosses a process boundary (HTTP, mail, storage, queue). Prefer asserting results over asserting call counts.
- **[R]** Data access: EF Core's InMemory provider is not a relational database — it ignores constraints, transactions and SQL translation. For queries, uniqueness and concurrency use the project's real-database setup (SQLite in-memory, a test container, a disposable local DB); say which.

## 4. Running

- **[M]** Narrowest first (the affected test class or file), then widen to the project, then the full suite when quick. Read failures in full.
- **[M]** Use the real commands from `CLAUDE.md` → Development Commands. Typical: `dotnet test --filter "FullyQualifiedName~ProductService"`, `npx vitest run <file>`.
- **[M]** Never skip, delete, `[Ignore]` or loosen an assertion to make a run pass. A failing test is either fixed by fixing the code, or reported.
