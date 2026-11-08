import { DndContext, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'

import { BOARD_COLUMNS } from '../../api/types'
import type { TaskStatus, TaskSummary } from '../../api/types'
import { describeError, describeErrorCode } from '../../api/errorMessages'
import { AssignAndMoveDialog } from '../../shared/components/AssignAndMoveDialog'
import { MoveRefusalDialog } from '../../shared/components/MoveRefusalDialog'
import { MoveToast } from '../../shared/components/MoveToast'
import { useAssignAndMoveTaskMutation } from '../../shared/hooks/useAssignAndMoveTaskMutation'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'
import { useOverdueTasksQuery } from '../../shared/hooks/useOverdueTasksQuery'
import { STATUS_LABELS } from '../../shared/lib/statusLabels'

import { BoardColumn } from './components/BoardColumn'
import { NewTaskDialog } from './components/NewTaskDialog'
import { useBoardQuery } from './hooks/useBoardQuery'
import { useColumnPagesQuery } from './hooks/useColumnPagesQuery'
import { mergeColumnItems } from './utils/mergeColumnItems'

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
 * the non-drag path is never a lesser one (`docs/design/SPEC.md` "Keep a non-drag
 * path"). A column not in the dragged card's own `allowedTransitions` is a
 * genuinely disabled drop target, not just a dimmed one (`BoardColumn`).
 *
 * The sidebar, search, team-capacity popover and "N at capacity" chip from the
 * wireframe are left out here: they need F02 (capacity), a user roster or
 * routes this slice does not build (`docs/design/SPEC.md`'s MVP scope note). The
 * "N overdue" chip stays and links to `/overdue` (F05 T9); its count comes
 * from `useOverdueTasksQuery`'s `pagination.totalItems` — the same source of
 * truth as the Overdue view — rather than counting only the loaded page of
 * each column, a pre-existing defect this slice fixes.
 */
export function BoardPage() {
  const navigate = useNavigate()
  const boardQuery = useBoardQuery()
  const overdueTasksQuery = useOverdueTasksQuery()
  const [activeTask, setActiveTask] = useState<TaskSummary | null>(null)
  const [refusal, setRefusal] = useState<{ code: string | null; taskTitle: string; message: string | null } | null>(
    null,
  )
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [assignAndMove, setAssignAndMove] = useState<{
    taskId: number
    toStatus: TaskStatus
    taskTitle: string
    version: string
  } | null>(null)
  const [assignAndMoveErrorCode, setAssignAndMoveErrorCode] = useState<string | null>(null)
  const [assignAndMoveErrorMessage, setAssignAndMoveErrorMessage] = useState<string | null>(null)

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }))

  // Tracks which columns the user has expanded via "show more" at least
  // once. Before that, the column's extra-pages query stays disabled so it
  // is never auto-fetched just because the board re-rendered or refreshed;
  // once expanded, it stays enabled for the rest of the session so it
  // participates in every later `invalidateQueries(['board'])` refetch.
  const [expandedColumns, setExpandedColumns] = useState<Record<TaskStatus, boolean>>({
    Backlog: false,
    Todo: false,
    InProgress: false,
    Review: false,
    Done: false,
  })

  function pageSizeFor(status: TaskStatus): number | undefined {
    return boardQuery.data?.columns.find((c) => c.status === status)?.pagination?.pageSize
  }

  // One `useInfiniteQuery` per column, called unconditionally for all five
  // fixed statuses (never in a loop) — `BOARD_COLUMNS` never changes shape.
  const columnPages = {
    Backlog: useColumnPagesQuery('Backlog', pageSizeFor('Backlog'), expandedColumns.Backlog),
    Todo: useColumnPagesQuery('Todo', pageSizeFor('Todo'), expandedColumns.Todo),
    InProgress: useColumnPagesQuery('InProgress', pageSizeFor('InProgress'), expandedColumns.InProgress),
    Review: useColumnPagesQuery('Review', pageSizeFor('Review'), expandedColumns.Review),
    Done: useColumnPagesQuery('Done', pageSizeFor('Done'), expandedColumns.Done),
  } as const satisfies Record<TaskStatus, ReturnType<typeof useColumnPagesQuery>>

  function loadMore(status: TaskStatus) {
    setExpandedColumns((prev) => (prev[status] ? prev : { ...prev, [status]: true }))
    void columnPages[status].fetchNextPage()
  }

  function mergedItemsFor(status: TaskStatus): TaskSummary[] {
    const column = boardQuery.data?.columns.find((c) => c.status === status)
    return mergeColumnItems(column?.items ?? [], columnPages[status].data?.pages)
  }

  const moveMutation = useMoveTaskMutation({
    onRefused: (code, variables, message) => {
      if (code !== null && CONFLICT_CODES.has(code)) {
        setToastMessage(describeErrorCode(code, message))
        return
      }

      if (code === 'Task.Move.AssigneeRequired') {
        setAssignAndMoveErrorCode(null)
        setAssignAndMoveErrorMessage(null)
        setAssignAndMove({
          taskId: variables.taskId,
          toStatus: variables.toStatus,
          taskTitle: variables.taskTitle,
          version: variables.version,
        })
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

  /**
   * Searches board page 1 *and* any extra pages already loaded via "show
   * more" (F03) — otherwise a page-2+ card could not be dragged at all.
   */
  function findTask(taskId: number): TaskSummary | undefined {
    for (const status of BOARD_COLUMNS) {
      const found = mergedItemsFor(status).find((item) => item.id === taskId)

      if (found !== undefined) {
        return found
      }
    }

    return undefined
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

  const overdueCount = overdueTasksQuery.data?.pages.at(-1)?.pagination?.totalItems ?? 0

  return (
    <section>
      <div className="flex flex-wrap items-center gap-[var(--space-3)]">
        <h2 className="text-[length:var(--font-size-xl)] font-semibold">Board</h2>
        <span
          className="text-[length:var(--font-size-sm)] text-text-muted"
          title="Urgent → High → Normal, then earliest due date. Tasks with no date go last."
        >
          Sort: <strong>Priority</strong>
        </span>
        <div className="ml-auto flex items-center gap-[var(--space-3)] max-[600px]:ml-0">
          {overdueCount > 0 && (
            <Link
              to="/overdue"
              className="inline-flex items-center rounded-pill bg-overdue-surface px-[var(--space-3)] py-[2px] text-[length:var(--font-size-sm)] font-semibold text-overdue no-underline hover:underline"
            >
              {overdueCount} overdue
            </Link>
          )}
          <button
            type="button"
            className="min-h-10 cursor-pointer rounded-md border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover"
            onClick={() => setCreateOpen(true)}
          >
            New task
          </button>
        </div>
      </div>

      {boardQuery.isPending && (
        <p className="mt-[var(--space-5)] text-[length:var(--font-size-sm)] text-text-muted" role="status">
          Loading board…
        </p>
      )}

      {boardQuery.isError && (
        <div
          className="mt-[var(--space-5)] flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger"
          role="alert"
        >
          <p>{describeError(boardQuery.error)}</p>
          <button type="button" onClick={() => boardQuery.refetch()}>
            Try again
          </button>
        </div>
      )}

      {boardQuery.isSuccess && (
        <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd} onDragCancel={() => setActiveTask(null)}>
          <div className="mt-[var(--space-5)] grid grid-flow-col auto-cols-[minmax(232px,1fr)] items-start gap-[var(--space-4)] overflow-x-auto max-[600px]:auto-cols-[85vw]">
            {BOARD_COLUMNS.map((status) => {
              const column = boardQuery.data.columns.find((c) => c.status === status)
              const items = mergedItemsFor(status)
              const pages = columnPages[status]

              return (
                <BoardColumn
                  key={status}
                  status={status}
                  label={STATUS_LABELS[status]}
                  icon={<StatusIcon status={status} />}
                  items={items}
                  pagination={column?.pagination ?? null}
                  onLoadMore={() => loadMore(status)}
                  isLoadingMore={pages.isFetchingNextPage}
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
        <MoveRefusalDialog
          code={refusal.code}
          taskTitle={refusal.taskTitle}
          message={refusal.message}
          onClose={() => setRefusal(null)}
        />
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}

      {assignAndMove !== null && (
        <AssignAndMoveDialog
          taskTitle={assignAndMove.taskTitle}
          toStatusLabel={STATUS_LABELS[assignAndMove.toStatus]}
          isPending={assignAndMoveMutation.isPending}
          errorCode={assignAndMoveErrorCode}
          errorMessage={assignAndMoveErrorMessage}
          onClose={() => setAssignAndMove(null)}
          onSelect={(assigneeId) =>
            assignAndMoveMutation.mutate(
              {
                taskId: assignAndMove.taskId,
                toStatus: assignAndMove.toStatus,
                assigneeId,
                version: assignAndMove.version,
              },
              { onSuccess: () => setAssignAndMove(null) },
            )
          }
        />
      )}

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
