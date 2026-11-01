import { ApiError } from './client'

/**
 * One error-code → copy map, per `docs/ui.md` ("Server error codes map to these
 * messages in one place in the client"). Every call site renders through
 * `describeError` (have an `ApiError`) or `describeErrorCode` (have a bare
 * code already read off one, e.g. a mutation's `onError`) instead of inventing
 * its own text.
 *
 * 401/403 carry no `errorCode` (`f01-workflow.md` T3) and are mapped from the
 * HTTP status instead.
 */
const ERROR_MESSAGES: Record<string, string> = {
  'Task.GetBoard.Exception': 'The board could not be loaded. Try again in a moment.',
  'Task.Get.NotFound': 'This task no longer exists.',
  'Task.Get.Exception': 'The task could not be loaded. Try again in a moment.',
  'Task.Move.NotFound': 'This task no longer exists. The board has been refreshed.',
  'Task.Move.Conflict': 'Someone else changed this task. The board has been refreshed.',
  'Task.Move.InvalidTransition': 'Tasks move one stage at a time — choose one of its legal next steps.',
  'Task.Move.SameStatus': 'This task is already in that column.',
  'Task.Move.ActorIsAssignee': 'Someone other than the assignee has to move this out of Review.',
  'Task.Move.Exception': 'The move could not be completed. Try again in a moment.',
  'Task.Reopen.NotFound': 'This task no longer exists.',
  'Task.Reopen.Conflict': 'Someone else changed this task. Refreshing it now.',
  'Task.Reopen.InvalidTransition': 'Only a task in Done can be reopened.',
  'Task.Reopen.Exception': 'The task could not be reopened. Try again in a moment.',
  'TaskHistory.GetList.TaskNotFound': 'This task no longer exists.',
  'TaskHistory.GetList.Exception': 'The history could not be loaded. Try again in a moment.',
  'Notification.GetList.Exception': 'Notifications could not be loaded. Try again in a moment.',
  'Notification.MarkRead.NotFound': 'This notification no longer exists.',
  'Notification.MarkRead.Exception': 'Could not mark this notification as read.',
}

const GENERIC_FALLBACK = 'Something went wrong. Try again.'

/** For a bare error code already pulled off an `ApiError` (a mutation's `onError`, typically). */
export function describeErrorCode(code: string | null): string {
  if (code !== null && code in ERROR_MESSAGES) {
    return ERROR_MESSAGES[code]
  }

  return GENERIC_FALLBACK
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

  return GENERIC_FALLBACK
}
