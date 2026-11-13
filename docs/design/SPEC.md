# TaskFlow UI design spec

Source of truth for the TaskFlow UI. Replaces `docs/ui/` and `docs/ui.md`.

- Design: "TaskFlow Wireframe" (Claude Design canvas), rebuilt in Figma: file `7SKbWcfT5dDQWebWUT4vzn`.
- Pictures: `docs/design/png/*.png` (2x). Static markup: `docs/design/html/*.html` (pixel reference, not production code).
- Tokens below are copied from the source artboard (`Main.dc.html`), not eyeballed.
- All artboards are 1440 x 900 unless stated. Sidebar 232.6 px wide, content starts at x = 260.6.

## 1. Figma frames

| Screen | Figma node-id | PNG | Features |
|---|---|---|---|
| Board (light) | `6-2` | Board-light.png | F01 F02 F03 F05 |
| Board (dark) | `9-2` | Board-dark.png | theme |
| Board + team popover | `11-2` | Board-team.png | F03 |
| Task detail | `8-2` | TaskDetail.png | F02 F05 |
| New task (modal) | `8-140` | NewTask.png | F01 |
| Assign picker | `7-121` | AssignPicker.png | F02 F03 |
| Blocked actions (3 dialogs) | `7-203` | BlockedActions.png | F01 F02 |
| Overdue list | `5-2` | OverdueList.png | F05 F03 |
| My tasks | `7-2` | MyTasks.png | F04 |

URL: `https://www.figma.com/design/7SKbWcfT5dDQWebWUT4vzn/?node-id=<id>`.
If Figma and this file disagree, stop and ask. If the Figma call limit is reached, use this file and the PNGs.

## 2. Design tokens

Map these to CSS variables in `FE/src/styles/tokens.css` and Tailwind v4 `@theme`. Do not hard-code hex in components.

### 2.1 Colors

| Token | Light | Dark | Use |
|---|---|---|---|
| bg | `#FBFBFC` | `#111114` | page background |
| side | `#F2F2F5` | `#0C0C0E` | sidebar, kbd chip |
| col | `#F3F3F6` | `#17171B` | board column, toolbar track |
| card | `#FFFFFF` | `#202026` | cards, panels, inputs |
| border | `#E3E3E8` | `#2D2D35` | all 1 px borders |
| text | `#18181D` | `#EDEDF0` | primary text |
| text-2 (body) | `#3D3E46` | `#D7D7DE` | descriptions, dialog body |
| muted | `#5E5F69` | `#A3A4AE` | labels, meta, icons |
| faint | `#A3A4AE` | `#80818B` | separators "—", disabled |
| accent | `#4F4FCC` | `#8B8BF5` | primary button, active state |
| on-accent | `#FFFFFF` | `#0E0E14` | text on accent |
| kbd-on-accent | `rgba(255,255,255,.18)` | `rgba(14,14,20,.16)` | key chip inside primary button |
| accent-soft | `#EBEBFA` | `#25253D` | active nav item, current step |
| accent-ink | `#3A3AA6` | `#C4C4FA` | text on accent-soft, links |
| danger | `#C42B33` | `#FF8C8C` | overdue text, "Full" |
| danger-soft | `#FFF3F3` | `#2B1A1D` | overdue chip, full row, drop zone |
| danger-border | `#F2C3C5` | `#5A2A30` | overdue card border |
| urgent | `#D92F37` | `#FF6B6B` | Urgent icon, full meter, avatar ring |
| high | `#C85A14` | `#FF9F5A` | High icon, "due this week" dot, Review icon |
| normal | `#8A8B96` | `#80818B` | Normal priority icon |
| meter-off | `#DEDEE4` | `#33333B` | empty capacity segment |
| success | `#1F7A4D` | `#5FD39A` | Done icon, "cleared" text |
| success-soft | `#E6F4EC` | n/a (not shown in dark) | "finished" dialog icon tile |
| warn-soft / warn-ink | `#FFF6EC` / `#8A3B0B` | n/a | "Sent back for rework" chip |
| dashed border | `#CFCFD6` | `#2D2D35` | placeholder / locked step / drop zone |
| overlay | `rgba(24,24,29,.32)` | n/a | modal scrim |
| side-panel (detail) | `#F5F5F8` | n/a | right properties panel on Task detail |

Avatar pairs (background / initials), assigned per person, index 0 to 5:

| # | Light bg / ink | Dark bg / ink | Person |
|---|---|---|---|
| 0 | `#FDE8F1` / `#8E1F52` | `#3A1D2B` / `#F7A8CB` | Lan, Vy |
| 1 | `#DDF3EE` / `#11604E` | `#133229` / `#7FDCC2` | Minh, Long |
| 2 | `#FDF0D5` / `#7A4B07` | `#3A2C12` / `#F3C77A` | Huy |
| 3 | `#E3EBFD` / `#1E3A8A` | `#1A2540` / `#A9C1FA` | Tuấn, Đức |
| 4 | `#EFE7FB` / `#5B2A86` | `#2B2140` / `#D3B8F7` | Mai, Thảo |
| 5 | `#E2F2DC` / `#2F5E1B` | `#1C2E17` / `#A8DB93` | Phương, Nam |

