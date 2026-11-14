import { useEffect, useRef, useState } from 'react'

import type { AllowedTransition, TaskStatus } from '../../api/types'
import { moveButtonLabel } from '../lib/moveButtonLabel'
import { STATUS_LABELS } from '../lib/statusLabels'

function ArrowIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 8h10M9 4l4 4-4 4" />
    </svg>
  )
}

interface CardMoveActionProps {
  taskId: number
  taskTitle: string
  taskStatus: TaskStatus
  moves: AllowedTransition[]
  onMove: (status: TaskStatus) => void
  /**
   * Revision R2 "Take over" (`task.canTakeOver`) — an extra, non-move entry
   * merged into the same single-button-or-menu list below, so the card's
   * one action control gains "Take over" without a second hand-rolled
   * popover. Absent by default: `MyTasksPage`'s "Ready for you to review"
   * rows (this component's other caller) never have `canTakeOver` true, so
   * they pass no `takeOver` prop and render exactly as before.
   */
  takeOver?: { onSelect: () => void }
}

interface ActionItem {
  key: string
  label: string
  ariaLabel: string
  onSelect: () => void
}

/**
 * The board card's one action control (`docs/design/SPEC.md` §4/§10 point
 * 11, layout fix 2026-11-12 again) — a small round "→" button that sits in
 * the card's own first row (between the key and the avatar), not an overlay
 * on top of the chips below it. One legal move: the button acts directly.
 * More than one: it opens a small menu, modelled on `AvatarStack`'s own
 * popover (click-outside + Esc) rather than inventing a second pattern, with
 * full arrow-key/Enter/Esc keyboard support (`role="menu"`/`"menuitem"`).
 * Rendered only when there is at least one move or a `takeOver` entry — a
 * task with neither shows no button at all, same as before.
 *
 * Moved from `features/board/components/` to `shared/components/` (second
 * use — My tasks §5.8's "Ready for you to review" rows need the same
 * multi-transition menu, not a second hand-rolled popover) — `shared/` may
 * not reach into a feature, and this control carries no board-specific
 * assumption to begin with.
 */
export function CardMoveAction({ taskId, taskTitle, taskStatus, moves, onMove, takeOver }: CardMoveActionProps) {
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([])

  const items: ActionItem[] = moves.map((move) => ({
    key: move.status,
    label: moveButtonLabel(taskStatus, move),
    ariaLabel: `Move #${taskId} to ${STATUS_LABELS[move.status]}`,
    onSelect: () => onMove(move.status),
  }))

  if (takeOver !== undefined) {
    items.push({ key: 'take-over', label: 'Take over', ariaLabel: `Take over #${taskId}`, onSelect: takeOver.onSelect })
  }

  useEffect(() => {
    if (!open) {
      return
    }

    function onPointerDown(event: PointerEvent) {
      if (event.target instanceof Node && containerRef.current?.contains(event.target) === false) {
        setOpen(false)
      }
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        setOpen(false)
        triggerRef.current?.focus()
        return
      }

      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault()
        setActiveIndex((index) => {
          const next = event.key === 'ArrowDown' ? (index + 1) % items.length : (index - 1 + items.length) % items.length
          itemRefs.current[next]?.focus()
          return next
        })
      }
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open, items.length])

  // Focus the first item once the menu's DOM exists; `activeIndex` itself is
  // set where `open` is set (the click that caused it), not here.
  useEffect(() => {
    if (open) {
      itemRefs.current[0]?.focus()
    }
  }, [open])

  if (items.length === 0) {
    return null
  }

  // Button/menu interaction must never also start a drag — distinct from
  // `GripIcon`, which is the only element `useDraggable`'s activator
  // listeners are actually bound to, but `stopPropagation` here keeps this
  // control safe even if that ever changes.
  function stopDragActivation(event: React.PointerEvent | React.MouseEvent) {
    event.stopPropagation()
  }

  if (items.length === 1) {
    const item = items[0]

    return (
      <button
        type="button"
        title={item.label}
        aria-label={item.ariaLabel}
        className="inline-flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-pill border border-border bg-surface text-text-muted opacity-0 [transition:opacity_var(--transition-fast)] group-hover:opacity-100 group-focus-within:opacity-100 hover:border-accent hover:bg-accent-soft hover:text-accent-ink [@media(hover:none)]:opacity-100 [@media(pointer:coarse)]:size-7"
        onPointerDown={stopDragActivation}
        onClick={(event) => {
          stopDragActivation(event)
          item.onSelect()
        }}
      >
        <ArrowIcon />
      </button>
    )
  }

  return (
    <div ref={containerRef} className="relative shrink-0">
      <button
        type="button"
        ref={triggerRef}
        title="Move to…"
        aria-label={`Move #${taskId} to…`}
        aria-haspopup="menu"
        aria-expanded={open}
        className="inline-flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-pill border border-border bg-surface text-text-muted opacity-0 [transition:opacity_var(--transition-fast)] group-hover:opacity-100 group-focus-within:opacity-100 hover:border-accent hover:bg-accent-soft hover:text-accent-ink [@media(hover:none)]:opacity-100 [@media(pointer:coarse)]:size-7"
        onPointerDown={stopDragActivation}
        onClick={(event) => {
          stopDragActivation(event)
          setActiveIndex(0)
          setOpen((value) => !value)
        }}
      >
        <ArrowIcon />
      </button>

      {open && (
        <ul
          role="menu"
          aria-label={`Move "${taskTitle}" to`}
          className="absolute top-[calc(100%+4px)] right-0 z-50 m-0 min-w-[160px] list-none rounded-md border border-border bg-surface-raised p-[var(--space-1)] shadow-raised"
        >
          {items.map((item, index) => (
            <li key={item.key} role="none">
              <button
                type="button"
                role="menuitem"
                ref={(el) => {
                  itemRefs.current[index] = el
                }}
                tabIndex={activeIndex === index ? 0 : -1}
                className="flex w-full cursor-pointer items-center rounded-sm border-0 bg-transparent px-[var(--space-2)] py-[var(--space-2)] text-left text-[length:var(--font-size-sm)] text-text hover:bg-accent-soft hover:text-accent-ink"
                onPointerDown={stopDragActivation}
                onClick={(event) => {
                  stopDragActivation(event)
                  setOpen(false)
                  triggerRef.current?.focus()
                  item.onSelect()
                }}
              >
                {item.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
