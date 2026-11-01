import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { describeError, describeErrorCode } from '../../api/errorMessages'
import type { TaskPriority } from '../../api/types'
import { MoveRefusalDialog } from '../../shared/components/MoveRefusalDialog'
import { MoveToast } from '../../shared/components/MoveToast'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'

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
import styles from './TaskDetailPage.module.css'

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

  const [refusal, setRefusal] = useState<{ code: string | null; taskTitle: string } | null>(null)
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [reopenOpen, setReopenOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [assigneePickerOpen, setAssigneePickerOpen] = useState(false)
  const [priorityError, setPriorityError] = useState<string | null>(null)
  const [dueDateError, setDueDateError] = useState<string | null>(null)
  const [assignErrorCode, setAssignErrorCode] = useState<string | null>(null)

  const moveMutation = useMoveTaskMutation({
    onRefused: (code, taskTitle) => {
      if (code !== null && CONFLICT_CODES.has(code)) {
        setToastMessage(describeErrorCode(code))
        return
      }

      setRefusal({ code, taskTitle })
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
    onRefused: (code) => {
      if (isConflictLike(code)) {
        setToastMessage(describeErrorCode(code))
        setAssigneePickerOpen(false)
        return
      }

      setAssignErrorCode(code)
    },
  })

  if (taskQuery.isPending) {
    return (
      <p className={styles.note} role="status">
        Loading task…
      </p>
    )
  }

  if (taskQuery.isError) {
    return (
      <div className={styles.errorBox} role="alert">
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
    assignMutation.mutate(
      { taskId: task.id, assigneeId, version: task.version },
      { onSuccess: () => setAssigneePickerOpen(false) },
    )
  }

  return (
    <section className={styles.page}>
      <Link to="/board" className={styles.back}>
        ← Board
      </Link>

      <div className={styles.header}>
        <div className={styles.headerRow}>
          <h1 className={styles.title}>{task.title}</h1>
          <button type="button" className={styles.editButton} onClick={() => setEditOpen(true)}>
            Edit
          </button>
        </div>
        {task.description !== null && task.description !== '' && (
          <p className={styles.description}>{task.description}</p>
        )}
      </div>

      <div className={styles.body}>
        <main className={styles.main}>
          <WorkflowStrip
            status={task.status}
            moves={moves}
            reopen={reopen}
            onMove={(toStatus) =>
              moveMutation.mutate({ taskId: task.id, toStatus, version: task.version, taskTitle: task.title })
            }
            onReopenRequested={() => setReopenOpen(true)}
          />

          <section aria-labelledby="activity-heading" className={styles.activity}>
            <div className={styles.activityHeader}>
              <h2 id="activity-heading" className={styles.sectionTitle}>
                Activity
              </h2>
              <span className={styles.permanentNote}>Permanent — entries can&rsquo;t be edited or deleted</span>
            </div>
            {historyQuery.isPending && (
              <p role="status" className={styles.note}>
                Loading history…
              </p>
            )}
            {historyQuery.isError && (
              <p role="alert" className={styles.note}>
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
          onClose={() => setAssigneePickerOpen(false)}
          onSelect={handleAssign}
        />
      )}

      {refusal !== null && (
        <MoveRefusalDialog code={refusal.code} taskTitle={refusal.taskTitle} onClose={() => setRefusal(null)} />
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}
    </section>
  )
}
