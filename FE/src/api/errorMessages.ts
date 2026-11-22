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
/**
 * Client-assigned sentinel, never a server `errorCode` — a 403 from
 * `PlanTasks`'s policy carries no code at all (`f01-workflow.md` T3: "permission
 * → 403 from `[Authorize(Policy)]`"). `useUpdatePriorityMutation`/
 * `useUpdateDueDateMutation` map a 403 to this constant so the one
 * error-code → copy table still owns the message, instead of a second
 * ad hoc string living in the mutation. Session permissions can lag the
 * server by up to ~15 minutes after a role change (`round-2-decisions.md`
 * "Deploy note for R2-1"); the mutations also trigger a session refresh
 * when this fires, once, not a retry loop.
 */
export const FORBIDDEN_SENTINEL = 'Client.PlanTasksForbidden'

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
  // Revision R2 (holder-only progression, planning rights, assignment lock).
  'Task.Move.ActorNotAssignee': 'Only the current assignee can do this. Take over the task first, then try again.',
  'Task.Assign.SelfOnly': 'You can only assign this task to yourself.',
  'Task.Assign.Locked': 'The assignee is locked while this task is in Review or Done.',
  'Task.Create.PlanningNotPermitted': 'Only a Manager or Admin can set priority or due date. Remove them to create this task as Normal priority with no due date.',
  'Task.Create.AssigneeNotPermitted': 'You can only assign a new task to yourself.',
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
  'Notification.MarkAllRead.Exception': 'Could not mark your notifications as read. Try again in a moment.',
  [FORBIDDEN_SENTINEL]: "You don't have permission to change this. Your session is refreshing — try again in a moment.",
  // f08-accounts-profile.md "Auth changes": `POST /auth/password/change-required`
  // folds a wrong current password, a deactivated account, a MustChangePassword
  // mismatch *and* the Identity lockout check into this one code — there is
  // no distinct lockout signal on this endpoint (verified in
  // `AuthService.ChangeRequiredPassword`, confirmed not a client gap). Shown
  // generically, on purpose: a more specific message would let someone probe
  // whether a username exists or is currently locked out.
  'Auth.ChangeRequired.InvalidCredential': 'That username or current password is incorrect.',
  'Auth.ChangeRequired.PolicyViolation': 'Check the highlighted fields.',
  'Auth.ChangeRequired.Exception': 'The password could not be changed. Try again in a moment.',
  // f08-accounts-profile.md "Self API"/"Admin API".
  'Profile.Update.Conflict': 'Someone else changed your profile. Refreshing it now.',
  'Profile.Update.Exception': 'Your display name could not be saved. Try again in a moment.',
  'Profile.ChangePassword.WrongPassword': 'Your current password is incorrect.',
  'Profile.ChangePassword.PolicyViolation': 'Check the highlighted fields.',
  'Profile.ChangePassword.Exception': 'Your password could not be changed. Try again in a moment.',
  'Profile.Avatar.Empty': 'Choose a picture to upload.',
  'Profile.Avatar.TooLarge': 'That picture is too large — 2 MiB or smaller.',
  'Profile.Avatar.UnsupportedType': 'Only JPEG, PNG or WebP pictures are supported.',
  'Profile.Avatar.StorageUnavailable': 'The picture could not be stored right now. Try again in a moment.',
  'Profile.Avatar.Conflict': 'Someone else changed your profile. Try uploading again.',
  'Profile.Avatar.Exception': 'The picture could not be saved. Try again in a moment.',
  'User.Create.UserNameTaken': 'That username is already taken.',
  'User.ChangeRole.LastAdmin': 'This is the last Admin — change someone else to Admin first.',
  'User.ChangeRole.Conflict': 'Someone else changed this account. Refreshing it now.',
  'User.ChangeRole.NotFound': 'This account no longer exists.',
  'User.ChangeRole.Exception': 'The role could not be changed. Try again in a moment.',
  'User.Deactivate.Self': 'You cannot deactivate your own account.',
  'User.Deactivate.LastAdmin': 'This is the last Admin — assign another Admin before deactivating this account.',
  'User.Deactivate.Conflict': 'Someone else changed this account. Refreshing it now.',
  'User.Deactivate.NotFound': 'This account no longer exists.',
  'User.Deactivate.Exception': 'The account could not be deactivated. Try again in a moment.',
  'User.Activate.Conflict': 'Someone else changed this account. Refreshing it now.',
  'User.Activate.NotFound': 'This account no longer exists.',
  'User.Activate.Exception': 'The account could not be reactivated. Try again in a moment.',
  'User.ResetPassword.Conflict': 'Someone else changed this account. Refreshing it now.',
  'User.ResetPassword.NotFound': 'This account no longer exists.',
  'User.ResetPassword.Exception': 'The password could not be reset. Try again in a moment.',
  'User.RemoveAvatar.NotFound': 'This account no longer exists.',
  'User.RemoveAvatar.Exception': "The picture could not be removed. Try again in a moment.",
  'User.GetList.Exception': 'The account list could not be loaded. Try again in a moment.',
  'User.Get.NotFound': 'This account no longer exists.',
  'User.Get.Exception': 'The account could not be loaded. Try again in a moment.',
  'User.Create.Exception': 'The account could not be created. Try again in a moment.',
  // f09-reports.md "Error codes" — Reports replaces Activity.
  'Report.GetScorecard.InvalidRange': 'The start date must be on or before the end date.',
  'Report.GetScorecard.RangeInFuture': 'The end date can’t be after today.',
  'Report.GetScorecard.RangeTooLarge': 'Pick a range of 366 days or fewer.',
  'Report.GetScorecard.TooManyBuckets': 'Day view is limited to 92 days — pick a shorter range or a coarser granularity.',
  'Report.GetScorecard.SelfOnly': 'You can only view your own report.',
  'Report.GetScorecard.UserNotFound': 'That person could not be found.',
  'Report.GetScorecard.Exception': 'The report could not be loaded. Try again in a moment.',
  'Report.GetTeam.InvalidRange': 'The start date must be on or before the end date.',
  'Report.GetTeam.RangeInFuture': 'The end date can’t be after today.',
  'Report.GetTeam.RangeTooLarge': 'Pick a range of 366 days or fewer.',
  'Report.GetTeam.Exception': 'The team report could not be loaded. Try again in a moment.',
  'Report.GetTasks.InvalidRange': 'The start date must be on or before the end date.',
  'Report.GetTasks.RangeInFuture': 'The end date can’t be after today.',
  'Report.GetTasks.RangeTooLarge': 'Pick a range of 366 days or fewer.',
  'Report.GetTasks.SelfOnly': 'You can only view your own report.',
  'Report.GetTasks.UserNotFound': 'That person could not be found.',
  'Report.GetTasks.Exception': 'The task list could not be loaded. Try again in a moment.',
  'Report.Export.InvalidRange': 'The start date must be on or before the end date.',
  'Report.Export.RangeInFuture': 'The end date can’t be after today.',
  'Report.Export.RangeTooLarge': 'Pick a range of 366 days or fewer.',
  'Report.Export.TooManyBuckets': 'Day view is limited to 92 days — pick a shorter range or a coarser granularity.',
  'Report.Export.InvalidDataset': 'The members export is only available for the team scope.',
  'Report.Export.UserNotFound': 'That person could not be found.',
  'Report.Export.TooManyRows': 'This range has too much data to export — pick a shorter range.',
  'Report.Export.Exception': 'The export could not be generated. Try again in a moment.',
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