Real app: derive the index from the user id (stable hash mod 6). Initials: first letters, max 2.

### 2.2 Typography

Fonts: **Geist** (UI) and **Geist Mono** (ids, keys). Load from Google Fonts or self-host.

| Role | Size / weight | Where |
|---|---|---|
| Display | 30 / 600, tracking -0.6 | task title on Task detail |
| Greeting | 20 / 600, tracking -0.2 | My tasks banner |
| Section / breadcrumb | 15 / 600 (crumb parent 15 / 400) | topbar, "Activity" |
| Dialog title | 16 / 600 | modals |
| Body L | 14 / 400, line 22 | dialog body |
| Card title, row label | 14 / 500, line 20 | task title |
| Body | 13 / 400 | table cells, buttons 13 / 500 or 600 |
| Meta | 12 / 400 (500 or 600 for emphasis) | chips, hints, counts |
| Eyebrow | 11 / 600 uppercase, tracking +0.44 | "HAS ROOM · MOST FIRST", "AT CAPACITY" |
| Mono | 11 / 400 (13 on Task detail breadcrumb) | `TF-18` keys, `⌘K` |
| Avatar initials | 9 / 600 (11 for 26 px, 8 for tiny) | avatars |

### 2.3 Shape, spacing, elevation

- Radius: 3 (meter segment), 5 (kbd), 6 (chips, inner buttons), 8 (nav item, input, popover row), 9 (buttons, search, segmented), 10 (task card, dialog input), 12 (board column, panels), 14 (large cards), 16 (modal), full for avatars and pills.
- Spacing is mostly 4-based: 4, 8, 10, 12, 14, 16, 20, 24. Card padding 13 x 13, column padding 10, column gap 14 (column width 232.8), modal padding 20 to 23.
- Shadow, card: `0 1px 2px rgba(16,16,24,.06)` (light only; dark has none, uses border).
- Shadow, popover: `0 16px 40px rgba(16,16,24,.16)` light, `.55` black dark.
- Shadow, modal: `0 12px 32px rgba(16,16,24,.10)` (dialogs) and `0 24px 64px .24` (New task).

## 3. Shell (all pages)

