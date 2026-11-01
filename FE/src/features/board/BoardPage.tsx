import { DndContext, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import { BOARD_COLUMNS } from '../../api/types'
import type { TaskStatus, TaskSummary } from '../../api/types'
import { describeError, describeErrorCode } from '../../api/errorMessages'
import { MoveRefusalDialog } from '../../shared/components/MoveRefusalDialog'
import { MoveToast } from '../../shared/components/MoveToast'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'
import { STATUS_LABELS } from '../../shared/lib/statusLabels'

import { BoardColumn } from './components/BoardColumn'
import { NewTaskDialog } from './components/NewTaskDialog'
import { useBoardQuery } from './hooks/useBoardQuery'
import styles from './BoardPage.module.css'

function StatusIcon({ status }: { status: TaskStatus }) {
  switch (status) {
    case 'Backlog':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeDasharray="2.2 2.2">
          <circle cx="8" cy="8" r="6" />
        </svg>
      )
    case 'Todo':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6">
          <circle cx="8" cy="8" r="6" />
        </svg>
      )
    case 'InProgress':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16">
          <circle cx="8" cy="8" r="6" fill="none" stroke="var(--color-accent)" strokeWidth="1.6" />
          <path d="M8 4a4 4 0 0 1 0 8z" fill="var(--color-accent)" />
        </svg>
      )
    case 'Review':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16">
          <circle cx="8" cy="8" r="6" fill="none" stroke="var(--color-priority-high)" strokeWidth="1.6" />
          <path d="M8 2a6 6 0 1 1-6 6h6z" fill="var(--color-priority-high)" />
        </svg>
      )
    case 'Done':
      return (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 16 16">
          <circle cx="8" cy="8" r="7" fill="var(--color-success)" />
          <path d="m5 8.2 2 2 4-4.2" fill="none" stroke="var(--color-surface)" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )
  }
}

/** `event.active.id` is `task-{id}`, so it never collides with a column's status id. */
function parseDraggedTaskId(dragId: string | number): number {
  return Number(String(dragId).replace('task-', ''))
}

const CONFLICT_CODES = new Set(['Task.Move.Conflict', 'Task.Move.NotFound'])

/**
 * The board: five fixed columns, drag-and-drop between them (F01 D9, D10).
 * Every move available by drag is also a button on the card (`TaskCard`), so
 * the non-drag path is never a lesser one (`docs/ui.md` "Keep a non-drag
 * path"). A column not in the dragged card's own `allowedTransitions` is a
 * genuinely disabled drop target, not just a dimmed one (`BoardColumn`).
 *
 * The sidebar, search, team-capacity popover and "N at capacity" chip from the
 * wireframe are left out here: they need F02 (capacity), a user roster or
 * routes this slice does not build (`docs/ui.md`'s MVP scope note). The
 * "N overdue" chip stays, because `isOverdue` is already on every card F01
 * returns today.
 */
export function BoardPage() {
  const navigate = useNavigate()
  const boardQuery = useBoardQuery()
  const [activeTask, setActiveTask] = useState<TaskSummary | null>(null)
  const [refusal, setRefusal] = useState<{ code: string | null; taskTitle: string } | null>(null)
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }))

  const moveMutation = useMoveTaskMutation({
    onRefused: (code, taskTitle) => {
      if (code !== null && CONFLICT_CODES.has(code)) {
        setToastMessage(describeErrorCode(code))
        return
      }

      setRefusal({ code, taskTitle })
    },
  })

  function findTask(taskId: number): TaskSummary | undefined {
    return boardQuery.data?.columns.flatMap((column) => column.items).find((item) => item.id === taskId)
  }

  function handleDragStart(event: DragStartEvent) {
    setActiveTask(findTask(parseDraggedTaskId(event.active.id)) ?? null)
  }

  function handleDragEnd(event: DragEndEvent) {
    const task = findTask(parseDraggedTaskId(event.active.id))
    const toStatus = event.over?.id as TaskStatus | undefined
    setActiveTask(null)

    if (task === undefined || toStatus === undefined || toStatus === task.status) {
      return
    }

    const isAllowed = task.allowedTransitions.some((t) => t.operation === 'Move' && t.status === toStatus)

    if (!isAllowed) {
      return
    }

    moveMutation.mutate({ taskId: task.id, toStatus, version: task.version, taskTitle: task.title })
  }

  const allowedTargetsForActiveDrag = useMemo(
    () => new Set(activeTask?.allowedTransitions.filter((t) => t.operation === 'Move').map((t) => t.status) ?? []),
    [activeTask],
  )

  const overdueCount =
    boardQuery.data?.columns.reduce((total, column) => total + column.items.filter((item) => item.isOverdue).length, 0) ?? 0

  return (
    <section>
      <div className={styles.toolbar}>
        <h2 className={styles.heading}>Board</h2>
        <span
          className={styles.sortInfo}
          title="Urgent → High → Normal, then earliest due date. Tasks with no date go last."
        >
          Sort: <strong>Priority</strong>
        </span>
        <div className={styles.rightGroup}>
          {overdueCount > 0 && <span className={styles.overdueChip}>{overdueCount} overdue</span>}
          <button type="button" className={styles.newTaskButton} onClick={() => setCreateOpen(true)}>
            New task
          </button>
        </div>
      </div>

      {boardQuery.isPending && (
        <p className={styles.note} role="status">
          Loading board…
        </p>
      )}

      {boardQuery.isError && (
        <div className={styles.errorBox} role="alert">
          <p>{describeError(boardQuery.error)}</p>
          <button type="button" onClick={() => boardQuery.refetch()}>
            Try again
          </button>
        </div>
      )}

      {boardQuery.isSuccess && (
        <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd} onDragCancel={() => setActiveTask(null)}>
          <div className={styles.columns}>
            {BOARD_COLUMNS.map((status) => {
              const column = boardQuery.data.columns.find((c) => c.status === status)
              const items = column?.items ?? []

              return (
                <BoardColumn
                  key={status}
                  status={status}
                  label={STATUS_LABELS[status]}
                  icon={<StatusIcon status={status} />}
                  items={items}
                  isDragging={activeTask !== null}
                  isDropTarget={allowedTargetsForActiveDrag.has(status)}
                  onMove={(taskId, toStatus, version) => {
                    const task = findTask(taskId)
                    moveMutation.mutate({ taskId, toStatus, version, taskTitle: task?.title ?? `#${taskId}` })
                  }}
                />
              )
            })}
          </div>
        </DndContext>
      )}

      {refusal !== null && (
        <MoveRefusalDialog code={refusal.code} taskTitle={refusal.taskTitle} onClose={() => setRefusal(null)} />
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}

      {createOpen && (
        <NewTaskDialog
          onClose={() => setCreateOpen(false)}
          onCreated={(taskId) => {
            setCreateOpen(false)
            navigate(`/tasks/${taskId}`)
          }}
        />
      )}
    </section>
  )
}
