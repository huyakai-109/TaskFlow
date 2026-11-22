import clsx from 'clsx'
import type { ButtonHTMLAttributes, ReactNode, Ref } from 'react'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'
export type ButtonSize = 'sm' | 'md' | 'lg'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  iconLeft?: ReactNode
  /** Shows a spinner in place of `iconLeft` and blocks `onClick` (same as `disabled`) — the request already in flight, not a second one. */
  loading?: boolean
  ref?: Ref<HTMLButtonElement>
}

// One rule, everywhere a button appears: `primary` is the single main action
// of a screen or dialog (Sign in, Create task, Save, a dialog's confirm);
// `secondary` is everything else (Cancel, Try again, Show more, a row's own
// action); the same logical action looks identical wherever it occurs.
const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: 'border-0 bg-accent font-semibold text-on-accent hover:bg-accent-hover',
  secondary: 'border border-border bg-surface font-medium text-text hover:bg-accent-soft hover:text-accent-ink',
  ghost: 'border-0 bg-transparent font-medium text-text hover:bg-column',
  danger: 'border-0 bg-danger font-semibold text-text-inverse hover:opacity-90',
}

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: 'min-h-9 gap-[var(--space-2)] px-[var(--space-3)] text-[length:var(--font-size-sm)]',
  md: 'min-h-10 gap-[var(--space-2)] px-[var(--space-4)] text-[length:var(--font-size-base)]',
  lg: 'min-h-11 gap-[var(--space-2)] px-[var(--space-4)] text-[length:var(--font-size-base)]',
}

function SpinnerIcon() {
  return (
    <svg
      aria-hidden="true"
      width="14"
      height="14"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      className="motion-safe:animate-spin"
    >
      <path d="M8 1.5a6.5 6.5 0 1 1-6.5 6.5" />
    </svg>
  )
}

/**
 * The one button in the app — every hand-copied primary/secondary/ghost/
 * danger button converges here (bug fix, 2026-11-22). `:focus-visible` comes
 * from the app-wide rule in `global.css`, not repeated here.
 */
export function Button({
  variant = 'secondary',
  size = 'md',
  iconLeft,
  loading = false,
  disabled,
  className,
  children,
  ref,
  ...rest
}: ButtonProps) {
  const isDisabled = disabled === true || loading

  return (
    <button
      ref={ref}
      className={clsx(
        'inline-flex cursor-pointer items-center justify-center rounded-md disabled:cursor-not-allowed disabled:opacity-60',
        VARIANT_CLASSES[variant],
        SIZE_CLASSES[size],
        className,
      )}
      disabled={isDisabled}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? <SpinnerIcon /> : iconLeft}
      {children}
    </button>
  )
}
