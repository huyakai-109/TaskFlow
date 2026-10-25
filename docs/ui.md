# TaskFlow — UI

The UI reference for `FE/`. Read it before building or changing a screen. Behaviour comes from `docs/features/f01…f05`; this file covers how that behaviour looks and reads.

**Precedence.** The specs in `docs/features/` win over the wireframe. Where they disagree, *Wireframe vs. decided specs* below says which way to go. Colours, spacing, radii and fonts come from `FE/src/styles/tokens.css`, never from a literal value copied out of a screen file.

## Sources

| What | Where |
|---|---|
| Wireframe canvas (view and comment here) | https://claude.ai/artifact/9WFkau8hC24M5eqsZ5xLr2 — "TaskFlow Wireframe" |
| Screen source, one file per artboard | `docs/ui/screens/*.dc.html` (snapshot of the canvas, 2026-10-25) |
| Canvas layout and titles | `docs/ui/screens/canvas.json` |
| Tokens actually used by the app | `FE/src/styles/tokens.css` |

### Reading a `.dc.html` file

The screen files are design markup, not app code. **Do not copy them into `FE/` or run them in a browser**, because they depend on the canvas runtime (`support.js`). Read them for structure, sizes, copy and states.

- Values written inline (`style="padding: 12px; …"`) are the design.
- `Main.dc.html` themes itself: `{{t.card}}` and similar resolve from the `light` / `dark` objects in the `<script>` at the bottom of the file. The other screens are light-only, with the values written inline.
- `BoardDark.dc.html` and `BoardTeam.dc.html` only mount `Main` with `dark` or `teamOpen` switched on.
- `<sc-for>` is a repeated block. `<sc-if>` is a conditional block.
- Names, dates and counts are sample data. Real values come from the API.

## Screens

| File | Screen | Features | Notes |
|---|---|---|---|
| `Main.dc.html` | **Board** (light) | F01 F02 F03 F05 | App shell, toolbar, five status columns, cards. The team popover opens from the avatar stack |
| `BoardDark.dc.html` | Board (dark) | — | Same as Main with dark tokens |
| `BoardTeam.dc.html` | Board with the **team capacity popover** open | F02 | Popover lists everyone sorted by load, with search |
| `AssignPicker.dc.html` | **Assignee picker** and **drop onto a full assignee** | F02 | Picker: "has room" sorted by most room first, then "at capacity" disabled. Drag state: target column turns red-dashed and a tooltip names someone free |
| `TaskDetail.dc.html` | **Task detail** | F01 F04 F05 | Workflow strip, the legal next step only, activity timeline, properties panel |
| `OverdueList.dc.html` | **Overdue** view (list) | F05 F03 | Overdue now · due today (not overdue yet) · flag cleared this week |
| `NewTask.dc.html` | **New task** dialog | F01 F03 F05 | Title, description, priority, due date, assignee |
| `MyTasks.dc.html` | **My tasks**, seen by a Member | F01 F02 F03 F05 | Own capacity, in-progress list with next step, up next, review |
| `BlockedActions.dc.html` | **Refusal dialogs** | F01 F02 | Skipped stage · assignee at capacity · task is Done |

## App shell and layout

- **Sidebar** 232–260px. Its surface is a step dimmer than the content so the board stands out. It holds: workspace name, navigation, saved views (e.g. Overdue with a count), and the signed-in user with their role at the bottom. It stacks above the content at narrow widths (`flex-wrap`).
- **Header** (≈64px): page title or breadcrumb, then search and the primary action on the right.
- **Toolbar** under the header. View controls (Board/List, sort info) sit on the left. **Exceptions only** sit on the right: `N overdue` and `N at capacity` chips, and the team avatar stack. A chip is hidden when its count is 0. Detail lives in the popover, not on the board.
- **Board**: five columns `Backlog · Todo · In Progress · Review · Done`, each at least 232px wide, 14px apart. The row scrolls horizontally when the window is narrow and never wraps.
- Widths to check: 390 / 768 / 1440 (`CLAUDE.md`). Only 1440 has been drawn.

