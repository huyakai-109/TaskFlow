# 0005 — Real-time push: scope change

**Signed off 2026-11-13:** approved, with real-time push explicitly limited to presence (v1) for this round; v2 (board/notification push) remains a documented, unstarted roadmap item.

- **Context:** `CLAUDE.md` lists "real-time push" as out of scope. The product owner now asks for a live presence indicator on the board ([f07-presence.md](../features/f07-presence.md)).
- **Decision:** Move "real-time push" into scope, staged:
  - v1: presence only ([f07](../features/f07-presence.md)).
  - v2: board-changed and notification-created signals, sent after commit through an `IRealtimePublisher` port in `BusinessLogic`.
  - Never a generic event bus. The in-app-only notification rule (F01 D7) still holds.
- **Operational risk:**
  - Connection count scales with open tabs.
  - A restart drops every connection; clients reconnect, causing a brief thundering herd.
  - A misconfigured proxy breaks WebSockets silently — presence disappears, and v1 has no other functional loss.
  - Token-in-URL logging risk (mitigated per ADR 0004).
  - `/healthz` does not cover the hub.
- **Proposed `CLAUDE.md` diff (proposal only, not applied by this round):**
  - *Out of scope:* remove "real-time push"; add "real-time push beyond presence until v2 is approved".
  - *Users and roles:* "Members view, create, edit title/description, move **their own** work forward and take over tasks; Manager/Admin plan (priority, due date), assign anyone, delete, reopen; Admin manages accounts."
  - *Users* bullet and *Q-U*: replace "User management stays out of scope" with "Admin-only account management ([f08](../features/f08-accounts-profile.md))".
  - Also fix stale facts found while reading the code for this round: *Status* (task domain is well past "not started"), `InvariantGlobalization` (now `false`, per ROADMAP R6), *CI* (exists), *Users* ("no endpoint creates users" — F08 adds one).
- **Alternatives rejected:**
  - Leaving "real-time push" out of scope and shipping presence anyway: contradicts `CLAUDE.md` silently, which `workflow.md` forbids.
  - Going straight to v2 (board/notification push): larger surface, no proven need yet beyond presence.