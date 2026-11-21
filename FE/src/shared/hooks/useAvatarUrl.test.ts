import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useAvatarUrl } from './useAvatarUrl'

const { fetchAuthenticatedBlob } = vi.hoisted(() => ({ fetchAuthenticatedBlob: vi.fn() }))

vi.mock('../../api/client', () => ({ fetchAuthenticatedBlob }))

afterEach(() => {
  vi.clearAllMocks()
  vi.restoreAllMocks()
})

function fakeResponse(): Response {
  return { blob: () => Promise.resolve(new Blob(['x'])) } as unknown as Response
}

describe('useAvatarUrl — blob cache refcounting', () => {
  it('fetches once for two concurrent consumers of the same avatarUrl, and does not revoke until the last one unmounts', async () => {
    fetchAuthenticatedBlob.mockResolvedValue(fakeResponse())
    const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:one')
    const revokeObjectURL = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})

    const a = renderHook(() => useAvatarUrl('/api/v1/users/1/avatar?v=abc'))
    const b = renderHook(() => useAvatarUrl('/api/v1/users/1/avatar?v=abc'))

    await waitFor(() => expect(a.result.current).toBe('blob:one'))
    await waitFor(() => expect(b.result.current).toBe('blob:one'))

    // One network fetch and one object URL for both consumers of the same key.
    expect(fetchAuthenticatedBlob).toHaveBeenCalledTimes(1)
    expect(createObjectURL).toHaveBeenCalledTimes(1)

    a.unmount()
    expect(revokeObjectURL).not.toHaveBeenCalled()

    b.unmount()
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:one')
  })

  it('returns null and never calls URL.createObjectURL when avatarUrl is null', () => {
    const createObjectURL = vi.spyOn(URL, 'createObjectURL')
    const { result } = renderHook(() => useAvatarUrl(null))

    expect(result.current).toBeNull()
    expect(createObjectURL).not.toHaveBeenCalled()
  })
})
