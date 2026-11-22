import { describe, expect, it } from 'vitest'

// Hand-copied "filled primary button" look — `bg-accent` (not `bg-accent-soft`)
// together with `text-on-accent` in the same literal — the exact pattern this
// bug fix replaced with `<Button variant="primary">` everywhere else.
const PRIMARY_BUTTON_PATTERN = /bg-accent(?!-)[^"'`]*text-on-accent|text-on-accent[^"'`]*bg-accent(?!-)/

// Intentional non-`Button` uses of that same colour pair — never a plain
// button standing in for a page/dialog action, the one thing this scan
// guards against.
const ALLOWLIST = new Set([
  // Sidebar's own logo mark — a brand mark, not a button.
  '/src/app/Sidebar.tsx',
  // A navigation `<Link>` styled to match `Button`'s primary look — stays an
  // `<a>` for navigation (`frontend.md` §8), never a `<button>`, so it can't
  // use the shared component itself; classes are hand-matched instead.
  '/src/features/not-found/NotFoundPage.tsx',
  // The Button component itself defines the pattern as a class string, not a
  // raw hand-copied button.
  '/src/shared/components/Button.tsx',
])

// `import.meta.glob` (Vite, already typed via `vite/client` in
// `tsconfig.app.json`) reads source as plain text — no Node `fs`/`path`
// types needed, unlike the rest of this app's browser-only tsconfig.
const sourceModules = import.meta.glob(['/src/features/**/*.{ts,tsx}', '/src/shared/**/*.{ts,tsx}', '/src/app/**/*.{ts,tsx}'], {
  eager: true,
  query: '?raw',
  import: 'default',
}) as Record<string, string>

describe('No hand-copied primary-button class pattern outside <Button> (bug fix, 2026-11-22)', () => {
  it('finds bg-accent + text-on-accent only in the allowlisted files', () => {
    const offenders = Object.entries(sourceModules)
      .filter(([path]) => !path.endsWith('.test.ts') && !path.endsWith('.test.tsx') && !ALLOWLIST.has(path))
      .filter(([, content]) => PRIMARY_BUTTON_PATTERN.test(content))
      .map(([path]) => path)

    expect(offenders).toEqual([])
  })
})
