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

import styles from './NewTaskDialog.module.css'

// Mirrors TaskCreateReq's [MaxLength] attributes (coding-standards.md "limits
// agree across layers") — the server is still the real enforcer.
const TITLE_MAX = 200
const DESCRIPTION_MAX = 4000
const PRIORITIES: TaskPriority[] = ['Urgent', 'High', 'Normal']

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
    <div className={styles.overlay} onClick={() => !mutation.isPending && onClose()}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="nt-title"
        className={styles.dialog}
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <div className={styles.header}>
          <h2 id="nt-title" className={styles.heading}>
            New task
          </h2>
          <button
            type="button"
            aria-label="Close"
            className={styles.closeButton}
            onClick={onClose}
            disabled={mutation.isPending}
          >
            ×
          </button>
        </div>

        {serverError !== null && (
          <p role="alert" className={styles.serverError}>
            {serverError}
          </p>
        )}

        <div className={styles.field}>
          <label htmlFor="nt-title-input" className={styles.label}>
            Title
          </label>
          <input
            id="nt-title-input"
            ref={titleRef}
            type="text"
            className={styles.titleInput}
            value={title}
            maxLength={TITLE_MAX}
            required
            disabled={mutation.isPending}
            onChange={(event) => setTitle(event.target.value)}
          />
          {fieldErrors.title !== undefined && (
            <span role="alert" className={styles.fieldError}>
              {fieldErrors.title}
            </span>
          )}
        </div>

        <div className={styles.field}>
          <label htmlFor="nt-desc-input" className={styles.label}>
            Description <span className={styles.optional}>(optional)</span>
          </label>
          <textarea
            id="nt-desc-input"
            rows={3}
            className={styles.textarea}
            value={description}
            maxLength={DESCRIPTION_MAX}
            disabled={mutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
          {fieldErrors.description !== undefined && (
            <span role="alert" className={styles.fieldError}>
              {fieldErrors.description}
            </span>
          )}
        </div>

        <fieldset className={styles.fieldset}>
          <legend className={styles.label}>Priority — decides where it sits on the board</legend>
          <div className={styles.priorityOptions}>
            {PRIORITIES.map((candidate) => (
              <label
                key={candidate}
                className={`${styles.priorityOption} ${priority === candidate ? styles.priorityOptionSelected : ''}`}
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
            <span role="alert" className={styles.fieldError}>
              {fieldErrors.priority}
            </span>
          )}
        </fieldset>

        <div className={styles.row}>
          <div className={styles.field}>
            <label htmlFor="nt-due-input" className={styles.label}>
              Due date <span className={styles.optional}>(optional)</span>
            </label>
            <input
              id="nt-due-input"
              type="date"
              className={styles.dateInput}
              value={dueDate}
              disabled={mutation.isPending}
              onChange={(event) => setDueDate(event.target.value)}
            />
            <span className={styles.hint}>No date = never overdue, listed after dated tasks.</span>
            {fieldErrors.duedate !== undefined && (
              <span role="alert" className={styles.fieldError}>
                {fieldErrors.duedate}
              </span>
            )}
          </div>

          <div className={styles.field}>
            <label htmlFor="nt-assignee-input" className={styles.label}>
              Assignee <span className={styles.optional}>(optional)</span>
            </label>
            <AssigneeSelect
              id="nt-assignee-input"
              value={assigneeId}
              disabled={mutation.isPending}
              onChange={setAssigneeId}
            />
            <span className={styles.hint}>
              Only people already assigned to a task are listed — there is no user directory yet.
            </span>
            {fieldErrors.assigneeid !== undefined && (
              <span role="alert" className={styles.fieldError}>
                {fieldErrors.assigneeid}
              </span>
            )}
          </div>
        </div>

        <div className={styles.footer}>
          <span className={styles.backlogNote}>New tasks start in Backlog</span>
          <button type="button" className={styles.secondaryAction} onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </button>
          <button type="submit" className={styles.primaryAction} disabled={mutation.isPending}>
            {mutation.isPending ? 'Creating…' : 'Create task'}
          </button>
        </div>
      </form>
    </div>
  )
}
