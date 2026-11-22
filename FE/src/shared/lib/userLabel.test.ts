import { describe, expect, it } from 'vitest'

import { userLabel } from './userLabel'

describe('userLabel', () => {
  it('returns displayName when present', () => {
    expect(userLabel({ userName: 'member1', displayName: 'Lan' })).toBe('Lan')
  })

  it('falls back to userName when displayName is null', () => {
    expect(userLabel({ userName: 'member1', displayName: null })).toBe('member1')
  })

  it('falls back to userName when displayName is undefined', () => {
    expect(userLabel({ userName: 'member1' })).toBe('member1')
  })

  it('falls back to userName when displayName is an empty string', () => {
    expect(userLabel({ userName: 'member1', displayName: '' })).toBe('member1')
  })

  it('falls back to userName when displayName is whitespace-only', () => {
    expect(userLabel({ userName: 'member1', displayName: '   ' })).toBe('member1')
  })

  it('returns the existing "no name available" fallback when both are missing', () => {
    expect(userLabel({})).toBe('?')
  })

  it('returns the same fallback for a null/undefined user', () => {
    expect(userLabel(null)).toBe('?')
    expect(userLabel(undefined)).toBe('?')
  })
})
