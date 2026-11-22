import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { Avatar } from './Avatar'

const { useAvatarUrl } = vi.hoisted(() => ({ useAvatarUrl: vi.fn() }))

vi.mock('../hooks/useAvatarUrl', () => ({ useAvatarUrl }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

/**
 * Investigation finding (brief's "avatar fallback check"): the user "QA
 * Renamed" rendered as a solid black circle instead of initials. The avatar
 * request itself was not inspected directly (no running backend this
 * session — `fetchAuthenticatedBlob` is mocked everywhere in this test
 * suite), but the code path is clear: `useAvatarUrl` turns *any* response
 * body into an object URL via `URL.createObjectURL`, including an empty
 * body, a non-image content type, or a 404 page — it never checks the
 * bytes decode as an image. The `<img>` element then fails to decode and
 * paints as a solid colour inside the circular `object-fit: cover` frame
 * (a classic broken-image symptom, not a legitimately black picture) —
 * there was no `onError` handler to catch that and fall back to initials.
 * Fixed in `Avatar.tsx`; this test fails without that fix (no `onError`
 * handler means the broken `<img>` stays mounted forever).
 */
describe('Avatar — falls back to initials when the image fails to decode', () => {
  it('renders the initials fallback once the <img> fires onError', () => {
    useAvatarUrl.mockReturnValue('blob:broken')

    const { container } = render(<Avatar userId={7} name="QA Renamed" avatarUrl="/api/v1/users/7/avatar?v=1" size={32} />)

    const img = container.querySelector('img')
    expect(img).not.toBeNull()

    fireEvent.error(img!)

    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByText('QA')).not.toBeNull()
  })

  it('renders the real image while it has not failed', () => {
    useAvatarUrl.mockReturnValue('blob:good')

    const { container } = render(<Avatar userId={7} name="QA Renamed" avatarUrl="/api/v1/users/7/avatar?v=1" size={32} />)

    expect(container.querySelector('img')).not.toBeNull()
    expect(screen.queryByText('QA')).toBeNull()
  })

  it('falls back to initials immediately when there is no avatar at all', () => {
    useAvatarUrl.mockReturnValue(null)

    render(<Avatar userId={7} name="QA Renamed" avatarUrl={null} size={32} />)

    expect(screen.getByText('QA')).not.toBeNull()
  })
})
