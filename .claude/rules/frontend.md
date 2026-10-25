---
paths:
  # TaskFlow: the React client lives in FE/ (build output FE/dist is excluded on purpose).
  - "FE/src/**/*.{ts,tsx,css}"
  - "FE/index.html"
  - "FE/package.json"
  - "FE/vite.config.ts"
  - "FE/tsconfig*.json"
---

# Frontend Rules

Client-side code. The kit carries no frontend house style: **every convention comes from the project**. What follows is the bar the code must meet and the order in which to find the project's conventions.

## 1. Discover before writing

**[M]** Before the first edit, establish and state in a few lines (anything not found is `UNVERIFIED`, never guessed):

| Find | Where |
|---|---|
| Framework + version, language strictness, build tool, commands (dev, build, lint, typecheck, test) | `package.json`, lockfile, `tsconfig`, framework config, CI |
| Structure — by feature or by type; where pages, components, hooks/services, types live | directory tree |
| Component, state, data-fetching, form, routing/guard, styling and i18n conventions | two comparable screens, read end to end — ideally one list and one form |
| Design system — component library, tokens (colour, spacing, type), layout shell, any UI doc (`docs/ui.md`, a design-system folder) | styles setup, shared UI folder |

- **[M]** Never assume React, Angular, Vue, Tailwind, a state library or anything else — read it. Never write code in a framework the project does not use.
- **[M]** No client code exists → do not pick a stack. Ask, unless the user approved a stack during `team-delivery` bootstrap; then record its conventions in `docs/ui.md` (tokens, component library, layout shell, breakpoints) so every later screen follows one source.

## 2. Structure and components

- **[M]** Follow the project's folder scheme and component style. One component per file, named like its peers.
- **[M]** Separate data from presentation the way the project does: route/page components own fetching, params and permissions; presentational components take props and emit events.
- **[R]** A component past ~200 lines, or with several unrelated states, is doing two jobs — split along the project's existing lines.
- **[M]** Use the project's shared components (buttons, inputs, tables, dialogs, form fields) before writing new ones. Extract a shared component at the second real use.
- **[M]** No new UI library, state manager, styling system or build step. If one seems necessary, raise it as a decision.

## 3. API contract and data

- **[M]** Call the backend through the project's shared HTTP client; unwrap the envelope and map error codes in its existing shared layer, not per call site.
- **[M]** Implement the agreed contract exactly — route, verb, field names, types, nullability, enum values, error codes, pagination shape. Declare its types once.
- **[M]** The backend deviates from the contract → report the mismatch. Never paper over a server defect client-side.

## 4. UI states and honest data

- **[M]** Every async surface handles loading, empty, error and success — plus permission-denied where it can occur.
- **[M]** Errors show what happened and what to do, mapped from the server's error code where a mapping exists; never one generic message for everything.
- **[R]** Empty-because-no-data and empty-because-filtered get different copy and actions. Zero prints as 0, a missing value as a dash; an empty section keeps its frame.

## 5. Forms

- **[M]** Client validation for feedback, server validation as truth: show server field errors next to their fields.
- **[M]** Disable submit while in flight; prevent double submission; keep the user's input on failure.
- **[M]** Limits shown and enforced in the UI (length, file type, size) equal the server's.
- **[R]** Confirm before discarding unsaved changes and before destructive actions, where the project does so elsewhere.

## 6. Visual consistency and UX

- **[M]** Use the design system's tokens and components. No raw colours, font sizes or spacing values where a token exists; no new visual style on one screen.
- **[M]** The primary action of a screen is obvious and reachable without scrolling on desktop; destructive actions are visually distinct and confirmed.
- **[R]** Work screens (admin, lists, dashboards): colour carries meaning (status, change), not decoration; numbers right-aligned in tables; filters and actions where the project already puts them.
- **[R]** Motion only to show what changed after a user action; respect `prefers-reduced-motion`.

## 7. Responsive

- **[M]** Layout works at 390 px (phone), 768 px (tablet) and 1440 px (desktop) unless the project declares a narrower target — no horizontal page scroll, no clipped controls.
- **[M]** Touch targets at least 44 × 44 px on touch layouts. Wide tables scroll inside their container or collapse the way the project's other tables do.

## 8. Accessibility — WCAG 2.1 AA baseline

- **[M]** Semantic elements (`button` for actions, `a` for navigation); every input has a visible, associated label; icon-only buttons have an accessible name.
- **[M]** Fully keyboard operable, visible focus, no traps; dialogs move focus in and return it on close.
- **[M]** Text contrast ≥ 4.5:1 (3:1 for large text and UI components); state never conveyed by colour alone.
- **[M]** Async errors and status changes are announced (live region); images have meaningful or empty `alt`.
- **[M]** Never lower the project's existing accessibility bar.

## 9. Permissions and security

- **[M]** Hide or disable actions using the same permission identifiers as the backend. This is presentation only — the server check is the boundary.
- **[M]** No unescaped untrusted HTML (`dangerouslySetInnerHTML`, `v-html`, `[innerHTML]`) without sanitising, stated in the report.
- **[M]** No secrets in client code: anything in the bundle is public, including build-time environment variables. No tokens in URLs or logs.

## 10. Performance

- **[R]** Paginate or virtualise long lists; debounce search input; avoid refetching what the cache holds; lazy-load routes where the project does. State the bundle cost of anything added.

## 11. Verification

- **[M]** Run the project's typecheck, lint, build and tests (discovered in §1).
- **[M]** Compiling is not working. Exercise the screen: run the app and load it, or drive it with the project's e2e tool / Playwright, and check each state (loading, empty, error, success) and the three widths in §7. Take and look at screenshots when a browser tool is available.
- **[M]** Report what you rendered and how. Not exercised → `NOT VERIFIED` with the reason.
- **[M]** No test setup in an existing project → say so and ask before adding one.
