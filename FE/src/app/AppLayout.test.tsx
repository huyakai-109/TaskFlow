import { cleanup, render } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AppLayout } from './AppLayout'

// Sidebar/Topbar pull in a long chain of query hooks unrelated to this
// test (overdue count, users, board search, notifications…) — stubbed out
// so this stays a layout test, not a re-test of either component.
vi.mock('./Sidebar', () => ({ Sidebar: () => <aside>sidebar</aside> }))
vi.mock('./Topbar', () => ({ Topbar: () => <header>topbar</header> }))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe('AppLayout — shell root is a positioned containing block (live /reports/team scroll bug)', () => {
  it('carries `relative` on the `flex h-dvh overflow-hidden` root, so an absolutely-positioned descendant (e.g. a sr-only <caption>) is clipped by it instead of escaping to the document', () => {
    const { container } = render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>,
    )

    const root = container.firstElementChild as HTMLElement
    expect(root.className).toContain('relative')
    expect(root.className).toContain('h-dvh')
    expect(root.className).toContain('overflow-hidden')
  })
})
