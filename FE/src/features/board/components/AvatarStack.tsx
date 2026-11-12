import { useEffect, useRef, useState } from 'react'

import { avatarPaletteIndex, initialsOf } from '../../../shared/lib/avatar'
import { useTeamCapacity } from '../../../shared/hooks/useTeamCapacity'

import { TeamCapacityPopover } from './TeamCapacityPopover'

const VISIBLE_COUNT = 5

function ChevronIcon() {
  return (
    <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="m4 6 4 4 4-4" />
    </svg>
  )
}

/**
 * `docs/design/SPEC.md` §4 "AvatarStack" / §5.1 — toolbar avatars, ring
 * `urgent` when the person is full), "+N" bubble, chevron opening the Team
 * popover (`docs/design/COMING-SOON.md` A9, built this session). Order and
 * capacity data: `useTeamCapacity` (full first, then most loaded) — same
 * source and completeness caveat as the Team capacity page (A2).
 *
 * Avatar diameter is 28px, not the wireframe's 26px, and overlap is -5px,
 * not -6px (layout fix, 2026-11-12, revised again) — the reported "clipped"
 * look was never the text overflowing its own avatar's box (it never did);
 * it was each avatar's own right edge, where the 2nd initial sits, being
 * *painted over* by the next avatar stacked on top of it at a -6/-7px
 * overlap. -5px keeps every initial's drawn region clear of the next
 * avatar's left edge at this size, while still reading as one overlapped
 * stack rather than a plain row.
 */
export function AvatarStack() {
  const capacity = useTeamCapacity()
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

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
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  if (capacity.isPending || capacity.isError) {
    return null
  }

  const visible = capacity.entries.slice(0, VISIBLE_COUNT)
  const overflow = capacity.entries.length - visible.length

  return (
    <div ref={containerRef} className="relative flex items-center">
      <div className="flex items-center" role="group" aria-label="Team members">
        {visible.map(({ user, isFull }, index) => (
          <span
            key={user.id}
            className="flex size-[28px] shrink-0 items-center justify-center rounded-pill text-[11px] leading-none font-semibold"
            style={{
              backgroundColor: `var(--avatar-${avatarPaletteIndex(user.id)}-bg)`,
              color: `var(--avatar-${avatarPaletteIndex(user.id)}-ink)`,
              marginLeft: index === 0 ? 0 : -5,
              boxShadow: isFull ? '0 0 0 2px var(--color-bg), 0 0 0 4px var(--color-priority-urgent)' : '0 0 0 2px var(--color-bg)',
            }}
            title={user.userName}
          >
            {initialsOf(user.userName)}
          </span>
        ))}
        {overflow > 0 && (
          <span
            className="flex size-[28px] shrink-0 items-center justify-center rounded-pill bg-column text-[11px] leading-none font-semibold text-text-muted"
            style={{ marginLeft: -5, boxShadow: '0 0 0 2px var(--color-bg)' }}
          >
            +{overflow}
          </span>
        )}
      </div>
      <button
        type="button"
        aria-label="Open team capacity"
        aria-expanded={open}
        className="ml-[var(--space-1)] inline-flex size-6 cursor-pointer items-center justify-center rounded-sm border-0 bg-transparent text-text-muted hover:text-text"
        onClick={() => setOpen((value) => !value)}
      >
        <ChevronIcon />
      </button>

      {open && <TeamCapacityPopover onClose={() => setOpen(false)} />}
    </div>
  )
}
