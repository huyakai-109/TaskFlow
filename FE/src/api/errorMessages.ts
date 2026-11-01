import { ApiError } from './client'

/**
 * One error-code → copy map, per `docs/ui.md` ("Server error codes map to these
 * messages in one place in the client"). Every call site renders through
 * `describeError` instead of inventing its own text, so slice 1.6's drag
 * refusals (`Task.Move.*`, `Task.Reopen.*`, …) only need new entries here.
 *
 * 401/403 carry no `errorCode` (`f01-workflow.md` T3) and are mapped from the
 * HTTP status instead.
 */
const ERROR_MESSAGES: Record<string, string> = {
  'Task.GetBoard.Exception': 'The board could not be loaded. Try again in a moment.',
}

export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) {
      return 'Your session has expired. Sign in again.'
    }

    if (error.status === 403) {
      return 'You do not have permission to see this.'
    }

    if (error.errorCode !== null && error.errorCode in ERROR_MESSAGES) {
      return ERROR_MESSAGES[error.errorCode]
    }

    return error.message
  }

  return 'Something went wrong. Try again.'
}
