import { ApiError } from './client'

/**
 * One error-code → copy map, per `docs/design/SPEC.md` ("Server error codes map to these
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
  'Task.Create.DueDateInPast': 'The due date has already passed. Pick today or a future date.',
  'Task.Create.AssigneeNotFound': 'That person could not be found. Pick someone from the list.',
  'Task.Create.Exception': 'The task could not be created. Try again in a moment.',
  'Task.Update.NotFound': 'This task no longer exists.',
  'Task.Update.Conflict': 'Someone else changed this task. Refreshing it now.',
  'Task.Update.Exception': 'The task could not be saved. Try again in a moment.',
  'Task.UpdatePriority.NotFound': 'This task no longer exists.',
  'Task.UpdatePriority.Conflict': 'Someone else changed this task. Refreshing it now.',
  'Task.UpdatePriority.Exception': 'The priority could not be saved. Try again in a moment.',
  'Task.UpdateDueDate.NotFound': 'This task no longer exists.',
  'Task.UpdateDueDate.Conflict': 'Someone else changed this task. Refreshing it now.',
  'Task.UpdateDueDate.DueDateInPast': 'The due date has already passed. Pick today or a future date.',
  'Task.UpdateDueDate.Exception': 'The due date could not be saved. Try again in a moment.',
  'Task.Assign.NotFound': 'This task no longer exists.',
  'Task.Assign.Conflict': 'Someone else changed this task. Refreshing it now.',
  'Task.Assign.AssigneeNotFound': 'That person could not be found. Pick someone from the list.',
  'Task.Assign.Exception': 'The assignment could not be saved. Try again in a moment.',
  'Task.Move.AssigneeRequired': 'This task needs an assignee before it can move to In Progress. Pick someone to start it.',
  'Task.Assign.AssigneeRequired': 'The assignee can’t be cleared while this task is active.',
  // *.CapacityReached: the server's own `error` string already names the person and
  // the limit (`docs/features/f02-capacity.md` T4 sign-off) — `describeErrorCode`
  // below prefers that raw message over this fallback, which only shows if it is
  // ever missing.
  'Task.Move.CapacityReached': 'That person is already at the in-progress limit.',
  'Task.Assign.CapacityReached': 'That person is already at the in-progress limit.',
  'Request.Validation.Failed': 'Check the highlighted fields.',
  'TaskHistory.GetList.TaskNotFound': 'This task no longer exists.',
  'TaskHistory.GetList.Exception': 'The history could not be loaded. Try again in a moment.',
  'Notification.GetList.Exception': 'Notifications could not be loaded. Try again in a moment.',
  'Notification.MarkRead.NotFound': 'This notification no longer exists.',
  'Notification.MarkRead.Exception': 'Could not mark this notification as read.',
}

const GENERIC_FALLBACK = 'Something went wrong. Try again.'

/**
 * `*.CapacityReached` is server-templated with the assignee's name and the
 * configured limit (`TaskControllerMS.Templates.CapacityReached`) — shown
 * verbatim rather than re-derived from the code, per F02's technical contract.
 */
const CAPACITY_REACHED_CODES = new Set(['Task.Move.CapacityReached', 'Task.Assign.CapacityReached'])

/**
 * For a bare error code already pulled off an `ApiError` (a mutation's
 * `onError`, typically). `rawMessage` is the server's own `error` string;
 * it is shown as-is for a capacity refusal and otherwise ignored, so every
 * other code keeps going through the fixed copy above.
 */
export function describeErrorCode(code: string | null, rawMessage?: string | null): string {
  if (code !== null && CAPACITY_REACHED_CODES.has(code) && rawMessage != null && rawMessage.length > 0) {
    return rawMessage
  }

  if (code !== null && code in ERROR_MESSAGES) {
    return ERROR_MESSAGES[code]
  }

  return GENERIC_FALLBACK
}

/**
 * The three literal `error` strings `AuthController.Login` returns — auth sets
 * no `errorCode` (`docs/features/f01-login.md` "Contract used"), so this is
 * the one place they map to copy, R4's sibling rule applied to a literal
 * string instead of a code. Exported so `LoginPage` can also branch on
 * invalid-credential (clear the password field, AC2) and will-lock-out
 * (warning tone, AC3) without re-typing the raw strings.
 */
export const LOGIN_INVALID_CREDENTIAL = 'Invalid credential'
export const LOGIN_WILL_LOCK_OUT = 'Your account will be locked out'
const LOGIN_LOCKED_OUT = 'Your account has been locked out'

const LOGIN_ERROR_MESSAGES: Record<string, string> = {
  [LOGIN_INVALID_CREDENTIAL]: 'Incorrect username or password.',
  [LOGIN_WILL_LOCK_OUT]: 'One more failed attempt will lock your account. Contact an administrator if you are not sure of your password.',
  [LOGIN_LOCKED_OUT]: 'Your account has been locked out. Contact an administrator to unlock it.',
}

const GENERIC_LOGIN_FALLBACK = 'Could not reach the server. Check your connection and try again.'

/** For the login screen's raw `error` string (`null` for a network/transport failure or an unmapped value). */
export function describeLoginError(error: string | null): string {
  if (error !== null && error in LOGIN_ERROR_MESSAGES) {
    return LOGIN_ERROR_MESSAGES[error]
  }

  return GENERIC_LOGIN_FALLBACK
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
      return describeErrorCode(error.errorCode, error.message)
    }

    return error.message
  }

  return GENERIC_FALLBACK
}
