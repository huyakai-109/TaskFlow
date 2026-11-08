/**
 * Shared avatar helpers — used by the board card and the task detail history
 * timeline (first shared `src/shared/` module: a small cross-feature utility,
 * not a component, so it does not collide with either feature's folder).
 */

export function initialsOf(userName: string): string {
  return userName.slice(0, 2).toUpperCase()
}

/** A stable background/ink pair per person (`docs/design/SPEC.md` Avatar), from a fixed six-colour palette. */
export function avatarPaletteIndex(userId: number): number {
  return ((userId - 1) % 6) + 1
}
