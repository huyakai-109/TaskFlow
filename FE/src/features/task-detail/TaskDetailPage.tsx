import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { describeError, describeErrorCode } from '../../api/errorMessages'
import type { TaskPriority, TaskStatus } from '../../api/types'
import { AssignAndMoveDialog } from '../../shared/components/AssignAndMoveDialog'
import { MoveRefusalDialog } from '../../shared/components/MoveRefusalDialog'
import { MoveToast } from '../../shared/components/MoveToast'
import { useAssignAndMoveTaskMutation } from '../../shared/hooks/useAssignAndMoveTaskMutation'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'
import { STATUS_LABELS } from '../../shared/lib/statusLabels'

import { AssigneePickerDialog } from './components/AssigneePickerDialog'
import { EditTaskDialog } from './components/EditTaskDialog'
import { HistoryTimeline } from './components/HistoryTimeline'
import { PropertiesPanel } from './components/PropertiesPanel'
import { ReopenDialog } from './components/ReopenDialog'
import { WorkflowStrip } from './components/WorkflowStrip'
import { useAssignTaskMutation } from './hooks/useAssignTaskMutation'
import { useReopenTaskMutation } from './hooks/useReopenTaskMutation'
import { useTaskHistoryQuery } from './hooks/useTaskHistoryQuery'
import { useTaskQuery } from './hooks/useTaskQuery'
import { useUpdateDueDateMutation } from './hooks/useUpdateDueDateMutation'
import { useUpdatePriorityMutation } from './hooks/useUpdatePriorityMutation'

const noteClass = 'text-[length:var(--font-size-sm)] text-text-muted'
const errorBoxClass =
  'flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger'

const CONFLICT_CODES = new Set(['Task.Move.Conflict', 'Task.Move.NotFound'])

/** `Conflict`/`NotFound` codes across the properties-panel operations get a toast, same as a drag refusal; anything else is a field-level message. */
function isConflictLike(code: string | null): boolean {
  return code !== null && (code.endsWith('.Conflict') || code.endsWith('.NotFound'))
}

/**
 * The task detail screen (`docs/ui/screens/TaskDetail.dc.html`): workflow
 * strip with every legal move plus the Manager/Admin-only reopen action,
 * read-only activity timeline (F04), and the properties panel. Routed at
 * `/tasks/:id`, reachable from a board card's key or title (`TaskCard`).
 */
