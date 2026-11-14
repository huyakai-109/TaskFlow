import { useUsersQuery } from '../hooks/useUsersQuery'

interface AssigneeSelectProps {
  id: string
  value: number | null
  disabled?: boolean
  onChange: (assigneeId: number | null) => void
  /**
   * Revision R2 (`Task.Create.AssigneeNotPermitted`): a caller without
   * `AssignAnyone` may only create a task unassigned or assigned to
   * themselves. When set, the dropdown offers only "Unassigned" and this
   * one user id — never the full roster — so the UI never invites a choice
   * the server will refuse.
   */
  restrictToUserId?: number
}

/**
 * A plain native `<select>`, not the wireframe's searchable listbox popover
 * (`docs/design/SPEC.md` §5.5 and `docs/design/png/AssignPicker.png`): that widget's value is sorting by
 * room and disabling people at capacity — out of scope (`docs/ROADMAP.md`
 * slice 3.5, rejected). A native select is fully keyboard- and
 * screen-reader-operable with no extra listbox logic to get right for a list
 * that cannot yet be sorted or disabled meaningfully.
 */
export function AssigneeSelect({ id, value, disabled = false, onChange, restrictToUserId }: AssigneeSelectProps) {
  const usersQuery = useUsersQuery()
  const users = (usersQuery.data ?? []).filter((user) => restrictToUserId === undefined || user.id === restrictToUserId)

  return (
    <select
      id={id}
      className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:opacity-60"
      value={value === null ? '' : String(value)}
      disabled={disabled || usersQuery.isPending}
      onChange={(event) => onChange(event.target.value === '' ? null : Number(event.target.value))}
    >
      <option value="">Unassigned</option>
      {usersQuery.isPending && <option disabled>Loading…</option>}
      {usersQuery.isError && <option disabled>Could not load the list</option>}
      {users.map((user) => (
        <option key={user.id} value={user.id}>
          {user.userName}
        </option>
      ))}
    </select>
  )
}
