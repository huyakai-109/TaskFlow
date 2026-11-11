import { DndContext, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import clsx from 'clsx'
import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'

import { BOARD_COLUMNS } from '../../api/types'
import type { TaskStatus, TaskSummary } from '../../api/types'
import { describeError, describeErrorCode } from '../../api/errorMessages'
import { AssignAndMoveDialog } from '../../shared/components/AssignAndMoveDialog'
import { MoveRefusalDialog } from '../../shared/components/MoveRefusalDialog'
import { MoveToast } from '../../shared/components/MoveToast'
import { useAssignAndMoveTaskMutation } from '../../shared/hooks/useAssignAndMoveTaskMutation'
import { useBoardQuery } from '../../shared/hooks/useBoardQuery'
import { useMoveTaskMutation } from '../../shared/hooks/useMoveTaskMutation'
import { useOverdueTasksQuery } from '../../shared/hooks/useOverdueTasksQuery'
import { STATUS_LABELS } from '../../shared/lib/statusLabels'

import { BoardColumn } from './components/BoardColumn'
import { BoardListView } from './components/BoardListView'
import { BoardSearch } from './components/BoardSearch'
import { NewTaskDialog } from './components/NewTaskDialog'
import { useColumnPagesQuery } from './hooks/useColumnPagesQuery'
import { mergeColumnItems } from './utils/mergeColumnItems'

type BoardView = 'board' | 'list'

/** Typing `c` while focused in a field must never pop a dialog. */
function isTypingTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)
}

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
  const [view, setView] = useState<BoardView>('board')
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
  const anyDialogOpen = refusal !== null || createOpen || assignAndMove !== null

  // `C` new task (`docs/design/SPEC.md` §3/§6 point 7, built 2026-11-11 — SPEC §10
  // point 5). Scoped to the Board page, where the New task dialog and its
  // mutation already live; never fires while typing in a field or while
  // another dialog is already open.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() !== 'c' || event.metaKey || event.ctrlKey || event.altKey) {
        return
      }

      if (isTypingTarget(event.target) || anyDialogOpen) {
        return
      }

      event.preventDefault()
      setCreateOpen(true)
    }

    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [anyDialogOpen])

  return (
    <section>
      {/*
       * `docs/design/SPEC.md` §5.1 toolbar, trimmed per §10 Gate A: no List
       * segment (view doesn't exist — a dead segment is worse than none), no
       * "N at capacity" pill or avatar stack/Team-capacity popover (per-user
       * capacity counts are deferred BE work, point 3).
       */}
      <div className="flex flex-wrap items-center gap-[var(--space-3)]">
        <h1 className="sr-only">Board</h1>
        <div
          role="tablist"
          aria-label="Board view"
          className="inline-flex min-h-9 items-center gap-[2px] rounded-control-lg bg-column p-[2px]"
        >
          <button
            type="button"
            role="tab"
            aria-selected={view === 'board'}
            className={clsx(
              'min-h-8 cursor-pointer rounded-control border-0 px-[var(--space-3)] text-[length:var(--font-size-sm)]',
              view === 'board' ? 'bg-surface font-semibold text-text shadow-card' : 'bg-transparent text-text-muted',
            )}
            onClick={() => setView('board')}
          >
            Board
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={view === 'list'}
            className={clsx(
              'min-h-8 cursor-pointer rounded-control border-0 px-[var(--space-3)] text-[length:var(--font-size-sm)]',
              view === 'list' ? 'bg-surface font-semibold text-text shadow-card' : 'bg-transparent text-text-muted',
            )}
            onClick={() => setView('list')}
          >
            List
          </button>
        </div>
        <span
          className="inline-flex min-h-9 items-center gap-[var(--space-1)] rounded-control-lg border border-border px-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted"
          title="Urgent → High → Normal, then earliest due date. Tasks with no date go last."
        >
          <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <path d="M7 4v16M7 4 4 7M7 4l3 3M17 20V4M17 20l-3-3M17 20l3-3" />
          </svg>
          Sort: <strong className="ml-[var(--space-1)] font-semibold text-text">Priority</strong>
        </span>
        <div className="ml-auto flex items-center gap-[var(--space-3)] max-[600px]:ml-0 max-[600px]:w-full max-[600px]:flex-wrap">
          <BoardSearch tasks={BOARD_COLUMNS.flatMap((status) => mergedItemsFor(status))} />
          {overdueCount > 0 && (
            <Link
              to="/overdue"
              className="inline-flex min-h-9 items-center gap-[var(--space-1)] rounded-pill bg-overdue-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-semibold text-overdue no-underline hover:underline"
            >
              <svg aria-hidden="true" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3 2" />
              </svg>
              {overdueCount} overdue
            </Link>
          )}
          <button
            type="button"
            className="inline-flex min-h-10 cursor-pointer items-center gap-[var(--space-2)] rounded-control-lg border-0 bg-accent px-[var(--space-4)] font-semibold text-on-accent hover:bg-accent-hover"
            onClick={() => setCreateOpen(true)}
          >
            New task
            <kbd
              aria-hidden="true"
              className="rounded-[5px] bg-[rgba(255,255,255,0.18)] px-[6px] py-[1px] font-mono text-[length:var(--font-size-2xs)] font-normal"
            >
              C
            </kbd>
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

      {boardQuery.isSuccess && view === 'board' && (
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

      {boardQuery.isSuccess && view === 'list' && (
        <BoardListView
          itemsByStatus={Object.fromEntries(BOARD_COLUMNS.map((status) => [status, mergedItemsFor(status)])) as Record<TaskStatus, TaskSummary[]>}
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