## Visual language

| Element | Spec (from the wireframe) |
|---|---|
| Type | Geist for text, Geist Mono for task keys and keyboard hints. Body 14px; secondary 12–13px; page title 15px semibold; task title in detail 30px semibold. See *Tokens* |
| Radii | Card 10px · column 12px · popover/dialog 14–16px · chip 6px · pill 999px |
| Card | Surface + 1px border + faint shadow (none in dark). Top: key (`TF-7`, mono 11px) and assignee avatar. Title 14px medium. Footer chips: priority, due date |
| Priority icon | Urgent: filled rounded square with "!" · High: three bars, all filled · Normal: three bars, two filled. Always icon **and** word |
| Status icon | Backlog: dashed circle · Todo: empty circle · In Progress: half-filled circle · Review: three-quarter circle · Done: filled circle with a tick |
| Due chip | `22 Oct` · `Due today` (emphasised, not red) · `No due date` (dashed border) |
| Overdue | Card border turns danger-tinted **and** a red chip reads `Overdue 1d` with a clock icon. Never colour alone |
| Avatar | Initials in a 20–26px circle. One background/ink pair per person, from a fixed palette of six |
| Capacity meter | Three 14×6px segments. Filled = accent. All three filled = urgent colour, labelled `Full`. The limit is configuration — read it from the API, never hard-code "3" |
| Done card | Muted. Lock icon + `Locked`. Line `Approved by <name> · <date>` |
| Popover / dialog | Raised surface, 14–16px radius, a strong shadow (light only). Dialogs use `role="alertdialog"` when they report a refusal |
| Toast | Dark pill near the bottom, for a short confirmation |

## UI rules that come from the specs

- **Show only legal moves** (F01 D10). Each task from the API carries the statuses the *calling user* may move it to. Buttons, menus and drop targets come from that list. Never recompute the transition table in the client.
- **Drag and drop** (F01 D9). Dropping a card on a column means "change status to that column" and nothing else. The drop position is ignored, because F03 owns the order. While dragging, columns not in the card's allowed list are visibly disabled. A refused drop puts the card back and shows the server's reason.
- **Keep a non-drag path.** Every move available by drag is also available as a button or menu item (task detail, card menu), for keyboard and screen-reader users.
- **Refusals explain and offer the next step** (`BlockedActions`). Show the error code's message, plus the one action that would work: "Move to Todo", or pick someone with room.
- **Capacity shows up at the moment of choosing.** The picker sorts people with room first and disables people at the limit when the task is (or would be) In Progress. Assigning a task outside In Progress is allowed even to a full person (F02).
- **Order is computed** (F03). Urgent → High → Normal, then earliest due date, undated last. There is no sort control. The `Sort: Priority` button only explains the rule in a tooltip. The Done column is newest-completed first, limited to 20.
- **Overdue** (F05) covers every status except Done, Backlog included. "Due today" is not overdue.
- **History is read-only** (F04). No edit or delete affordance on an entry. A System entry has its own icon, not an avatar.

## Copy

Short, plain and specific. Name the task, the person and the rule, then the way forward. Headings state what happened ("Lan is at capacity", "Move it to Todo first", "This one's finished"). Body text explains in one or two sentences. Buttons are verbs ("Move to Todo", "Assign to Huy and start"). Server error codes map to these messages in one place in the client.

## Wireframe vs. decided specs — follow the spec

The wireframe was drawn before some decisions were locked. Build these the spec's way.

