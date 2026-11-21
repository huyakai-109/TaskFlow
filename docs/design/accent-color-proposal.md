# Accent colour — light theme proposal (brainstorm only, not implemented)

Status: **Proposal, awaiting the product owner's choice.** No token, component or
test file has been changed for this. Written in response to feedback that the
light-theme accent (purple/indigo) on primary buttons reads too saturated/heavy.

## 1. Where the accent colour is used today (light theme)

Single source of truth: `FE/src/styles/tokens.css` `:root` block, five tokens —
`--color-accent` `#4f4fcc`, `--color-accent-hover` `#4343b3`, `--color-accent-soft`
`#ebebfa`, `--color-accent-ink` `#3a3aa6`, `--color-on-accent` `#ffffff`, plus
`--color-focus` (currently aliased to the same value as `--color-accent`,
`#4f4fcc`). Mapped into Tailwind by `global.css`'s `@theme inline` block as
`bg-accent`, `bg-accent-hover`, `bg-accent-soft`, `text-accent-ink`, `text-on-accent`,
`border-accent`, `outline-focus` — components never use a literal hex.

Audited every call site (`grep` counts: 42× `bg-accent`, 22× `bg-accent-soft`, 26×
`text-accent-ink`, 10× `border-accent`, 4× explicit `outline-focus` + the one
app-wide `:focus-visible` rule in `global.css` that every focusable element gets
for free). Grouped by role:

| Role | Where | Token(s) |
|---|---|---|
| **Primary button** | Login "Sign in", New task "Create task", Edit/Save forms (Profile display name, password, avatar Upload), "Change password and sign in", "Reopen", "Create account", Board/Task-detail "Take it and start"/"Assign to … and start" confirm, `WorkflowStrip`'s primary move ("Submit for review" etc.), `CardMoveAction`'s single-legal-move button, Accounts "New account" | `bg-accent` + `text-on-accent`, `hover:bg-accent-hover` |
| **Secondary/ghost links & text buttons** | "Mark all as read", "Show more" hover state, breadcrumb-style "Open Team capacity →" / "see the full … column" / "see all on Board", "Clear filters" (Activity), underlined inline links in dialogs | `text-accent-ink`, usually with `underline` |
| **Focus ring** | Every focusable element, app-wide — one rule in `global.css` (`:focus-visible { outline: 2px solid var(--color-focus) }`), plus a few components that also set `focus:outline-focus` explicitly on top of it (redundant but harmless) | `--color-focus` (aliases `--color-accent`'s value, not the same variable) |
| **Selected state** | Sidebar active `NavLink` (`bg-accent-soft` + `text-accent-ink`), `SortMenu`'s current option, `AssigneePickerDialog`/options-list selected row, New task modal's selected Priority card (`border-accent` + `bg-accent-soft`) | `bg-accent-soft` + `text-accent-ink`, `border-accent` |
| **Chips / "selected" pills** | Same `accent-soft` background family as above — this app has no separate "chip" visual language for the accent colour; overdue/urgent/warning chips use `danger`/`warning`/`priority-*` tokens instead, not accent | `bg-accent-soft` |
| **Status bar / badges** | **None** — status (overdue, capacity-full, priority) deliberately uses `danger`/`warning`/`success`/`priority-*` tokens, never accent, so the badge/status vocabulary is unaffected by anything proposed below. The one small accent-coloured mark is the notification bell's unread dot (`bg-accent`, 8px) | `bg-accent` (decorative dot only) |

**Implication for implementation (after a choice is made, not done now):** every one
of the above reaches the colour through five tokens plus `--color-focus`. Changing
light-theme feel is a **six-line edit in one file** (`tokens.css`'s `:root` block) —
no component touches a literal colour, confirmed by the audit above. Dark theme is
a fully separate set of values (two more blocks in the same file) and is untouched
by this proposal.

## 2. Three options

All three keep the same *role* split (`accent` / `accent-hover` / `accent-soft` /
`accent-ink` / `on-accent`) and change only the actual colour. Contrast ratios
below are **computed** (WCAG relative-luminance formula, not estimated) against
this app's real backgrounds: white `#ffffff` (dialogs/cards/inputs), the page
background `#fbfbfc`, and each option's own `accent-soft` tint. The two numbers
that matter for AA: **4.5:1** for normal text (white button label on `accent`;
`accent-ink` link text on white) and **3:1** for large text / UI components
(focus ring and border against the background they sit on).

### Option 1 — "Desaturate the current purple" (keeps brand identity)

| Token | Value |
|---|---|
| `--color-accent` | `#6366a1` |
| `--color-accent-hover` | `#51547f` |
| `--color-accent-soft` | `#eceefb` |
| `--color-accent-ink` | `#474a84` |
| `--color-on-accent` | `#ffffff` |
| `--color-focus` | `#6366a1` (same as accent) |

Contrast: white text on `accent` = **5.32:1** (AA pass, normal text) · `accent-ink`
on white = **8.14:1** (AAA) · `accent-ink` on `accent-soft` = **7.05:1** (AAA) ·
`accent` on page bg `#fbfbfc` (focus ring / border) = **5.15:1** (comfortably past
the 3:1 UI-component floor).

**Feel:** same indigo hue as today, just pulled toward grey — "muted indigo",
still readable as "this is TaskFlow's colour", noticeably calmer than `#4f4fcc`.

### Option 2 — Neutral blue-gray (furthest from the current purple)

| Token | Value |
|---|---|
| `--color-accent` | `#55617a` |
| `--color-accent-hover` | `#434c61` |
| `--color-accent-soft` | `#eceef1` |
| `--color-accent-ink` | `#3d4756` |
| `--color-on-accent` | `#ffffff` |
| `--color-focus` | `#55617a` |

Contrast: white on `accent` = **6.22:1** · `accent-ink` on white = **9.40:1** ·
`accent-ink` on `accent-soft` = **8.09:1** · `accent` on page bg = **6.01:1**.

**Feel:** "neutral blue-gray" — barely reads as a hue at all next to the purple
it replaces; closest to a "corporate SaaS grey-blue" look, lowest-risk against
clashing with anything else in the palette (danger/warning/success/priority are
all still clearly distinct from it).

### Option 3 — Soft teal (a genuinely different hue)

| Token | Value |
|---|---|
| `--color-accent` | `#2f7f74` |
| `--color-accent-hover` | `#256961` |
| `--color-accent-soft` | `#e3f4f1` |
| `--color-accent-ink` | `#1f5d55` |
| `--color-on-accent` | `#ffffff` |
| `--color-focus` | `#2f7f74` |

Contrast: white on `accent` = **4.76:1** (AA pass, closest margin of the three —
still real headroom above the 4.5 floor) · `accent-ink` on white = **7.62:1** ·
`accent-ink` on `accent-soft` = **6.71:1** · `accent` on page bg = **4.61:1**.

**Feel:** "soft teal" — a clear identity change, not a desaturation of the same
hue; reads calmer and more "organic", most different from every other status
colour in the palette (useful if the goal is to stop the accent reading as
"another shade of the danger/priority reds and oranges" at a glance — it
currently doesn't clash, but teal makes the separation unmistakable).

### Summary table

| | Option 1 (desaturated purple) | Option 2 (neutral blue-gray) | Option 3 (soft teal) |
|---|---|---|---|
| Keeps brand hue | Yes | No | No |
| White-on-accent contrast | 5.32:1 | 6.22:1 | 4.76:1 |
| Link-text contrast | 8.14:1 | 9.40:1 | 7.62:1 |
| AA (4.5:1 / 3:1)? | Pass | Pass | Pass |
| Risk | Lowest (smallest visual jump) | Low (very safe, a bit "beige-corporate") | Medium (biggest identity change) |

## 3. Visual preview

**No screenshot tool is available in this environment** — I did not render or view
any of these in a browser, and the paragraph below is not a substitute for that.
Instead: a static, standalone HTML file, **`docs/design/accent-color-preview.html`**,
assembles the real token values above into small, labelled replicas of the
components named in the audit (primary button, link, focus ring on a text input,
a selected sidebar-style nav item, a selected priority card, the unread dot) for
each option side by side, plus the current colour as a reference column, in both
light and a simple dark block (confirming the dark-theme tokens are untouched —
dark is not part of this proposal). It is a plain, dependency-free HTML+CSS file;
open it directly in a browser to compare the three options against the real
computed colours above. It is **not** a screenshot and does not substitute for
opening the real app.

## 4. Decision needed

Pick **Option 1**, **Option 2**, or **Option 3** (or ask for a fourth). Once chosen:
implementation is the six tokens in `tokens.css`'s `:root` block only (one place,
per the brief) — dark theme's own block is untouched unless told otherwise — and
the decision gets recorded in `docs/design/SPEC.md` §10 as a documented deviation
from the original wireframe's accent colour, same pattern every other §10 entry
already follows.