- **Sidebar** (232.6 w, `side`, full height): logo tile 26 x 26 radius 7 accent with "T", team name 14/600. Nav items 34 high, radius 8, gap 10, icon 16: Inbox (count), My tasks, Board, Team capacity. Active item: `accent-soft` bg, `accent-ink` text weight 500. "Views" eyebrow (12/400 muted), items with 8 px dot: Overdue (`urgent`, count), Due this week (`high`, count). Footer (49 high, top border): avatar 26, name 14/500, role 12 muted.
- **Topbar**: breadcrumb `Team Alpha / Board` (parent 15/400 muted, current 15/600). Right: search box 260 x 36 (radius 9, `⌘K` chip), primary button **New task** + `C` key chip (110 x 36, radius 9).
- Task detail uses a back link `‹ Board / TF-7` and `J` `K` chips with hint "next / previous task".
- Pages: Inbox and Team capacity are linked in the nav but not designed here (Team capacity is only the popover).
- **Shell layout fix (additive, 2026-11-12, revised three times same day)** — the wireframe draws the shell but not its scroll/overflow mechanics, which broke at 1280x575 (sidebar and topbar scrolling away with the page, 5 fixed-232px columns overflowing a sidebar-width content area). Not shown in the PNGs, decided here; revised twice more the same day — first to a Jira-style fixed-276px-column board, then to this, the standing version, once a fixed column width was found to reintroduce exactly the horizontal-overflow problem the first pass already fixed, just at a different threshold width:
  - The whole shell is exactly the viewport height (`h-dvh`), no page-level scroll. The sidebar and a pinned topbar never scroll. On every page except Board/List, `<main>` is the one scrollable region. On Board and List, `<main>` itself does not scroll either — a **board area** below the (non-scrolling) toolbar is the one scroll region instead, so the toolbar is never more than a few pixels away from the columns.
  - Search and **New task** (and its `C`/`⌘K` shortcuts) moved out of `BoardPage`'s own toolbar into the shared topbar (`FE/src/app/Topbar.tsx`), so both work from every page, not only the Board. Breadcrumb current/parent segments per route: Board, My tasks, Inbox, Team capacity (no parent); Overdue ("Views / Overdue"); Task detail keeps its own back-link form (`‹ Board / #id`) instead of a breadcrumb.
  - **Board columns are `grid-cols-5` (`minmax(0,1fr)` tracks), not a fixed width** — they shrink and grow with the viewport, and the board area is `overflow-y-auto overflow-x-hidden`: no horizontal scrollbar at 640px and up, ever, at any of the five columns. Past ~2180px wide the row caps at that width and centres (`mx-auto max-w-[2180px]`), capping each column near ~420px instead of growing without bound on an ultra-wide monitor. Column/row gap scales with the viewport (`clamp(10px,1.5vw,16px)`) instead of a fixed `--space-4`. A column's own height always follows its card count and never scrolls internally; every column's background is stretched (grid's own default `align-items: stretch`, `min-height: 100%` via `min-h-full` on the row) to the board area's own height, even an empty or short column. Each column's header is `position: sticky; top: 0` against the board area (not `<main>` and not the column itself — the column is deliberately left `overflow: visible`, since any other value would make the column its own nearest scroll container and break the header's stickiness), opaque (`bg-column`) so a card never shows through it while scrolling.
  - **Below 640px, the board shows one status at a time** instead of a column row: a pinned tab bar (`role="tablist"`, one tab per status with its own icon + truncated label + count, arrow-key accessible) switches `grid-cols-1`'s single rendered column; moving a task is the card's existing "Move to …" button (drag was never the only path).
  - **`TaskCard` adapts to its column's own rendered width via a Tailwind v4 container query** (`@container` on `BoardColumn`, no new dependency) rather than a viewport breakpoint, since what matters is the column's width, not the window's: at 230px and up, full chip text and a 20px avatar; from 170px, the priority chip drops its text label (icon + `aria-label` only) and the avatar shrinks to 16px; below 170px the title also clamps to 3 lines instead of growing the card arbitrarily tall. Due/overdue chip text was already short ("14 Nov", "Overdue 5d") and is left as-is at every tier rather than adding an icon-only micro-state to a component shared with three other pages (`DueDateChips.tsx`) for a marginal gain.
  - The List view gets the same treatment minus the container queries: the toolbar stays with Board's, and the table (`table-fixed`, percentage column widths, wrapping cells instead of a hard `min-width`) needs no horizontal scroll at any width, with a sticky, opaque `<thead>`.
  - One thin, token-coloured scrollbar everywhere (`FE/src/styles/global.css`, `scrollbar-width`/`scrollbar-color` plus `::-webkit-scrollbar-*`, including `::-webkit-scrollbar-button { display: none }`) — no browser-drawn arrow buttons, same look in light and dark.
  - Every priority/due/overdue chip is `white-space: nowrap` and `shrink-0`; the chip *row* may still wrap a whole chip to a second line as a last resort, but a chip's own text never breaks mid-word.
  - **Avatar stack diameter is 28px, overlap -5px** (wireframe: 26px/-6px). The "clipped letter" look was never the text overflowing its own avatar's box — it was each avatar's own right edge, where the 2nd initial sits, being *painted over* by the next avatar stacked on top of it. -5px at 28px keeps that covering boundary clear of where the initials render, confirmed by measuring each avatar's own text run against its box (not just against its sibling).
  - **Collapsible sidebar** (not drawn in the wireframe — additive): a toggle button in the sidebar header collapses it to a 64px icon rail and back to 240px. Below 1100px wide it auto-collapses the same way, *unless* the user has clicked the toggle at least once, in which case that explicit choice (persisted in `localStorage`, `FE/src/app/useSidebarCollapse.ts`) wins over the width default on every width until toggled again; both honour `prefers-reduced-motion`. Collapsed nav items show a small dot badge instead of a count, and a hover/focus tooltip with the item's name. Below 601px the sidebar is not shown inline at all (not even collapsed) — it becomes an off-canvas drawer opened from a menu button in the topbar, with the same focus-trap/Esc/focus-return pattern every other dialog in this app already uses (`useDialogA11y`).

## 4. Components

- **TaskCard**: 212.8 w, `card`, radius 10, 1 px `border`, card shadow. Row 1: key (mono 11 muted) left, assignee avatar 20 right. Row 2: title 14/500 (wraps, line 20). Row 3: chips. Overdue or in-review-late card: border `danger-border`. Done card: opacity .85, title and meta `muted`, lock icon + "Locked", line "Approved by Minh · 16 Oct". Review card key reads `TF-9 · reviewer Minh`.
- **PriorityChip**: 21 high, radius 6, 1 px border, icon 12 + label 12/400. Icons: Urgent = filled red rounded square with "!"; High = 3 bars all `high`; Normal = 2 bars `normal`, third `meter-off`.
- **DueChip**: same shape. Plain date (`14 Nov`), "Due today" (text weight 500), "No due date" (dashed border).
- **OverdueChip**: `danger-soft` bg, no border, clock icon + "Overdue 2d", 12/600 `danger`.
- **ColumnHeader**: status icon 14 + name 13/600 + count 13/400 muted. Icons: Backlog dashed circle, Todo circle, In Progress half circle `accent`, Review 3/4 circle `high`, Done filled `success` check.
- **Segmented control** (Board | List): track `col`, 36 high, radius 9, active segment `card` radius 6 with shadow.
- **Sort button**: bordered, 36 high, icon + "Sort:" + value 500.
- **Filter pills** (top right of Board): `2 overdue` (`danger-soft`, clock icon) and `1 at capacity` (border `danger-border`, dot). Click filters the board.
- **AvatarStack**: 26 avatars, overlap -6, ring 2 px `bg` (ring `urgent` if the person is full), "+6" bubble, chevron opens Team popover.
- **CapacityMeter**: three 14 x 6 segments radius 3. Filled = `accent`, filled when full = `urgent`, empty = `meter-off`. Label `1/3` (12 muted) or `Full` (12/600 `danger`).
- **Popover (Team capacity)**: 340 x 467, radius 14, border, popover shadow. Header "Team capacity" + "In progress · max 3". Search "Find a teammate". Summary: **1** full, **2** almost full, **8** have room (numbers bold, `danger` for first). Rows 36 high, avatar 22, name 13, meter, label; full row has `danger-soft` bg. Footer link "Open Team capacity →" (`accent-ink`, 500).
- **Buttons**: primary (accent bg, 600), secondary (card bg, 1 px border, 500), ghost text. Heights 36 (toolbar), 40 (dialogs), 44 (Task detail primary). Primary may show a key chip (`⌘↵`).
- **Dialog**: 389 w, radius 16, border, shadow, padding 23; icon tile 32 x 32 radius 10; title 16/600; body 14 `text-2`; buttons 40 high; `Esc` chip after Cancel/Close.
- **WorkflowStepper**: pills 24 high radius 12 separated by "—". Past = `col`-ish grey pill + check; current = `accent-soft` + `accent-ink` 600; future = dashed pill (26 high).
- **Activity row**: avatar 20, **actor** 600, sentence 400, timestamp right (12 muted, format `15 Oct, 09:30`). System actor uses `danger-soft` circle with clock icon and `Overdue` in `danger` 600.

