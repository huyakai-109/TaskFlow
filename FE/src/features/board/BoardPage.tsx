import { DndContext, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import clsx from 'clsx'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

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
import { ViewingNowPlaceholder } from './components/ViewingNowPlaceholder'
import { useColumnPagesQuery } from './hooks/useColumnPagesQuery'
import { mergeColumnItems } from './utils/mergeColumnItems'

type BoardView = 'board' | 'list'

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
  const boardQuery = useBoardQuery()
  const overdueTasksQuery = useOverdueTasksQuery()
  const [activeTask, setActiveTask] = useState<TaskSummary | null>(null)
  const [refusal, setRefusal] = useState<{ code: string | null; taskTitle: string; message: string | null } | null>(
    null,
  )
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [view, setView] = useState<BoardView>('board')
  // Below 640px the board shows one status at a time, chosen from a pinned
  // tab bar instead of a horizontally-scrolling row of columns (layout fix,
  // 2026-11-12 again). Desktop ignores this entirely (CSS-only hidden).
  const [mobileStatus, setMobileStatus] = useState<TaskStatus>('Backlog')
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
    <section className="flex h-full min-h-0 flex-col gap-[var(--space-3)]">
      {/*
       * `docs/design/SPEC.md` §5.1 toolbar. List segment and search were
       * reopened and built 2026-11-11 (SPEC.md §10 point 5 superseded,
       * `docs/design/COMING-SOON.md` A3) — search moved to the shared
       * `Topbar` (layout fix) so it works from every page, not only here.
       * The "N at capacity" pill and the avatar stack/Team popover built the
       * same day were removed 2026-11-13 — capacity already has its own
       * page (Team capacity), so showing it a second time here was
       * redundant; `ViewingNowPlaceholder` takes that slot now, see its own
       * doc comment. `shrink-0`: this row stays put above the scrolling
       * board/list area below it (Jira-style layout fix, 2026-11-12) —
       * `<main>` itself never scrolls on this page, only the area under this
       * toolbar does.
       */}
      <div className="flex shrink-0 flex-wrap items-center gap-[var(--space-3)]">
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
          <span aria-hidden="true" className="h-6 w-px bg-border" />
          <ViewingNowPlaceholder />
        </div>
      </div>

      {/*
       * Below 640px the board shows one status at a time instead of a row of
       * columns (layout fix, 2026-11-12 again) — switched here, or with the
       * arrow keys while this tab bar has focus; moving a task is the "Move
       * to …" button on the card (drag never was the only path — see
       * `TaskCard`'s own doc comment).
       */}
      {boardQuery.isSuccess && view === 'board' && (
        <div
          role="tablist"
          aria-label="Board status"
          className="hidden shrink-0 items-center gap-[2px] overflow-x-auto rounded-control-lg bg-column p-[2px] max-[639px]:flex"
          onKeyDown={(event) => {
            if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') {
              return
            }

            event.preventDefault()
            const index = BOARD_COLUMNS.indexOf(mobileStatus)
            const delta = event.key === 'ArrowRight' ? 1 : -1
            const next = BOARD_COLUMNS[(index + delta + BOARD_COLUMNS.length) % BOARD_COLUMNS.length]
            setMobileStatus(next)
          }}
        >
          {BOARD_COLUMNS.map((status) => {
            const column = boardQuery.data.columns.find((c) => c.status === status)
            const totalItems = column?.pagination?.totalItems ?? mergedItemsFor(status).length

            return (
              <button
                key={status}
                type="button"
                role="tab"
                aria-selected={mobileStatus === status}
                className={clsx(
                  'flex min-h-8 shrink-0 cursor-pointer items-center gap-[var(--space-1)] rounded-control border-0 px-[var(--space-2)] text-[length:var(--font-size-xs)]',
                  mobileStatus === status ? 'bg-surface font-semibold text-text shadow-card' : 'bg-transparent text-text-muted',
                )}
                onClick={() => setMobileStatus(status)}
              >
                <StatusIcon status={status} />
                <span className="max-w-[56px] truncate">{STATUS_LABELS[status]}</span>
                <span className="font-normal">{totalItems}</span>
              </button>
            )
          })}
        </div>
      )}

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
          {/*
           * `docs/design/SPEC.md` §5.1 — the board area: the one vertical
           * scroll region of this page (layout fix, 2026-11-12 again —
           * `overflow-x-hidden`, never a horizontal scrollbar: all five
           * columns are `grid-cols-5` `minmax(0,1fr)` tracks that shrink and
           * grow with the viewport instead of a fixed width). Capped at
           * ~420px per column and centred past ~2180px wide so columns don't
           * grow unreasonably large on an ultra-wide monitor. Below 640px,
           * `grid-cols-1` and only the status picked in the tab bar above
           * renders at all (`BoardColumn`'s `isMobileHidden`).
           */}
          <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden">
            <div className="mx-auto grid min-h-full max-w-[2180px] grid-cols-1 items-stretch gap-[clamp(10px,1.5vw,16px)] pb-[var(--space-1)] min-[640px]:grid-cols-5">
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
                    isMobileHidden={status !== mobileStatus}
                    onMove={(taskId, toStatus, version) => {
                      const task = findTask(taskId)
                      moveMutation.mutate({ taskId, toStatus, version, taskTitle: task?.title ?? `#${taskId}` })
                    }}
                  />
                )
              })}
            </div>
          </div>
        </DndContext>
      )}

      {boardQuery.isSuccess && view === 'list' && (
        <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden rounded-lg border border-border">
          <BoardListView
            itemsByStatus={Object.fromEntries(BOARD_COLUMNS.map((status) => [status, mergedItemsFor(status)])) as Record<TaskStatus, TaskSummary[]>}
            paginationByStatus={
              Object.fromEntries(
                BOARD_COLUMNS.map((status) => [status, boardQuery.data.columns.find((c) => c.status === status)?.pagination ?? null]),
              ) as Record<TaskStatus, (typeof boardQuery.data.columns)[number]['pagination']>
            }
            onLoadMore={loadMore}
            isLoadingMore={(status) => columnPages[status].isFetchingNextPage}
          />
        </div>
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
    </section>
  )
}
