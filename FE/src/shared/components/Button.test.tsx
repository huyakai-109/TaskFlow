import { createRef } from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { Button } from './Button'

afterEach(() => {
  cleanup()
})

describe('Button — variants and sizes', () => {
  it('renders the primary variant classes', () => {
    render(<Button variant="primary">Save</Button>)
    const button = screen.getByRole('button', { name: 'Save' })
    expect(button.className).toContain('bg-accent')
    expect(button.className).toContain('text-on-accent')
  })

  it('renders the secondary variant classes (the default)', () => {
    render(<Button>Cancel</Button>)
    const button = screen.getByRole('button', { name: 'Cancel' })
    expect(button.className).toContain('border-border')
    expect(button.className).toContain('bg-surface')
  })

  it('renders the ghost variant classes', () => {
    render(<Button variant="ghost">Dismiss</Button>)
    const button = screen.getByRole('button', { name: 'Dismiss' })
    expect(button.className).toContain('bg-transparent')
  })

  it('renders the danger variant classes', () => {
    render(<Button variant="danger">Delete</Button>)
    const button = screen.getByRole('button', { name: 'Delete' })
    expect(button.className).toContain('bg-danger')
    expect(button.className).toContain('text-text-inverse')
  })

  it.each([
    ['sm', 'min-h-9'],
    ['md', 'min-h-10'],
    ['lg', 'min-h-11'],
  ] as const)('renders the %s size as %s', (size, expectedClass) => {
    render(<Button size={size}>Go</Button>)
    expect(screen.getByRole('button', { name: 'Go' }).className).toContain(expectedClass)
  })
})

describe('Button — disabled and loading block clicks', () => {
  it('does not fire onClick while disabled', () => {
    const onClick = vi.fn()
    render(
      <Button disabled onClick={onClick}>
        Submit
      </Button>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Submit' }))

    expect(onClick).not.toHaveBeenCalled()
  })

  it('does not fire onClick while loading, and shows a spinner instead of the icon', () => {
    const onClick = vi.fn()
    render(
      <Button loading iconLeft={<span data-testid="icon" />} onClick={onClick}>
        Submit
      </Button>,
    )

    const button = screen.getByRole('button', { name: 'Submit' })
    fireEvent.click(button)

    expect(onClick).not.toHaveBeenCalled()
    expect(button.hasAttribute('disabled')).toBe(true)
    expect(button.getAttribute('aria-busy')).toBe('true')
    expect(screen.queryByTestId('icon')).toBeNull()
  })

  it('fires onClick when neither disabled nor loading', () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Submit</Button>)

    fireEvent.click(screen.getByRole('button', { name: 'Submit' }))

    expect(onClick).toHaveBeenCalledTimes(1)
  })
})

describe('Button — forwarding', () => {
  it('forwards a ref to the underlying <button>', () => {
    const ref = createRef<HTMLButtonElement>()
    render(<Button ref={ref}>Go</Button>)

    expect(ref.current).not.toBeNull()
    expect(ref.current?.tagName).toBe('BUTTON')
  })

  it('forwards arbitrary aria/native button props', () => {
    render(
      <Button aria-label="Close dialog" title="Close">
        ×
      </Button>,
    )

    const button = screen.getByRole('button', { name: 'Close dialog' })
    expect(button.getAttribute('title')).toBe('Close')
  })
})