## 5. Screens

### 5.1 Board (`6-2` light, `9-2` dark)
Toolbar: segmented Board/List, Sort button, then (right) overdue pill, capacity pill, divider, AvatarStack, chevron. Five columns in order Backlog, Todo, In Progress, Review, Done, each 232.8 w, radius 12, background `col`; board may scroll horizontally (the last column overflows 1440).
Cards sort by priority then due date. Counts in headers. Done cards locked. Overdue card shows OverdueChip next to priority. In-progress column header shows `4` (limit is per person, not per column).
Data shown: TF-21, TF-22 (Backlog), TF-18, TF-15, TF-19 (Todo), TF-7, TF-11, TF-13, TF-16 (In Progress), TF-9, TF-12 (Review), TF-4, TF-2 (Done).

### 5.2 Team popover (`11-2`)
Board plus popover anchored under the avatar stack (top right, y = 121). Section 4 "Popover". Lists all 11 members ordered full first, then most load, then least. Page behind is not dimmed.

### 5.3 Task detail (`8-2`)
Two columns. Left (content max about 800): title (display), description 15/400 line 24, **Workflow** card (stepper, primary action "Submit for review" `⌘↵`, helper "The only next step from In Progress. A reviewer can send it back here for rework."), **Activity** list with header note "Permanent — entries can't be edited or deleted" (lock icon). Right panel 300 w `#F5F5F8`: Status, Priority, Assignee (+ "3/3 full" in `danger`), Reviewer, Due date ("19 Oct · 1 day overdue" `danger` 600, hint "Moving the date into the future clears the overdue flag."), Created ("14 Oct by Huy").
Keyboard: `J` next task, `K` previous task, `⌘↵` primary action.

### 5.4 New task modal (`8-140`)
Board dimmed (`overlay`), modal 620 x 652 at x = 293. Breadcrumb chip `Team Alpha › New task`, close X. Fields in order: Title (borderless large input), Description (optional, 88 high box), Priority (3 segmented cards: Urgent / High selected / Normal; label "Priority — decides where it sits on the board"), Due date (optional; hint "No date = never overdue, listed after dated tasks."), Reviewer, Assignee (optional; dropdown with avatar, name, "0/3 in progress"; hint "The picker lists people with the most room first. The 3-task limit applies when work starts (In Progress), not when a task is assigned."). Footer bar: "New tasks start in Backlog", Cancel, **Create task** `⌘↵`.
Selected option style: 1 px `accent` border + `#F7F7FE` bg.

### 5.5 Assign picker (`7-121`)
Left: card "Audit log export (TF-13)" row with current assignee chip, popover 362 w under it: search "Assign to…", eyebrow "HAS ROOM · MOST FIRST", rows (avatar 22, name, 3-segment meter, `n/3`), "+ 5 more with room", divider, eyebrow "AT CAPACITY", disabled row (avatar 60% opacity, "Lan · can't take more active work", `Full`). Selected row `accent-soft`.
Right: drag state. Todo column with dashed ghost slot; In Progress column highlighted `danger-soft` with dashed `danger-border`; dark tooltip (bg `#18181D`, radius 12) "Lan is full (3/3) / Drop to pick someone with room — Huy is free."; dragged card floats with large shadow.

### 5.6 Blocked actions (`7-203`)
Three dialogs side by side (each 389 w):
1. **Move it to Todo first**: "“Dark mode for board” is still in Backlog. Tasks move one stage at a time, so it goes to Todo before anyone can start it." Buttons **Move to Todo** / Cancel `Esc`.
2. **Lan is at capacity** (danger-soft icon tile): "Lan already has 3 tasks in progress. Pick someone with room for “Payment timeout on checkout”:" then inline picker (Huy selected "0/3 · most room", Minh 1/3), button **Assign to Huy and start**.
3. **This one's finished** (success-soft icon tile, lock): "“Seed demo users” was approved and is locked in Done. If it needs more work, a manager can reopen it." Buttons **Ask a manager to reopen** / Close `Esc`.

