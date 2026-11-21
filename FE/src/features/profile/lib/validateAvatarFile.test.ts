import { describe, expect, it } from 'vitest'

import { AVATAR_MAX_BYTES, formatFileSize, validateAvatarFile } from './validateAvatarFile'

function fakeFile(type: string, size: number): File {
  return { type, size, name: 'avatar.png' } as unknown as File
}

describe('validateAvatarFile', () => {
  it('accepts a JPEG/PNG/WebP file within the size cap', () => {
    expect(validateAvatarFile(fakeFile('image/jpeg', 1024))).toBeNull()
    expect(validateAvatarFile(fakeFile('image/png', AVATAR_MAX_BYTES))).toBeNull()
    expect(validateAvatarFile(fakeFile('image/webp', 1))).toBeNull()
  })

  it('refuses an unsupported type (e.g. SVG/GIF) even if small', () => {
    expect(validateAvatarFile(fakeFile('image/svg+xml', 100))).toBe('Only JPEG, PNG or WebP pictures are supported.')
    expect(validateAvatarFile(fakeFile('image/gif', 100))).toBe('Only JPEG, PNG or WebP pictures are supported.')
  })

  it('refuses an empty file', () => {
    expect(validateAvatarFile(fakeFile('image/png', 0))).toBe('Choose a picture to upload.')
  })

  it('refuses a file over 2 MiB, accepts exactly 2 MiB', () => {
    expect(validateAvatarFile(fakeFile('image/png', AVATAR_MAX_BYTES + 1))).toBe('That picture is too large — 2 MiB or smaller.')
    expect(validateAvatarFile(fakeFile('image/png', AVATAR_MAX_BYTES))).toBeNull()
  })
})

describe('formatFileSize', () => {
  it('formats bytes, kilobytes and megabytes', () => {
    expect(formatFileSize(500)).toBe('500 B')
    expect(formatFileSize(2048)).toBe('2 KB')
    expect(formatFileSize(1_500_000)).toBe('1.4 MB')
  })
})