| Wireframe shows | Decided | Do |
|---|---|---|
| A **Reviewer** field in New task and in the Task detail properties | F01 D5: no reviewer role; anyone except the current assignee may move a task out of Review | Remove the field. Task detail can show who submitted it, from history |
| Moves done by buttons only | F01 D9: drag and drop | Build drag and drop and keep the buttons as the accessible path |
| Activity timeline without create, priority, due-date, reopen or delete events | F04 records all of them; reopen stores a reason | Render every event type F04 defines |
| "Ask a manager to reopen" button | No reopen-request flow in the MVP | Plain text for Members. Managers/Admins get a **Reopen** action that asks for a reason (dialog not drawn yet) |
| Capacity dialog "Assign to Huy and start" does both in one click | F01 D4: assign and move are two operations | The client may chain both calls behind one button; if the second fails, say so — the assignment stays |
| Sidebar: Inbox, My tasks, Team capacity, Due this week; ⌘K search; J/K and ⌘↵ shortcuts | MVP scope: board, task create/edit, overdue, history, one notification | Build **Board** and **Inbox** (the rework notification, F01 D7) and the Overdue filter. My tasks, the Team capacity page, Due this week, search and shortcuts are not in the MVP — leave them out unless the user adds them |
| Overdue view section "Flag cleared this week" | Not in F05's contract | Leave out unless F05's contract adds it |
| Task key `TF-7` | The task model has `Id` only | Show `#<Id>` unless a key field is added to the model |

## Not designed yet

Loading skeletons · empty board and empty column · network and server error states · reopen dialog with reason · delete confirmation (Manager/Admin) · edit task · notification inbox · login · 390px and 768px layouts. Follow the visual language above and `.claude/rules/frontend.md`, and record each new decision in this file.

## Tokens

Decided 2026-10-25: `FE/src/styles/tokens.css` takes the wireframe's palette, radii and fonts. Geist and Geist Mono are self-hosted through `@fontsource-variable/geist` and `@fontsource-variable/geist-mono` (OFL-1.1), imported in `global.css`, so no third-party font CDN is called. Use the token, never the hex value in a screen file.

| Wireframe role | Token |
|---|---|
| App background · sidebar · column · card · popover | `--color-bg` · `--color-sidebar` · `--color-column` · `--color-surface` · `--color-surface-raised` |
| Border · strong border | `--color-border` · `--color-border-strong` |
| Text · muted · on dark pill | `--color-text` · `--color-text-muted` · `--color-text-inverse` |
| Keyboard hint background | `--color-kbd` |
| Accent · hover · selected nav (bg / text) · text on accent · focus ring | `--color-accent` · `--color-accent-hover` · `--color-accent-soft` / `--color-accent-ink` · `--color-on-accent` · `--color-focus` |
| Danger · surface · border | `--color-danger` · `--color-danger-surface` · `--color-danger-border` |
| Overdue (aliases of danger) | `--color-overdue` · `--color-overdue-surface` · `--color-overdue-border` |
| Priority Urgent · High · Normal | `--color-priority-urgent` · `--color-priority-high` · `--color-priority-normal` |
| Success · surface | `--color-success` · `--color-success-surface` |
| Capacity meter: filled · full · empty | `--color-accent` · `--color-priority-urgent` · `--color-meter-empty` |
| Avatar pairs 1–6 | `--avatar-N-bg` / `--avatar-N-ink` — pick N from a stable hash of the user id, so a person keeps the same colour everywhere |
| Shadows: card · popover/dialog | `--shadow-card` (none in dark) · `--shadow-raised` |
| Radius: chip · card · column · dialog · pill | `--radius-sm` 6 · `--radius-md` 10 · `--radius-lg` 12 · `--radius-xl` 16 · `--radius-pill` |
| Fonts | `--font-sans` (Geist) · `--font-mono` (Geist Mono) |
| Type sizes 12 · 13 · 14 · 18 · 24px | `--font-size-xs` · `--font-size-sm` · `--font-size-base` · `--font-size-lg` · `--font-size-xl` |

A value the wireframe uses that has no token yet (e.g. the 30px task title, 15px page title) gets a token added here first, not a literal in a component.