export function TaskDetailPage() {
  const { id } = useParams<{ id: string }>()
  const taskId = Number(id)
  const taskQuery = useTaskQuery(taskId)
  const historyQuery = useTaskHistoryQuery(taskId)

  const [refusal, setRefusal] = useState<{ code: string | null; taskTitle: string; message: string | null } | null>(
    null,
  )
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [reopenOpen, setReopenOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [assigneePickerOpen, setAssigneePickerOpen] = useState(false)
  const [priorityError, setPriorityError] = useState<string | null>(null)
  const [dueDateError, setDueDateError] = useState<string | null>(null)
  const [assignErrorCode, setAssignErrorCode] = useState<string | null>(null)
  const [assignErrorMessage, setAssignErrorMessage] = useState<string | null>(null)
  const [assignAndMove, setAssignAndMove] = useState<{ toStatus: TaskStatus; version: string } | null>(null)
  const [assignAndMoveErrorCode, setAssignAndMoveErrorCode] = useState<string | null>(null)
  const [assignAndMoveErrorMessage, setAssignAndMoveErrorMessage] = useState<string | null>(null)

  const moveMutation = useMoveTaskMutation({
    onRefused: (code, variables, message) => {
      if (code !== null && CONFLICT_CODES.has(code)) {
        setToastMessage(describeErrorCode(code, message))
        return
      }

      if (code === 'Task.Move.AssigneeRequired') {
        setAssignAndMoveErrorCode(null)
        setAssignAndMoveErrorMessage(null)
        setAssignAndMove({ toStatus: variables.toStatus, version: variables.version })
        return
      }

      setRefusal({ code, taskTitle: variables.taskTitle, message })
    },
  })

  const assignAndMoveMutation = useAssignAndMoveTaskMutation({
    onRefused: (failure) => {
      if (failure.step === 'move') {
        setAssignAndMove(null)
        setToastMessage(describeErrorCode(failure.code, failure.message))
        return
      }

      setAssignAndMoveErrorCode(failure.code)
      setAssignAndMoveErrorMessage(failure.message)
    },
  })

  const reopenMutation = useReopenTaskMutation({
    onRefused: (code) => setToastMessage(describeErrorCode(code)),
  })

  const priorityMutation = useUpdatePriorityMutation({
    onRefused: (code) => {
      if (isConflictLike(code)) {
        setToastMessage(describeErrorCode(code))
        return
      }

      setPriorityError(describeErrorCode(code))
    },
  })

  const dueDateMutation = useUpdateDueDateMutation({
    onRefused: (code) => {
      if (isConflictLike(code)) {
        setToastMessage(describeErrorCode(code))
        return
      }

      setDueDateError(describeErrorCode(code))
    },
  })

  const assignMutation = useAssignTaskMutation({
    onRefused: (code, message) => {
      if (isConflictLike(code)) {
        setToastMessage(describeErrorCode(code, message))
        setAssigneePickerOpen(false)
        return
      }

      setAssignErrorCode(code)
      setAssignErrorMessage(message)
    },
  })

  if (taskQuery.isPending) {
    return (
      <p className={noteClass} role="status">
        Loading task…
      </p>
    )
  }

  if (taskQuery.isError) {
    return (
      <div className={errorBoxClass} role="alert">
        <p>{describeError(taskQuery.error)}</p>
        <Link to="/board">Back to board</Link>
      </div>
    )
  }

  const task = taskQuery.data
  const moves = task.allowedTransitions.filter((t) => t.operation === 'Move')
  const reopen = task.allowedTransitions.find((t) => t.operation === 'Reopen')

  function handlePriorityChange(priority: TaskPriority) {
    setPriorityError(null)
    priorityMutation.mutate({ taskId: task.id, priority, version: task.version })
  }

  function handleDueDateChange(dueDate: string | null) {
    setDueDateError(null)
    dueDateMutation.mutate({ taskId: task.id, dueDate, version: task.version })
  }

  function handleAssign(assigneeId: number | null) {
    setAssignErrorCode(null)
    setAssignErrorMessage(null)
    assignMutation.mutate(
      { taskId: task.id, assigneeId, version: task.version },
      { onSuccess: () => setAssigneePickerOpen(false) },
    )
  }

  return (
    <section className="flex max-w-[1100px] flex-col gap-[var(--space-5)]">
      <Link to="/board" className="self-start text-[length:var(--font-size-sm)] text-text-muted no-underline hover:underline">
        ← Board
      </Link>

      <div className="flex flex-col gap-[var(--space-3)]">
        <div className="flex flex-wrap items-center gap-[var(--space-3)]">
          <h1 className="m-0 text-[1.875rem] font-semibold tracking-[-0.02em]">{task.title}</h1>
          <button
            type="button"
            className="min-h-9 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] font-medium text-text hover:bg-accent-soft"
            onClick={() => setEditOpen(true)}
          >
            Edit
          </button>
        </div>
        {task.description !== null && task.description !== '' && (
          <p className="max-w-[640px] text-text-muted [line-height:1.6]">{task.description}</p>
        )}
      </div>

      <div className="flex flex-wrap items-start gap-[var(--space-5)]">
        <main className="flex min-w-[320px] flex-[999_1_540px] flex-col gap-[var(--space-5)]">
          <WorkflowStrip
            status={task.status}
            moves={moves}
            reopen={reopen}
            onMove={(toStatus) =>
              moveMutation.mutate({ taskId: task.id, toStatus, version: task.version, taskTitle: task.title })
            }
            onReopenRequested={() => setReopenOpen(true)}
          />

          <section aria-labelledby="activity-heading" className="flex flex-col gap-[var(--space-4)]">
            <div className="flex flex-wrap items-center justify-between gap-[var(--space-2)]">
              <h2 id="activity-heading" className="text-[length:var(--font-size-lg)] font-semibold">
                Activity
              </h2>
              <span className="text-[length:var(--font-size-xs)] text-text-muted">
                Permanent — entries can&rsquo;t be edited or deleted
              </span>
            </div>
            {historyQuery.isPending && (
              <p role="status" className={noteClass}>
                Loading history…
              </p>
            )}
            {historyQuery.isError && (
              <p role="alert" className={noteClass}>
                {describeError(historyQuery.error)}
              </p>
            )}
            {historyQuery.isSuccess && <HistoryTimeline entries={historyQuery.data} />}
          </section>
        </main>

        <PropertiesPanel
          task={task}
          onEditPriority={handlePriorityChange}
          isPriorityPending={priorityMutation.isPending}
          priorityError={priorityError}
          onEditDueDate={handleDueDateChange}
          isDueDatePending={dueDateMutation.isPending}
          dueDateError={dueDateError}
          onOpenAssigneePicker={() => setAssigneePickerOpen(true)}
          isAssigneePending={assignMutation.isPending}
        />
      </div>

      {reopenOpen && (
        <ReopenDialog
          taskTitle={task.title}
          isPending={reopenMutation.isPending}
          onCancel={() => setReopenOpen(false)}
          onSubmit={(reason) => {
            reopenMutation.mutate(
              { taskId: task.id, reason, version: task.version },
              { onSuccess: () => setReopenOpen(false) },
            )
          }}
        />
      )}

      {editOpen && (
        <EditTaskDialog
          task={task}
          onClose={() => setEditOpen(false)}
          onRefused={(code) => {
            if (isConflictLike(code)) {
              setToastMessage(describeErrorCode(code))
              setEditOpen(false)
            }
          }}
        />
      )}

      {assigneePickerOpen && (
        <AssigneePickerDialog
          currentAssigneeId={task.assignee?.id ?? null}
          isPending={assignMutation.isPending}
          errorCode={assignErrorCode}
          errorMessage={assignErrorMessage}
          onClose={() => setAssigneePickerOpen(false)}
          onSelect={handleAssign}
        />
      )}

      {assignAndMove !== null && (
        <AssignAndMoveDialog
          taskTitle={task.title}
          toStatusLabel={STATUS_LABELS[assignAndMove.toStatus]}
          isPending={assignAndMoveMutation.isPending}
          errorCode={assignAndMoveErrorCode}
          errorMessage={assignAndMoveErrorMessage}
          onClose={() => setAssignAndMove(null)}
          onSelect={(assigneeId) =>
            assignAndMoveMutation.mutate(
              { taskId: task.id, toStatus: assignAndMove.toStatus, assigneeId, version: assignAndMove.version },
              { onSuccess: () => setAssignAndMove(null) },
            )
          }
        />
      )}

      {refusal !== null && (
        <MoveRefusalDialog
          code={refusal.code}
          taskTitle={refusal.taskTitle}
          message={refusal.message}
          onClose={() => setRefusal(null)}
        />
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}
    </section>
  )
}
