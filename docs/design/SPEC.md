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
Section **Review** (0): dashed empty card "Nothing waiting on you / No one has asked you to review, and none of your work is waiting on a reviewer."
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

## 10. Decisions overriding the wireframe (Gate A, approved 2026-11-08)

Where this section disagrees with the Figma frames, the PNGs or sections 1–9 above, **this section wins**. Recorded because the Figma redesign was drawn without re-checking the already-decided F01 D5 contract and the current MVP scope.

1. **No Reviewer field anywhere** (F01 D5 — no reviewer role; anyone but the current assignee may move a task out of Review). Drop it from the board card (`TF-9 · reviewer Minh`), the Task detail properties panel, and the New task modal's Reviewer dropdown. Task detail may show "submitted by `<name>`" derived from the existing F04 history (a `Review`-entering `StatusChanged` entry's actor) if that's cheap to read; otherwise leave it out rather than inventing a field.
2. **Assign picker: visual re-skin only.** Do not add the "HAS ROOM · MOST FIRST" / "AT CAPACITY" sorting or disabling drawn in §5.5 and §5.2 — that's slice 3.5, already rejected 2026-11-05 (`docs/ROADMAP.md` unit 3). The picker keeps its current alphabetical, nobody-disabled behaviour under the new visual styling.
3. **No per-user in-progress count in this branch.** No `n/3` badge, no Team capacity popover (§5.2) — that data needs a small read-only backend addition (no migration) tracked as separate, future backend work. Slice "Team popover" is deferred out of this redesign entirely.
4. **Overdue list: "Overdue now" table only.** Do not build "Due today — not overdue yet," "Flag cleared this week," or the "Was N days late" / "Cleared — …" copy in §5.7 — out of scope per the signed-off `docs/reports/slice-5.4.md` T9, unchanged by this redesign. Tracked in `docs/ROADMAP.md` backlog, not built here.
5. **Out of MVP, hide rather than dead-link:** Inbox, the Team capacity page, List view, and the `⌘K` search box (§3, §5.1, §5.7, §5.8) are not built. Hide their nav entries and the search box rather than linking to a page that doesn't exist. Existing keyboard shortcuts stay as they are today — do not add `J`/`K`/`⌘↵`/`C` unless they already exist in the current app.
6. **My tasks (§5.8) — build it, trimmed.** Last slice. Build from the existing task API. Drop the "Sent back by `<name>` for rework" chip and the "You finished N tasks this week" footer line (both need data/aggregates this branch doesn't add — see point 3's backend-work principle). Keep: greeting + capacity meter, "In progress" / "Up next" / "Review" sections, per-task "Submit for review" / "Start" actions reusing existing move mutations.
7. **Historical documents** (`docs/ROADMAP.md` per-slice rows, `docs/reports/*.md`, `docs/features/*.md`) keep their `docs/ui.md` citations as dated, point-in-time records — not repointed to `docs/design/SPEC.md`.

**New slice order:** 1 tokens + shell + dark mode → 2 Board + TaskCard + chips + toolbar → 3 Task detail + activity (no Reviewer) → 4 New task + Assign picker re-skin + Blocked dialogs → 5 Overdue now + My tasks. Team popover slice is postponed indefinitely (point 3).
