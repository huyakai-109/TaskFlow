# Kanban TaskFlow — Business Requirements

Team-Based Task Management System.

> Source: *SEAP — Business Requirements Specification*, **Option 1 — Kanban TaskFlow** (pages 2–6).
> This file covers Option 1 only. It describes business behaviour, user scenarios, rules and edge cases — not technical design.

## Features

| ID | Feature |
|---|---|
| F01 | Strict Workflow State Machine |
| F02 | Resource Capacity Guard |
| F03 | Priority-Based Auto-Sorting |
| F04 | Task Audit & History Trail |
| F05 | Deadline & Overdue Management |

---

## F01 — Strict Workflow State Machine

### What this feature does

Every task follows a fixed, one-way lifecycle. The system ensures that work progresses logically — tasks cannot jump stages and skip important review or handoff steps. This enforces discipline and shared visibility across the team.

### Allowed transitions

| Step | Business Action |
|---|---|
| 1 | Backlog → Todo: The team acknowledges the task and places it in the queue. |
| 2 | Todo → In Progress: A team member picks up the task and starts working on it. |
| 3 | In Progress → Review: The work is complete and submitted for peer or quality review. |
| 4 | Review → Done: The reviewer approves the work. The task is closed. |
| 5 | Review → In Progress: The reviewer rejects the work and sends it back for rework. |

### Business rules

- A task can ONLY move to the next allowed stage — no skipping. For example, a Backlog task cannot jump directly to In Progress or Done.
- The only permitted backward move is Review → In Progress, which represents a rework cycle.
- Once a task reaches Done, it is locked. No further movement is allowed unless explicitly reopened by an authorised person.

### Business scenarios

| Scenario | Expected Outcome |
|---|---|
| A user tries to move a task from Backlog directly to In Progress. | The system refuses the action and informs the user that the task must first be moved to Todo. |
| A reviewer sends a task back for rework. | The task moves back to In Progress. The original assignee is notified and resumes work. |
| A user tries to move a Done task to another stage. | The system refuses. The task remains Done until reopened. |

---

## F02 — Resource Capacity Guard

### What this feature does

To prevent team members from being overloaded, the system limits each person to a maximum of 3 tasks in "In Progress" at any given time. When a team member is at capacity, no additional tasks can be assigned to them until they complete or hand off one of their active tasks.

### Business flow

| Step | Business Action |
|---|---|
| 1 | A user attempts to move a task to In Progress and assigns it to Team Member A. |
| 2 | The system checks how many tasks Team Member A currently has in In Progress. |
| 3 | If the count is below 3, the move is allowed. The task becomes active. |
| 4 | If the count is already 3, the system blocks the move and notifies the user that Team Member A has reached their capacity. |
| 5 | The user may instead assign the task to a different team member with available capacity, or wait until Team Member A finishes one of their active tasks. |

### Business rules

- The capacity limit of 3 applies to the assignee, not the requester.
- If a task is reassigned to a different person while it is In Progress, the capacity check applies to the new assignee, not the previous one.
- As soon as one of a member's tasks moves out of In Progress (to Review or Done), their capacity slot frees up immediately.

### Business scenarios

| Scenario | Expected Outcome |
|---|---|
| Team Member A already has 3 active tasks. Someone tries to assign a 4th. | The system blocks the assignment and shows a "Capacity Reached" message. |
| Team Member A moves one task to Review. Someone then assigns a new task. | Allowed — the capacity is now 2 out of 3. |
| A task is reassigned from Team Member A (1 active) to Team Member B (3 active). | Blocked — Team Member B is already at capacity. |

---

## F03 — Priority-Based Auto-Sorting

### What this feature does

The task list always surfaces the most important and time-sensitive work at the top, automatically. Team members do not need to manually sort or filter — the system ensures the highest-priority tasks are always visible first.

### Priority levels

| Priority | Meaning |
|---|---|
| Urgent | The task is critical and must be addressed immediately. Weight: highest. |
| High | The task is important and should be completed ahead of normal work. |
| Normal | Standard priority work. Completed in the order it was received. |

### Sorting logic

- Tasks are always listed Urgent first, then High, then Normal.
- Within the same priority level, tasks with earlier due dates appear first — the most urgent deadlines surface to the top.
- Tasks without a due date appear after those with a due date, within the same priority group.

### Business scenarios

| Scenario | Expected Outcome |
|---|---|
| There are 2 Urgent tasks: one due tomorrow, one due next week. | The task due tomorrow appears first. |
| There is 1 High task with no due date and 1 Normal task due today. | The High task appears first (priority wins over due date). |
| A task is upgraded from Normal to Urgent mid-sprint. | It immediately moves to the top of the list without any manual action. |

---

## F04 — Task Audit & History Trail

### What this feature does

Every significant change to a task is permanently recorded in a history log. This creates a clear, tamper-proof trail of accountability — managers and team members can always see who did what, and when, throughout the lifecycle of any task.

### What gets recorded

- Any status change (e.g., moved from Todo to In Progress).
- Any assignee change (e.g., reassigned from Alice to Bob).

### Log message examples

| Type | Example |
|---|---|
| Status change | "Alice moved Fix Login Bug from Todo to In Progress on 1 June 2025 at 09:30." |
| Assignee change | "Bob reassigned Fix Login Bug from Alice to Charlie on 1 June 2025 at 10:00." |

### Business rules

- History entries are permanent and cannot be edited or deleted, even by administrators. This preserves integrity.
- Any user who views a task can see the full history of changes in chronological order.
- If the change is triggered automatically by the system (e.g., overdue marking), it is recorded as a "System" action.

### Business scenarios

| Scenario | Expected Outcome |
|---|---|
| A manager wants to know who moved a task to Done. | The history trail shows the exact user and timestamp. |
| A task was moved through 3 stages in one day. | All 3 changes appear individually in the history, in order. |
| A team member denies reassigning a task. | The audit log provides an objective, uneditable record. |

---

## F05 — Deadline & Overdue Management

### What this feature does

The system automatically monitors all active tasks and identifies those that have passed their deadline without being completed. These tasks are flagged and visually highlighted in red so that teams can see project delays at a glance without manually checking each task.

### Overdue definition

A task becomes overdue when:

- Its due date has passed (the current date is after the due date), AND
- Its status is still not Done.

A task that is due today is NOT yet overdue — it becomes overdue starting tomorrow.

### Business flow

| Step | Business Action |
|---|---|
| 1 | Every day, the system automatically scans all active tasks. |
| 2 | Any task whose due date has passed and is not Done gets flagged as Overdue. |
| 3 | The task is displayed with a red visual indicator in the board view. |
| 4 | A history entry is recorded: "System marked [Task] as Overdue on [Date]." |
| 5 | If the task is later completed (moved to Done), the overdue flag is cleared. |
| 6 | If the due date is extended to a future date, the overdue flag is cleared immediately. |

### Business rules

- Tasks without a due date are never considered overdue.
- Completing a task always clears its overdue flag, regardless of how late it was.
- The overdue check runs automatically — no manual action is required from any user.

### Business scenarios

| Scenario | Expected Outcome |
|---|---|
| A task was due 3 days ago and is still In Progress. | It is flagged Overdue and shown in red. |
| An overdue task is extended to a new future due date. | The overdue flag is removed immediately. |
| An overdue task is finally moved to Done. | The overdue flag is cleared. The task shows as completed (not overdue). |
