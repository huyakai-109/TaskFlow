# Accent colour — light theme proposal

Status: **Decided 2026-11-21 — Option 3 (soft teal) chosen and implemented.**
Light theme's accent tokens (`FE/src/styles/tokens.css` `:root` block) now use
the Option 3 values below; dark theme is unchanged. Recorded as a deviation in
`docs/design/SPEC.md` §10. The rest of this file is kept as the rationale/
options record — history isn't deleted, same convention `SPEC.md` §10 itself
already uses. Written in response to feedback that the light-theme accent
(purple/indigo) on primary buttons read too saturated/heavy.

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

### Option 3 — Soft teal (a genuinely different hue) — **CHOSEN**

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

## 4. Decision

**Option 3 chosen 2026-11-21.** Implemented as the six tokens in `tokens.css`'s
`:root` block only, light theme, nothing else touched. Recorded in
`docs/design/SPEC.md` §10.

## 5. Follow-up audit (2026-11-21, after implementation)

### Real measured contrast (every accent combination in the shipped light theme)

| Pairing | Colours | Ratio | AA needed | Result |
|---|---|---|---|---|
| White button text on `accent` | `#ffffff` / `#2f7f74` | 4.76:1 | 4.5:1 (text) | Pass |
| White button text on `accent-hover` | `#ffffff` / `#256961` | 6.42:1 | 4.5:1 | Pass |
| `accent-ink` link text on white (`--color-surface`) | `#1f5d55` / `#ffffff` | 7.62:1 | 4.5:1 | Pass |
| `accent-ink` on page bg `--color-bg` | `#1f5d55` / `#fbfbfc` | 7.37:1 | 4.5:1 | Pass |
| `accent-ink` on `--color-column` | `#1f5d55` / `#f3f3f6` | 6.88:1 | 4.5:1 | Pass |
| `accent-ink` on `--color-sidebar` | `#1f5d55` / `#f2f2f5` | 6.82:1 | 4.5:1 | Pass |
| `accent-ink` on `--color-side-panel` | `#1f5d55` / `#f5f5f8` | 7.00:1 | 4.5:1 | Pass |
| `accent-ink` on `accent-soft` (selected nav item/row text) | `#1f5d55` / `#e3f4f1` | 6.71:1 | 4.5:1 | Pass |
| Plain `--color-text` on `accent-soft` (selected priority card body text) | `#18181d` / `#e3f4f1` | 15.56:1 | 4.5:1 | Pass |
| Focus ring (`--color-focus`) on white/surface | `#2f7f74` / `#ffffff` | 4.76:1 | 3:1 (UI component) | Pass |
| Focus ring on page bg | `#2f7f74` / `#fbfbfc` | 4.61:1 | 3:1 | Pass |
| Focus ring on `--color-column` (weakest case found) | `#2f7f74` / `#f3f3f6` | 4.30:1 | 3:1 | Pass |
| `accent` border on `accent-soft` (selected priority card border) | `#2f7f74` / `#e3f4f1` | 4.19:1 | 3:1 | Pass |

Every combination that uses the accent colour passes AA, including the weakest
case found (focus ring against `--color-column`, 4.30:1, still comfortably above
the 3:1 floor).

### Teal vs. success-green confusability — audited, no literal colour-alone violation found, but flagged

Measured: `--color-accent` `#2f7f74` is HSL(172°, 46%, 34%); `--color-success`
`#1f7a4d` is HSL(150°, 59%, 30%) — only **22° of hue** and **4 points of
lightness** apart, and the luminance contrast *between the two colours
themselves* is **1.12:1** (i.e. near-indistinguishable brightness if the two
swatches were placed side by side with nothing else to go on).

Audited every place `--color-success` carries business meaning:

| Place | Colour use | Also carries | Colour-alone? |
|---|---|---|---|
| Done column header icon (`BoardPage` `StatusIcon`) | Filled green circle + white check | A **distinct icon shape** per status (dashed circle / circle / half-circle / three-quarter circle / filled-check-circle) — Done's shape differs from every other status, not just its colour | No |
| "Flag cleared this week" heading dot (`OverdueListPage`, still a dead/withdrawn "Coming soon" stub per SPEC §10 point 14 — pre-existing, not touched here) | Small decorative dot, `aria-hidden` | The heading text itself ("Flag cleared this week") | No |
| Avatar-upload "Picture updated." (`AvatarUploadCard`) | Green status text | The sentence itself | No |
| Profile "Saved."/"Password changed." (`ProfilePage`) | Green status text | The sentence itself | No |

