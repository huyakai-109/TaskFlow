import { useKnownAssignees } from '../hooks/useKnownAssignees'

interface AssigneeSelectProps {
  id: string
  value: number | null
  disabled?: boolean
  onChange: (assigneeId: number | null) => void
}

/**
 * A plain native `<select>`, not the wireframe's searchable listbox popover
 * (`docs/ui/screens/AssignPicker.dc.html`): that widget's value is sorting by
 * room and disabling people at capacity, both F02, not built yet (see
 * `useKnownAssignees`). A native select is fully keyboard- and
 * screen-reader-operable with no extra listbox logic to get right for a list
 * that cannot yet be sorted or disabled meaningfully.
 */
export function AssigneeSelect({ id, value, disabled = false, onChange }: AssigneeSelectProps) {
  const assignees = useKnownAssignees()

  return (
    <select
      id={id}
      className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] disabled:opacity-60"
      value={value === null ? '' : String(value)}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value === '' ? null : Number(event.target.value))}
    >
      <option value="">Unassigned</option>
      {assignees.map((assignee) => (
        <option key={assignee.id} value={assignee.id}>
          {assignee.userName}
        </option>
      ))}
    </select>
  )
}
