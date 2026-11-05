import clsx from 'clsx'
import { useRef, useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '../../../api/client'
import { describeError } from '../../../api/errorMessages'
import type { CreateTaskInput } from '../../../api/tasks'
import type { TaskPriority } from '../../../api/types'
import { AssigneeSelect } from '../../../shared/components/AssigneeSelect'
import { fieldErrorMap } from '../../../shared/lib/fieldErrors'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'
import { useCreateTaskMutation } from '../hooks/useCreateTaskMutation'

// Mirrors TaskCreateReq's [MaxLength] attributes (coding-standards.md "limits
// agree across layers") — the server is still the real enforcer.
const TITLE_MAX = 200
const DESCRIPTION_MAX = 4000
const PRIORITIES: TaskPriority[] = ['Urgent', 'High', 'Normal']

const fieldClass = 'flex flex-col gap-[var(--space-1)]'
const labelClass = 'text-[length:var(--font-size-sm)] font-medium text-text-muted'
const fieldErrorClass = 'text-[length:var(--font-size-xs)] text-danger'
const hintClass = 'text-[length:var(--font-size-xs)] text-text-muted'

interface NewTaskDialogProps {
  onClose: () => void
  onCreated: (taskId: number) => void
}

/**
 * `docs/ui/screens/NewTask.dc.html`, minus the Reviewer field (`docs/ui.md`
 * "Wireframe vs. decided specs": F01 D5 has no reviewer role). New tasks
 * always start in Backlog — the server decides that, not this form.
 */
export function NewTaskDialog({ onClose, onCreated }: NewTaskDialogProps) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState<TaskPriority>('Normal')
  const [dueDate, setDueDate] = useState('')
  const [assigneeId, setAssigneeId] = useState<number | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const titleRef = useRef<HTMLInputElement>(null)
  const mutation = useCreateTaskMutation()

  useDialogA11y(titleRef, onClose, mutation.isPending)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const trimmedTitle = title.trim()

    if (trimmedTitle.length === 0) {
      setFieldErrors({ title: 'Title is required.' })
      return
    }

    setServerError(null)
    setFieldErrors({})

    const input: CreateTaskInput = {
      title: trimmedTitle,
      description: description.trim().length > 0 ? description : null,
      priority,
      dueDate: dueDate.length > 0 ? dueDate : null,
      assigneeId,
    }

    mutation.mutate(input, {
      onSuccess: (task) => onCreated(task.id),
      onError: (error) => {
        if (error instanceof ApiError) {
          setFieldErrors(fieldErrorMap(error.validates))
        }

        setServerError(describeError(error))
      },
    })
  }

  return (
    <div
      className="fixed inset-0 z-[100] flex items-start justify-center overflow-y-auto bg-[rgb(16_16_24_/_40%)] px-[var(--space-4)] py-[var(--space-6)]"
      onClick={() => !mutation.isPending && onClose()}
    >
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="nt-title"
        className="flex w-full max-w-[560px] flex-col gap-[var(--space-4)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <div className="flex items-center gap-[var(--space-2)]">
          <h2 id="nt-title" className="m-0 text-[length:var(--font-size-lg)] font-semibold">
            New task
          </h2>
          <button
            type="button"
            aria-label="Close"
            className="ml-auto inline-flex size-8 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent text-[length:var(--font-size-lg)] text-text-muted disabled:cursor-not-allowed disabled:opacity-60"
            onClick={onClose}
            disabled={mutation.isPending}
          >
            ×
          </button>
        </div>

        {serverError !== null && (
          <p
            role="alert"
            className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger"
          >
            {serverError}
          </p>
        )}

        <div className={fieldClass}>
          <label htmlFor="nt-title-input" className={labelClass}>
            Title
          </label>
          <input
            id="nt-title-input"
            ref={titleRef}
            type="text"
            className="border-0 border-b border-border bg-transparent py-[var(--space-2)] text-[length:var(--font-size-lg)] font-semibold text-text focus:outline-2 focus:outline-offset-2 focus:outline-focus"
            value={title}
            maxLength={TITLE_MAX}
            required
            disabled={mutation.isPending}
            onChange={(event) => setTitle(event.target.value)}
          />
          {fieldErrors.title !== undefined && (
            <span role="alert" className={fieldErrorClass}>
              {fieldErrors.title}
            </span>
          )}
        </div>

        <div className={fieldClass}>
          <label htmlFor="nt-desc-input" className={labelClass}>
            Description <span className="font-normal">(optional)</span>
          </label>
          <textarea
            id="nt-desc-input"
            rows={3}
            className="min-h-[72px] resize-y rounded-md border border-border bg-surface p-[var(--space-3)] text-text [font:inherit]"
            value={description}
            maxLength={DESCRIPTION_MAX}
            disabled={mutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
          {fieldErrors.description !== undefined && (
            <span role="alert" className={fieldErrorClass}>
              {fieldErrors.description}
            </span>
          )}
        </div>

        <fieldset className="m-0 flex flex-col gap-[var(--space-2)] border-0 p-0">
          <legend className={labelClass}>Priority — decides where it sits on the board</legend>
          <div className="grid grid-cols-3 gap-[var(--space-2)]">
            {PRIORITIES.map((candidate) => (
              <label
                key={candidate}
                className={clsx(
                  'flex min-h-10 cursor-pointer items-center gap-[var(--space-2)] rounded-md border border-border px-[var(--space-3)]',
                  priority === candidate && 'border-[1.5px] border-accent bg-accent-soft',
                )}
              >
                <input
                  type="radio"
                  name="priority"
                  value={candidate}
                  checked={priority === candidate}
                  disabled={mutation.isPending}
                  onChange={() => setPriority(candidate)}
                />
                {candidate}
              </label>
            ))}
          </div>
          {fieldErrors.priority !== undefined && (
            <span role="alert" className={fieldErrorClass}>
              {fieldErrors.priority}
            </span>
          )}
        </fieldset>

        <div className="grid grid-cols-2 gap-[var(--space-3)] max-[600px]:grid-cols-1">
          <div className={fieldClass}>
            <label htmlFor="nt-due-input" className={labelClass}>
              Due date <span className="font-normal">(optional)</span>
            </label>
            <input
              id="nt-due-input"
              type="date"
              className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit]"
              value={dueDate}
              disabled={mutation.isPending}
              onChange={(event) => setDueDate(event.target.value)}
            />
            <span className={hintClass}>No date = never overdue, listed after dated tasks.</span>
            {fieldErrors.duedate !== undefined && (
              <span role="alert" className={fieldErrorClass}>
                {fieldErrors.duedate}
              </span>
            )}
          </div>

          <div className={fieldClass}>
            <label htmlFor="nt-assignee-input" className={labelClass}>
              Assignee <span className="font-normal">(optional)</span>
            </label>
            <AssigneeSelect
              id="nt-assignee-input"
              value={assigneeId}
              disabled={mutation.isPending}
              onChange={setAssigneeId}
            />
            <span className={hintClass}>Leave unassigned and set it later from the task detail page.</span>
            {fieldErrors.assigneeid !== undefined && (
              <span role="alert" className={fieldErrorClass}>
                {fieldErrors.assigneeid}
              </span>
            )}
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-[var(--space-3)]">
          <span className={hintClass}>New tasks start in Backlog</span>
          <button
            type="button"
            className="min-h-11 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-4)] font-medium text-text disabled:cursor-not-allowed disabled:opacity-60"
            onClick={onClose}
            disabled={mutation.isPending}
          >
            Cancel
          </button>
          <button
            type="submit"
            className="ml-auto min-h-11 cursor-pointer rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent disabled:cursor-not-allowed disabled:opacity-60 max-[600px]:ml-0"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Creating…' : 'Create task'}
          </button>
        </div>
      </form>
    </div>
  )
}
