import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AvatarUploadCard } from './AvatarUploadCard'

const { mutate } = vi.hoisted(() => ({ mutate: vi.fn() }))

vi.mock('../hooks/useUploadAvatarMutation', () => ({
  useUploadAvatarMutation: () => ({ mutate, isPending: false }),
}))

// No global testing-library setup file in this project — cleaned up locally,
// same as `app/RequirePermission.test.tsx`.
afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

function fakeFile(name: string, type: string, size: number): File {
  const file = new File(['x'.repeat(Math.min(size, 10))], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

describe('AvatarUploadCard — upload-state handling', () => {
  it('shows a validation error immediately on an unsupported file, without calling the upload mutation', () => {
    render(<AvatarUploadCard userId={1} name="Member One" avatarUrl={null} />)

    const input = screen.getByLabelText('Choose a profile picture') as HTMLInputElement
    fireEvent.change(input, { target: { files: [fakeFile('avatar.svg', 'image/svg+xml', 100)] } })

    expect(screen.getByText('Only JPEG, PNG or WebP pictures are supported.')).not.toBeNull()
    expect(mutate).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Upload' })).toBeNull()
  })

  it('shows a validation error immediately on an oversized file', () => {
    render(<AvatarUploadCard userId={1} name="Member One" avatarUrl={null} />)

    const input = screen.getByLabelText('Choose a profile picture') as HTMLInputElement
    fireEvent.change(input, { target: { files: [fakeFile('avatar.png', 'image/png', 2 * 1024 * 1024 + 1)] } })

    expect(screen.getByText('That picture is too large — 2 MiB or smaller.')).not.toBeNull()
    expect(mutate).not.toHaveBeenCalled()
  })

  it('enables Upload on a valid file, Cancel discards it back to the idle state', () => {
    render(<AvatarUploadCard userId={1} name="Member One" avatarUrl={null} />)

    const input = screen.getByLabelText('Choose a profile picture') as HTMLInputElement
    fireEvent.change(input, { target: { files: [fakeFile('avatar.png', 'image/png', 1024)] } })

    expect(screen.getByText(/avatar\.png/)).not.toBeNull()
    const uploadButton = screen.getByRole('button', { name: 'Upload' }) as HTMLButtonElement
    expect(uploadButton.disabled).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('button', { name: 'Upload' })).toBeNull()
    expect(screen.getByText(/Drag a picture here/)).not.toBeNull()
  })

  it('calls the mutation exactly once on Upload, with the picked file', async () => {
    mutate.mockImplementation((_file, options) => options.onSuccess())

    render(<AvatarUploadCard userId={1} name="Member One" avatarUrl={null} />)

    const input = screen.getByLabelText('Choose a profile picture') as HTMLInputElement
    const file = fakeFile('avatar.png', 'image/png', 1024)
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Upload' }))

    expect(mutate).toHaveBeenCalledTimes(1)
    expect(mutate.mock.calls[0]?.[0]).toBe(file)

    await waitFor(() => expect(screen.getByText('Picture updated.')).not.toBeNull())
  })

  it('shows a retry on failure', async () => {
    mutate.mockImplementation((_file, options) => options.onError(new Error('boom')))

    render(<AvatarUploadCard userId={1} name="Member One" avatarUrl={null} />)

    const input = screen.getByLabelText('Choose a profile picture') as HTMLInputElement
    fireEvent.change(input, { target: { files: [fakeFile('avatar.png', 'image/png', 1024)] } })
    fireEvent.click(screen.getByRole('button', { name: 'Upload' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Retry' })).not.toBeNull())
  })
})
