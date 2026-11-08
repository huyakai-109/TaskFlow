import { useRef, useState } from 'react'
import type { FormEvent } from 'react'

import { ApiError } from '../../../api/client'
import { describeError } from '../../../api/errorMessages'
import type { TaskDetail } from '../../../api/types'
import { fieldErrorMap } from '../../../shared/lib/fieldErrors'
import { useDialogA11y } from '../../../shared/hooks/useDialogA11y'
import { useUpdateTaskMutation } from '../hooks/useUpdateTaskMutation'

// Mirrors TaskUpdateReq's [MaxLength] attributes.
const TITLE_MAX = 200
const DESCRIPTION_MAX = 4000

const fieldClass = 'flex flex-col gap-[var(--space-1)]'
const labelClass = 'text-[length:var(--font-size-sm)] font-medium text-text-muted'
const fieldErrorClass = 'text-[length:var(--font-size-xs)] text-danger'
const actionButtonClass =
  'min-h-11 cursor-pointer rounded-md px-[var(--space-4)] disabled:cursor-not-allowed disabled:opacity-60'

interface EditTaskDialogProps {
  task: TaskDetail
  onClose: () => void
  onRefused: (code: string | null) => void
}

/**
 * Title/description edit (`PUT /tasks/{id}`), not wireframed (`docs/design/SPEC.md`
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
    <div
      className="fixed inset-0 z-[100] flex items-start justify-center overflow-y-auto bg-[rgb(16_16_24_/_40%)] px-[var(--space-4)] py-[var(--space-6)]"
      onClick={() => !mutation.isPending && onClose()}
    >
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="edit-title"
        className="flex w-full max-w-[520px] flex-col gap-[var(--space-4)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <h2 id="edit-title" className="m-0 text-[length:var(--font-size-lg)] font-semibold">
          Edit task
        </h2>

        {serverError !== null && (
          <p
            role="alert"
            className="m-0 rounded-md border border-danger-border bg-danger-surface p-[var(--space-3)] text-[length:var(--font-size-sm)] text-danger"
          >
            {serverError}
          </p>
        )}

        <div className={fieldClass}>
          <label htmlFor="edit-title-input" className={labelClass}>
            Title
          </label>
          <input
            id="edit-title-input"
            ref={titleRef}
            type="text"
            className="min-h-10 rounded-md border border-border bg-surface px-[var(--space-3)] text-text [font:inherit] font-semibold"
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
          <label htmlFor="edit-desc-input" className={labelClass}>
            Description
          </label>
          <textarea
            id="edit-desc-input"
            rows={4}
            className="min-h-[96px] resize-y rounded-md border border-border bg-surface p-[var(--space-3)] text-text [font:inherit]"
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

        <div className="flex gap-[var(--space-3)]">
          <button
            type="submit"
            className={`${actionButtonClass} border-0 bg-accent font-semibold text-on-accent`}
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Saving…' : 'Save'}
          </button>
          <button
            type="button"
            className={`${actionButtonClass} border border-border bg-surface font-medium text-text`}
            onClick={onClose}
            disabled={mutation.isPending}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