### 5.7 Overdue list (`5-2`)
Breadcrumb `Team Alpha / Views / Overdue`, segmented Board/List. Info card (1024 w, 67 h): "Checked automatically every night at 00:05. A task due today becomes overdue tomorrow. Finishing it, or moving the due date into the future, clears the flag." and "Last check: today, 00:05".
Group 1 **Overdue now** (count): table columns Task (key + title), Status, Priority, Due, Late by (`1 day` in `danger` 600, pill), Assignee (avatar + name). Caption: "Sorted like the board: priority first (Urgent → High → Normal), then earliest due date."
Group 2 **Due today — not overdue yet**: one row, Due "Due today, 20 Oct".
Group 3 **Flag cleared this week**: rows with "Was 3 days late" and result in `success` 600: "Cleared — moved to Done, 17 Oct" or "Cleared — due date moved 16 Oct → 27 Oct".
Timezone for "00:05" is `Workflow:TimeZone` (Asia/Ho_Chi_Minh). Dates are rendered in that zone.

### 5.8 My tasks (`7-2`)
Viewer is Lan (Member). Banner card (944 w): greeting "Good afternoon, Lan", sentence "You're at full capacity. Send something to review before picking up new work.", meter (3 red segments) + "3/3 in progress" (`danger` 700) + "0 slots free".
Section **In progress** (3) with "Most urgent first": rows (card, radius 14, 944 w, 65 to 66 high): priority icon, title 14/500, key mono, chips (OverdueChip, warn chip "Sent back by Minh for rework", plain "Due today", "Due 25 Oct"), action button right: **Submit for review** (primary on the first row, secondary on others).
Section **Up next** (1, half-width card 464 w): TF-18 "Todo · 22 Oct", button **Start** disabled style, note "Frees up when one of your 3 moves to Review." (Start is disabled while the user is at 3.)
Section **Review** — split into two, see §10 point 12 (additive, 2026-11-12, Review-grouping task): "Ready for you to review" (dashed empty card "Nothing to review right now") and "Your work in review" (dashed empty card "None of your work is waiting on a reviewer").
Footer strip: "You finished 2 tasks this week" + **Show**.

## 6. Business rules visible in the UI (must hold in the front end)

1. A person can have at most **3** tasks In Progress. Starting or dragging a 4th is blocked with the "Lan is at capacity" dialog and an inline picker of people with room. Assigning a Backlog or Todo task has no limit.
2. Tasks move **one stage at a time** (Backlog, Todo, In Progress, Review, Done). Skipping shows "Move it to Todo first".
3. Review can send a task back to In Progress (activity: "sent it back from Review to In Progress for rework"). Done is **locked**; only a manager can reopen.
4. Activity log is append-only. System events (OverdueMarked) show actor "System".
5. Overdue: no due date means never overdue; overdue is set nightly by the scan, cleared by finishing the task or moving the date into the future.
6. Sorting everywhere: priority (Urgent, High, Normal) then earliest due date; tasks without a date last.
7. Keyboard: `C` new task, `⌘K` search, `J`/`K` next/previous task, `⌘↵` primary action, `Esc` closes dialogs.

## 7. Things the design needs that the API may not provide yet

Check each against current endpoints before building the slice. Do not invent endpoints.
- Per-user in-progress count for the picker and popover (capacity list).
- `isOverdue` filter exists (F05). "Late by", "Was N days late", "Cleared ..." history rows need overdue-cleared events with the reason and dates.
- "Due today, not overdue yet" group on the Overdue page.
- "You finished 2 tasks this week", "Sent back by Minh for rework" (needs last transition and actor), reviewer on each task.
- Inbox count and Inbox page, Team capacity page, List view, search (`⌘K`) are only hinted at; confirm scope before building.
- Dark mode toggle: design exists for the Board only; derive the rest from the dark token column.

## 8. Build notes

- Tailwind v4 only; express tokens as CSS variables so dark mode is a class or `data-theme` switch on the root.
- Icons in the design are simple 24 px stroke icons (stroke 1.8 to 2.2, round caps). Use one icon set or inline SVG; do not add a new UI library without asking.
- Match pixel sizes in section 2 and 4 first, then verify by screenshot against the PNG for that screen. List remaining differences at the end of each slice.

## 9. Theming mechanism (carried over from `docs/ui.md`, decided 2026-11-01)

`FE/src/styles/tokens.css` stays the one source of truth for every value in section 2 — nothing here changes what a token resolves to, only how a component reaches it.

