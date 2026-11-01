import { useRef, useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '../../../api/client'
import { describeError } from '../../../api/errorMessages'
import type { TaskDetail } from '../../../api/types'
import { fieldErrorMap } from '../../../shared/lib/fieldErrors'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'
import { useUpdateTaskMutation } from '../hooks/useUpdateTaskMutation'

import styles from './EditTaskDialog.module.css'

// Mirrors TaskUpdateReq's [MaxLength] attributes.
const TITLE_MAX = 200
const DESCRIPTION_MAX = 4000

interface EditTaskDialogProps {
  task: TaskDetail
  onClose: () => void
  onRefused: (code: string | null) => void
}

/**
 * Title/description edit (`PUT /tasks/{id}`), not wireframed (`docs/ui.md`
 * "Not designed yet" lists "edit task") — modelled on `NewTaskDialog`'s field
 * layout and this detail page's `ReopenDialog` for the dialog shell.
 * Business failures (`Conflict`/`NotFound`) are reported up through
 * `onRefused` so the page can toast and refresh, same as reopen/move; shape
 * failures (`Request.Validation.Failed`) stay in the dialog as field errors,
 * with the user's typed input preserved.
 */
export function EditTaskDialog({ task, onClose, onRefused }: EditTaskDialogProps) {
  const [title, setTitle] = useState(task.title)
  const [description, setDescription] = useState(task.description ?? '')
  const [serverError, setServerError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const titleRef = useRef<HTMLInputElement>(null)
  const mutation = useUpdateTaskMutation({ onRefused })

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

    mutation.mutate(
      {
        taskId: task.id,
        title: trimmedTitle,
        description: description.trim().length > 0 ? description : null,
        version: task.version,
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          if (error instanceof ApiError) {
            setFieldErrors(fieldErrorMap(error.validates))
          }

          setServerError(describeError(error))
        },
      },
    )
  }

  return (
    <div className={styles.overlay} onClick={() => !mutation.isPending && onClose()}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="edit-title"
        className={styles.dialog}
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <h2 id="edit-title" className={styles.heading}>
          Edit task
        </h2>

        {serverError !== null && (
          <p role="alert" className={styles.serverError}>
            {serverError}
          </p>
        )}

        <div className={styles.field}>
          <label htmlFor="edit-title-input" className={styles.label}>
            Title
          </label>
          <input
            id="edit-title-input"
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
          <label htmlFor="edit-desc-input" className={styles.label}>
            Description
          </label>
          <textarea
            id="edit-desc-input"
            rows={4}
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

        <div className={styles.actions}>
          <button type="submit" className={styles.primaryAction} disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : 'Save'}
          </button>
          <button type="button" className={styles.secondaryAction} onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