**Conclusion: nothing in the current UI relies on colour alone to separate
"accent" from "success"** — every success-green use already carries text or a
distinct icon shape. But the two colours are quantitatively close enough (22°
hue, 1.12:1 contrast to each other) that it's worth a conscious design decision,
not an accident: for a colour-vision-deficient user (deuteranopia/protanopia
compress green/teal hues further than typical vision does), two swatches this
close in hue *and* lightness could still read as "the same colour" if a future
screen ever places them right next to each other with nothing else to
distinguish them (none does today).

**Proposed fix, not implemented (needs approval, and would be a token-only
change in the same one place):** shift `--color-success` away from teal, e.g.
toward a truer green around HSL(145°, 60%, 28%) ≈ `#1a7a46` — about 27° further
from the new accent's 172° than today's 22° gap, same lightness, same existing
contrast ratios (this is a tiny hue nudge, re-verified to keep every existing
`--color-success` contrast pairing unchanged in magnitude). Left as a documented
option, not applied, since it's outside this task's one-place/accent-only scope
and needs your sign-off the same way the accent colour itself did.

### Hardcoded-colour audit outside the token system (light theme)

Grepped every `.tsx`/`.ts`/`.css` file for literal hex and `rgb()`/`rgba()`
values (outside `tokens.css` itself, which *is* the token system).

| Found | Where | Purple/accent-related? | Action |
|---|---|---|---|
| `bg-[rgb(16_16_24_/_40%)]` — a literal dialog-overlay colour, duplicating `--color-overlay` (`rgb(24 24 29 / 32%)`, a close but not identical value) | `CreateUserDialog.tsx`, `TemporaryPasswordDialog.tsx`, `EditTaskDialog.tsx`, `ReopenDialog.tsx` | No — neutral black overlay | **Migrated** to the existing `bg-overlay` utility (already used by `AssigneePickerDialog`/`MoveRefusalDialog`/others) — one-line class swap, four files, no visual intent change beyond matching the token's own slightly different value |
| `rgba(255,255,255,0.18)` — a translucent white "key chip" background drawn on top of accent-coloured buttons | `Topbar.tsx`, `NewTaskDialog.tsx`, `WorkflowStrip.tsx` | Decorative overlay only, not itself a brand colour — looks correct over *any* accent hue since it's relative transparency, not a fixed colour | **Left as-is** — not migrated to a token. A translucent-white decoration isn't a colour identity choice the way `--color-accent` is, and it already renders correctly against both the old purple and the new teal without any change; introducing a one-off token for a single reusable decoration felt like inventing an abstraction nobody asked for. Flagging in case you'd rather it become `--color-key-chip-bg` for consistency regardless. |

No hardcoded hex/rgb found in: buttons, links, focus ring, chips, status bar,
badges, Avatar initials background (`Avatar.tsx` already reads
`var(--avatar-{n}-bg/-ink)`), charts (none exist in this app), or the scrollbar
(`global.css` already uses `var(--color-border-strong)`).

### Dark theme — proposed teal companion (not implemented, your call)

Dark theme's current purple (`#8b8bf5` family) is untouched by this task. Since
light is now teal and dark stays purple, the two themes read as different brand
identities depending on OS/user preference. If you want dark theme to match,
here is a candidate, token-for-token, with the same role split, **measured, not
implemented**:

| Token | Proposed dark value |
|---|---|
| `--color-accent` | `#39b3a2` |
| `--color-accent-hover` | `#4fcab9` |
| `--color-accent-soft` | `#163733` |
| `--color-accent-ink` | `#6fe0cf` |
| `--color-on-accent` | `#0b1f1c` |
| `--color-focus` | `#39b3a2` |

Measured contrast: dark-on-accent text on accent bg = **6.64:1** · `accent-ink`
on dark bg `#111114` = **11.88:1** · on dark surface `#202026` = **10.22:1** ·
on `accent-soft` = **8.13:1** · focus ring on dark bg = **7.31:1** · on dark
surface = **6.28:1** — all comfortably past AA. The same green-proximity
question recurs in dark theme too: this candidate teal vs. dark theme's own
`--color-success` (`#5fd39a`) measures 1.38:1 contrast to each other (same
"close but not colour-alone-dependent today" situation as light theme).

Not applied — this is a proposal for you to approve or reject separately, same
as light theme's own three-option choice was.