- **Setup**: `@import 'tailwindcss';` in `FE/src/styles/global.css` (the app's root CSS file, imported once from `main.tsx`), registered as a Vite plugin in `vite.config.ts`. CSS-first config (Tailwind v4 style) — no `tailwind.config.js`.
- **Token mapping**: `global.css`'s `@theme inline` block maps colours, shadows, radii and font families into Tailwind's theme namespaces, each entry pointing *at* the existing `--color-*`/`--shadow-*`/`--radius-*`/`--font-*` custom property (not a copy of its value) — e.g. `--color-bg: var(--color-bg);` generates the utility `bg-bg`, which still resolves through `var(--color-bg)` at paint time, so it repaints under `prefers-color-scheme` or `[data-theme]` exactly like a CSS Module rule did. Spacing (`--space-*`) and font size (`--font-size-*`) are **not** remapped into Tailwind's `--spacing-*`/`--text-*` namespaces — both are multi-value scales in Tailwind v4, and only reassigning the keys this app happens to use risked a silent mismatch elsewhere in that scale. Components reach those two token groups through Tailwind's arbitrary-value syntax instead: `p-[var(--space-3)]`, `text-[length:var(--font-size-sm)]`.
- **Dark mode**: `tokens.css` switches dark values two ways — `@media (prefers-color-scheme: dark)` (unless `[data-theme="light"]` overrides it back) and an explicit `:root[data-theme="dark"]` override regardless of the OS. `global.css` declares a `@custom-variant dark` that reproduces those same two rules, so a `dark:` utility only ever fires when a token's own dark value would also apply.
- **Conditional classes**: `clsx` (approved dependency, 2026-11-01), used in place of CSS Modules' multi-class template strings.
- **Import direction**: `FE/src/` is organized `app/` (router, providers, pages that compose features) · `features/<feature>/{components,hooks,utils}` (plus the feature's own page file(s) at its root) · `shared/{components,hooks,lib,utils}` · `api/` · `styles/`. Dependency direction is `shared → features → app`: a feature never imports another feature (compose multiple features together only in `app/`), and `shared/` never imports `features/` or `app/`. Enforced by `FE/.oxlintrc.json`'s `no-restricted-imports` overrides, scoped per folder.

## 10. Decisions overriding the wireframe (Gate A, approved 2026-11-08; §10.2–§10.6 superseded 2026-11-11, "UI complete" task; §10.10–§10.11 added 2026-11-12, layout-fix task; §10.12 added 2026-11-12, Review-grouping task)

Where this section disagrees with the Figma frames, the PNGs or sections 1–9 above, **this section wins**. Recorded because the Figma redesign was drawn without re-checking the already-decided F01 D5 contract and the current MVP scope. Points 2–6 below originally scoped work *out*; the 2026-11-11 task reopened them because the data each needs turned out to already be loadable client-side (same reasoning as point 9's My tasks), not because the backend contract changed. Superseded wording is kept, struck through in spirit, with the new ruling beside it — history isn't deleted, `docs/ui.md` citations rule (point 7) stays the same idea applied forward.

1. **No Reviewer field anywhere** (F01 D5 — no reviewer role; anyone but the current assignee may move a task out of Review). Drop it from the board card (`TF-9 · reviewer Minh`), the Task detail properties panel, and the New task modal's Reviewer dropdown. Task detail may show "submitted by `<name>`" derived from the existing F04 history (a `Review`-entering `StatusChanged` entry's actor) if that's cheap to read; otherwise leave it out rather than inventing a field. **Unchanged — still forbidden, this is the F01 D5 contract, not a scope call.**
2. **Assign picker — sort/disable reopened 2026-11-11.** Originally "visual re-skin only" (slice 3.5's "HAS ROOM · MOST FIRST" / "AT CAPACITY" grouping rejected 2026-11-05). Per-user in-progress counts are derivable client-side from the board's already-loaded `InProgress` column (same source as point 9) without any backend change, so the grouping and disabled "Full" rows in §5.5/§5.2 are back in scope. Carries the same pagination caveat as point 9: a person whose in-progress tasks aren't on the board's loaded page is undercounted. **Status: built 2026-11-11** — `AssignAndMoveDialog` (`Task.Move.AssigneeRequired`'s picker, always about to start the task) applies the grouping unconditionally; `AssigneePickerDialog` (Task detail's general reassignment) applies it only while the task being reassigned is currently `InProgress` (F02 D2 — reassigning a Backlog/Todo/Review/Done task has no capacity limit per F01 D1, so grouping/disabling there would misrepresent a rule that doesn't apply; that picker keeps its plain alphabetical list for the other four statuses).
3. **Team capacity — popover and page reopened 2026-11-11.** Originally deferred indefinitely ("needs a small read-only backend addition"). That turned out unnecessary: `GET /tasks/board`'s `InProgress` column already carries `assignee` per task, so a per-user count (`n/3`, "Full") is a client-side reduction over data already fetched for the board, combined with `GET /users` for the full roster (including people with zero in-progress tasks, who'd otherwise be invisible). No backend change. **Status: built 2026-11-11** — `/team-capacity` page (`FE/src/features/team-capacity/TeamCapacityPage.tsx`), the Board toolbar's `AvatarStack` + "`N` at capacity" pill, and `TeamCapacityPopover` anchored under the avatar stack (fixed, centered positioning below 601px so it never clips off-screen — the design's fixed 340×467 popover assumes desktop width).
4. **Overdue list — "Due today" reopened, "Flag cleared this week" stays out.** "Due today — not overdue yet" is answerable from the existing `GET /tasks` list (a non-overdue task whose `dueDate` is today) — no backend change. **Status: built 2026-11-11** — its own paginated fetch (`useDueTodayTasksQuery`, `GET /tasks?isOverdue=false`), not derived from the board cache; "Checked" progress is shown honestly (how many non-overdue tasks have been scanned so far), never claiming completeness beyond what's been fetched. "Flag cleared this week" and "Was N days late" still need an overdue-cleared history event carrying the reason and both dates, which `TaskHistoryEvent` does not have — genuinely blocked on a backend addition, stays out of scope (COMING-SOON.md, category B).
5. **Inbox, Team capacity page, List view, `⌘K` search — reopened 2026-11-11, no longer hidden.** All four are answerable from endpoints that already exist:
   - **Inbox** (`/inbox`, `FE/src/features/inbox/InboxPage.tsx`) — built 2026-11-11, lists both read and unread rows from the existing `GET /notifications`.
   - **Team capacity** — see point 3, built.
   - **List view** — built 2026-11-11 as a client-side re-render of the board's already-loaded columns as one flat, sortable-by-nothing-extra table (`FE/src/features/board/components/BoardListView.tsx`); no new endpoint, the Board/List segmented control just swaps the view of the same `useBoardQuery` data.
   - **Search (`⌘K`)** — built 2026-11-11 as a client-side quick-switcher over the board's already-loaded tasks (title/`#id` match → jump to `/tasks/:id`), not a server full-text search — there is no search endpoint, and none is added. Named "search" in the UI per the design, documented here as "jump to a loaded task" so it is never mistaken for project-wide search.
   Keyboard shortcuts `C` (new task, Board), `J`/`K` (next/previous task within the current status column, Task detail), `⌘↵` (primary workflow action, Task detail) are added — built 2026-11-11. They are scoped to the page where their action already lives rather than lifted into a global command layer (that would need lifting the New-task dialog and its mutation out of `BoardPage`, a larger change not required by the brief).
6. **My tasks — "Sent back for rework" reopened, then reverted; "finished N tasks" stays out as a "Coming soon" stub.** The "Sent back by `<name>` for rework" chip was briefly built (`useSentBackBy`, reading each visible "In progress" row's own history) but that is one extra `GET /tasks/{id}/history` request per rendered row — an N+1 fetch pattern, even though bounded to ≤3 rows by F02's cap. **Reverted and reclassified category B** (`docs/design/COMING-SOON.md` B5): needs either a field already on `TaskSummary`/the board response, or a batch endpoint, not a per-row fetch. "You finished N tasks this week" (B2) needs a dated aggregate (count of a user's `StatusChanged → Done` events in the last 7 days) no endpoint returns — its footer strip is a real "Coming soon" stub (`MyTasksPage.tsx`, `—` placeholder, "Show" button, no API call). The TaskCard "Approved by `<name>` · `<date>`" line (§4) had the identical N+1 problem (`useApprovedBy`, one history request per visible Done card) — reverted the same way, back to the original "Locked · `<date>`" line, reclassified **B6**.
7. **Historical documents** (`docs/ROADMAP.md` per-slice rows, `docs/reports/*.md`, `docs/features/*.md`) keep their `docs/ui.md` citations as dated, point-in-time records — not repointed to `docs/design/SPEC.md`.
8. **`Task.Move.CapacityReached` stays a plain refusal, not §5.6's "Lan is at capacity" picker.** `docs/features/f02-capacity.md` AC22 requires the client to "show the right message and roll the card back" for this exact code. This is unchanged by the 2026-11-11 reopening of point 2: picker *grouping/disabling* (which rows are shown, in what order) is a presentation question; CapacityReached's refusal-not-reassignment behaviour is the signed-off AC22 contract and stays exactly as is. `Task.Move.AssigneeRequired` (no assignee at all) is unaffected and keeps its existing reassign-and-retry dialog.
9. **My tasks reads the board's already-loaded data, filtered client-side by the signed-in user's id.** `GET /tasks` has no `AssigneeId` filter (confirmed 2026-11-09) and still doesn't — no backend change in this branch. Known, accepted limitation: `GetBoard` caps each active column at `BoardColumnPageSize` (50) and only loads more once a column's own "Show more" is clicked, so a Todo-sourced "Up next" item beyond the first 50 team-wide active Todo tasks would be silently missing from My tasks. In-progress is unaffected (F02 caps any one user at 3, always within page 1). The same caveat now also applies to point 2 (Assign picker grouping), point 3 (Team capacity counts) and point 6 (Sent-back chip, N/A — that one reads per-task history, not a column): not fixed by client-side pagination-chasing — tracked as a documented limitation, not a bug.

10. **Pages use the full content width, additive, 2026-11-12.** Team capacity (560px), Inbox (720px) and My tasks (1000px) were each gated to a narrow hard `max-w` inside a ~1312px content area, leaving them visibly stuck in the top-left corner once the shell layout fix (above) made that content area's real width measurable. Every page now shares one frame: `mx-auto w-full max-w-[1680px]` (Board/List keep their own, wider `max-w-[2180px]` cap instead — five fixed-width-adjacent columns need more room than a single reading column does), with no cap at all below that width. Team capacity's single popover-style list became a `grid-template-columns: repeat(auto-fill, minmax(280px, 1fr))` card grid (same full-first ordering, same summary line above it) so the width is actually used, not just stretched; My tasks's three sections (In progress / Up next / Review) sit side by side at `min-[1100px]:grid-cols-3` instead of one stacked column; Task detail's `main` lost its own redundant `max-w-[800px]` (its flex-grow factor already wanted the space); Inbox needed no layout change, only the cap removed, since its rows were already full-width with a right-aligned timestamp. Overdue's and List's tables switched from a hard `min-width` (which forced a horizontal scrollbar below it) to `table-fixed` with percentage column widths and wrapping cells, so neither ever needs one.
11. **Board card action button, additive, 2026-11-12.** The wireframe draws no action button on a card at all (`docs/design/SPEC.md` §4's `TaskCard` — key, avatar, title, chips only); the "non-drag path" requirement (§5.1) still needs one. The first attempt (an absolute bar sliding up from the card's bottom edge) covered the chip row on hover, which is exactly the content it's supposed to coexist with. Standing version: a small round icon button (`→`) sits in the card's own first row, between the key and the avatar — reached by `opacity`/`pointer-events` on hover or focus-within (never `display:none`, so it stays keyboard-reachable), always shown on touch (`@media (hover: none)`). One legal move: the button acts directly (same `onMove` call every other path already used). More than one: it opens a small `role="menu"` popover (arrow keys, Enter, Esc, outside-click-to-close) listing each destination as a `menuitem` — modelled on `AvatarStack`'s own popover (click-outside + Esc), not a new pattern. No legal move: no button at all, same as before.

12. **My tasks's Review column splits into two sections, additive, 2026-11-12 (Review-grouping task).** The single Review list mixed two different things under one undifferentiated heading: tasks the viewer can act on (someone else's, per F01 D5) and tasks the viewer cannot (their own, waiting on someone else) — the latter had no action button and no label explaining why. Split into **"Ready for you to review"** (`item.assignee?.id !== userId` — F01 D5: anyone except the current assignee may move a task out of Review; this is explicitly *not* a designated-reviewer role, so neither this heading nor its subtitle "Anyone except the assignee can review." may imply one) and **"Your work in review"** (`item.assignee?.id === userId`, no action button, a "Waiting for a reviewer" status chip instead — the viewer is the one person who cannot act on this task). Same row style (`TaskRow`) as every other My tasks group; "Ready for you to review" additionally shows "Assigned to `<name>`" and reuses the Board card's own multi-destination menu (`CardMoveAction`, moved to `shared/components/` as its second use) when a task has more than one legal transition out of Review, instead of a single button. Client-side split over the same already-loaded `['board']` Review column — no backend change, same pagination caveat as point 9 (`reviewMayBeIncomplete`), plus its own local display cap of 5 rows with a "see all on Board" link when more rows are loaded than shown. Per-section empty states, dashed-border style per §5.8: "Nothing to review right now." / "None of your work is waiting on a reviewer."

13. **"Flag cleared this week" removed for good, 2026-11-13** (§5.7 group 3, §7 bullet). Product-owner directive, not a deferral — no `OverdueCleared` event is ever built. Full rationale: [docs/features/f05-overdue.md](../features/f05-overdue.md) Revision R2.
14. **Inbox replaced by Activity, 2026-11-13.** "Activity" takes the "Inbox" slot in the sidebar nav, no counter; `/inbox` is removed entirely (no redirect). The bell keeps read history — unread shown bold with an accent dot, read shown muted, the badge counts unread only, plus "Mark all as read". Full contract: [docs/features/f04-activity-feed.md](../features/f04-activity-feed.md), [docs/features/f06-notification-history.md](../features/f06-notification-history.md).
15. **Board toolbar, 2026-11-13.** The AvatarStack + Team popover (point 3 above) is replaced by a presence stack showing who is currently viewing the board (Confluence-style) — [docs/features/f07-presence.md](../features/f07-presence.md). The Team capacity page itself is unchanged; its own capacity pill stays on the board. "Sort" becomes a real control (Priority · Due date · Newest) — [docs/features/f10-read-models-and-sort.md](../features/f10-read-models-and-sort.md) §1.
16. **Planning-rights fields, 2026-11-13.** Priority and due date are read-only (or hidden in the New task modal) for a caller without `PlanTasks` — Members no longer edit them. Task detail and the card menu gain a "Take over" action, and `AssignAndMoveDialog` becomes "Take it and start" (self only — a Manager cannot start a task for someone else either). Full contract: [docs/features/f01-workflow.md](../features/f01-workflow.md) Revision R2.

**Slice order actually run 2026-11-11:** slice 1 — ComingSoon infrastructure + Inbox + Team capacity page + Board List view + search quick-switch + keyboard shortcuts (`C`/`J`/`K`/`⌘↵`); slice 2 — Board toolbar AvatarStack + "N at capacity" pill + Team popover; slice 3 — Assign picker HAS ROOM/AT CAPACITY grouping + Done card "Approved by" line; slice 4 — Overdue "Due today" + My tasks "Sent back for rework" chip. **Second pass, same day:** Done card "Approved by" and My tasks "Sent back by" were found to be N+1 fetch patterns on closer review and reverted (reclassified B5/B6); real "Coming soon" stubs were built for all 4 genuinely-blocked B items (flag-cleared-this-week, finished-this-week, last-check, the in-progress limit display); "may be incomplete" warnings (My tasks's own established pattern) were added to Team capacity and search. Category C (Reviewer field, CapacityReached picker — both contradicting a signed-off contract) remains out of scope, untouched.
