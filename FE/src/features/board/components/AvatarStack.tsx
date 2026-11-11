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
 * `docs/design/SPEC.md` §4 "AvatarStack" / §5.1 — toolbar avatars (overlap
 * -6, ring `urgent` when the person is full), "+N" bubble, chevron opening
 * the Team popover (`docs/design/COMING-SOON.md` A9, built this session).
 * Order and capacity data: `useTeamCapacity` (full first, then most loaded) —
 * same source and completeness caveat as the Team capacity page (A2).
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
            className="flex size-[26px] shrink-0 items-center justify-center rounded-pill text-[11px] font-semibold"
            style={{
              backgroundColor: `var(--avatar-${avatarPaletteIndex(user.id)}-bg)`,
              color: `var(--avatar-${avatarPaletteIndex(user.id)}-ink)`,
              marginLeft: index === 0 ? 0 : -6,
              boxShadow: isFull ? '0 0 0 2px var(--color-bg), 0 0 0 4px var(--color-priority-urgent)' : '0 0 0 2px var(--color-bg)',
            }}
            title={user.userName}
          >
            {initialsOf(user.userName)}
          </span>
        ))}
        {overflow > 0 && (
          <span
            className="flex size-[26px] shrink-0 items-center justify-center rounded-pill bg-column text-[11px] font-semibold text-text-muted"
            style={{ marginLeft: -6, boxShadow: '0 0 0 2px var(--color-bg)' }}
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
