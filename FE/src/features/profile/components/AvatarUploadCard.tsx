import clsx from 'clsx'
import { useEffect, useRef, useState } from 'react'

import { ApiError } from '../../../api/client'
import { describeErrorCode } from '../../../api/errorMessages'
import { Avatar } from '../../../shared/components/Avatar'
import { useUploadAvatarMutation } from '../hooks/useUploadAvatarMutation'
import { AVATAR_ALLOWED_TYPES, formatFileSize, validateAvatarFile } from '../lib/validateAvatarFile'

type UploadPhase = 'idle' | 'uploading' | 'success' | 'error'

interface AvatarUploadCardProps {
  userId: number
  name: string
  avatarUrl: string | null
}

const sectionClass = 'flex flex-col gap-[var(--space-3)] rounded-xl border border-border bg-surface-raised p-[var(--space-5)]'
const fieldErrorClass = 'm-0 text-[length:var(--font-size-xs)] text-danger'

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
 * Avatar upload, redesigned (follow-up task): a styled "Choose photo" button
 * plus drag-and-drop replace the browser's default file input; a picked file
 * is validated immediately (not on Upload click) and previewed in place of
 * the current avatar with its filename/size, Upload enabled only once a
 * valid file is picked, Cancel discards it. Uploading/success/error states
 * are announced through one `aria-live` region. No cropping, no new
 * dependency — same client pre-check `ProfilePage` already had, just a new
 * frame around it.
 */
export function AvatarUploadCard({ userId, name, avatarUrl }: AvatarUploadCardProps) {
  const uploadAvatarMutation = useUploadAvatarMutation()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [pendingFile, setPendingFile] = useState<File | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [phase, setPhase] = useState<UploadPhase>('idle')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isDragOver, setIsDragOver] = useState(false)

  // The local preview is its own object URL (never the uploaded one, which
  // only exists after a successful request) — revoked on cancel, on
  // completion, and on unmount, same "don't leak blob URLs" rule as
  // `useAvatarUrl`.
  useEffect(() => {
    return () => {
      if (previewUrl !== null) {
        URL.revokeObjectURL(previewUrl)
      }
    }
  }, [previewUrl])

  function resetFileInput() {
    if (fileInputRef.current !== null) {
      fileInputRef.current.value = ''
    }
  }

  function discardPendingFile() {
    if (previewUrl !== null) {
      URL.revokeObjectURL(previewUrl)
    }
    setPreviewUrl(null)
    setPendingFile(null)
    resetFileInput()
  }

  function acceptFile(file: File) {
    setValidationError(null)
    setErrorMessage(null)
    setPhase('idle')

    const problem = validateAvatarFile(file)

    if (problem !== null) {
      setValidationError(problem)
      discardPendingFile()
      return
    }

    if (previewUrl !== null) {
      URL.revokeObjectURL(previewUrl)
    }

    setPendingFile(file)
    setPreviewUrl(URL.createObjectURL(file))
  }

  function handleCancel() {
    setValidationError(null)
    setErrorMessage(null)
    setPhase('idle')
    discardPendingFile()
  }

  function handleUpload() {
    if (pendingFile === null) {
      return
    }

    setPhase('uploading')
    setErrorMessage(null)

    uploadAvatarMutation.mutate(pendingFile, {
      onSuccess: () => {
        setPhase('success')
        discardPendingFile()
      },
      onError: (error) => {
        setPhase('error')
        const code = error instanceof ApiError ? error.errorCode : null
        setErrorMessage(describeErrorCode(code))
      },
    })
  }

  const isBusy = phase === 'uploading'

  return (
    <div className={sectionClass}>
      <h2 className="m-0 text-[length:var(--font-size-lg)] font-semibold">Picture</h2>

      <div
        className={clsx(
          'flex flex-wrap items-center gap-[var(--space-4)] rounded-lg border-2 border-dashed p-[var(--space-3)] transition-colors',
          isDragOver ? 'border-accent bg-accent-soft' : 'border-dashed-border',
        )}
        onDragOver={(event) => {
          if (!isBusy) {
            event.preventDefault()
            setIsDragOver(true)
          }
        }}
        onDragLeave={() => setIsDragOver(false)}
        onDrop={(event) => {
          event.preventDefault()
          setIsDragOver(false)

          if (isBusy) {
            return
          }

          const file = event.dataTransfer.files[0]

          if (file !== undefined) {
            acceptFile(file)
          }
        }}
      >
        {previewUrl !== null ? (
          <img src={previewUrl} alt="" aria-hidden="true" className="size-16 shrink-0 rounded-pill object-cover" />
        ) : (
          <Avatar userId={userId} name={name} avatarUrl={avatarUrl} size={64} />
        )}

        <div className="flex min-w-0 flex-1 flex-col gap-[var(--space-2)]">
          {pendingFile !== null ? (
            <p className="m-0 min-w-0 truncate text-[length:var(--font-size-sm)] text-text">
              {pendingFile.name} <span className="text-text-muted">· {formatFileSize(pendingFile.size)}</span>
            </p>
          ) : (
            <p className="m-0 text-[length:var(--font-size-xs)] text-text-muted">
              Drag a picture here, or choose one. JPEG, PNG or WebP, 2 MiB or smaller.
            </p>
          )}

          <input
            ref={fileInputRef}
            type="file"
            accept={Array.from(AVATAR_ALLOWED_TYPES).join(',')}
            className="sr-only"
            aria-label="Choose a profile picture"
            disabled={isBusy}
            onChange={(event) => {
              const file = event.target.files?.[0]
              if (file !== undefined) {
                acceptFile(file)
              }
            }}
          />

          <div className="flex flex-wrap gap-[var(--space-2)]">
            <button
              type="button"
              className="min-h-9 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium text-text hover:bg-column disabled:cursor-not-allowed disabled:opacity-60"
              disabled={isBusy}
              onClick={() => fileInputRef.current?.click()}
            >
              Choose photo
            </button>

            {pendingFile !== null && (
              <>
                <button
                  type="button"
                  className="min-h-9 inline-flex cursor-pointer items-center gap-[var(--space-2)] rounded-md border-0 bg-accent px-[var(--space-3)] text-[length:var(--font-size-sm)] font-semibold text-on-accent hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={isBusy}
                  onClick={handleUpload}
                >
                  {isBusy && <SpinnerIcon />}
                  {isBusy ? 'Uploading…' : 'Upload'}
                </button>
                <button
                  type="button"
                  className="min-h-9 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium text-text hover:bg-column disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={isBusy}
                  onClick={handleCancel}
                >
                  Cancel
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      {/* One live region for every upload-flow status (`frontend.md` §8: async status changes are announced) — `alert` only for a genuine failure, `status` otherwise so it doesn't interrupt. */}
      <div aria-live="polite" role={phase === 'error' || validationError !== null ? 'alert' : 'status'}>
        {validationError !== null && <p className={fieldErrorClass}>{validationError}</p>}
        {phase === 'error' && errorMessage !== null && (
          <p className={fieldErrorClass}>
            {errorMessage}{' '}
            <button
              type="button"
              className="cursor-pointer border-0 bg-transparent p-0 text-accent-ink underline [font:inherit]"
              onClick={handleUpload}
            >
              Retry
            </button>
          </p>
        )}
        {phase === 'success' && (
          <p className="m-0 text-[length:var(--font-size-xs)] text-success">Picture updated.</p>
        )}
      </div>
    </div>
  )
}
