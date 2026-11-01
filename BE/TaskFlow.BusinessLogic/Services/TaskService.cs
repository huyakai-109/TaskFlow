using TaskFlow.BusinessLogic.Dtos.Base;
using TaskFlow.BusinessLogic.Dtos.Tasks;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TaskFlow.BusinessLogic.Services
{
    public interface ITaskService
    {
        Task<(string ErrorCode, TaskDetailResDto? Task)> Create(TaskCreateReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Get(long id, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Move(long id, TaskMoveReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Reopen(long id, TaskReopenReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Update(long id, TaskUpdateReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> UpdatePriority(long id, TaskUpdatePriorityReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> UpdateDueDate(long id, TaskUpdateDueDateReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Assign(long id, TaskAssignReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<string> Delete(long id, Guid version, long actorUserId);

        Task<BoardResDto> GetBoard(long actorUserId, string[] callerPermissions);

        Task<Pagination<TaskSummaryResDto>> GetList(TaskWorkflowStatus? status, int start, int length, long actorUserId, string[] callerPermissions);
    }

    public class TaskService(
        IConfiguration configuration,
        IUnitOfWork unitOfWork,
        ILogger<TaskService> logger) : ITaskService
    {
        // f01-workflow.md T5 endpoint 3: always five columns in lifecycle
        // order; F01 ships 50/column, F03 sets per-column page sizes later.
        private static readonly TaskWorkflowStatus[] BoardColumnOrder =
        [
            TaskWorkflowStatus.Backlog,
            TaskWorkflowStatus.Todo,
            TaskWorkflowStatus.InProgress,
            TaskWorkflowStatus.Review,
            TaskWorkflowStatus.Done,
        ];

        private const int BoardColumnPageSize = 50;

        private readonly string _timeZone = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;

        public async Task<(string ErrorCode, TaskDetailResDto? Task)> Create(TaskCreateReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var today = TodayInZone();

            if (requestDto.DueDate.HasValue && requestDto.DueDate.Value < today)
            {
                return (TaskControllerMS.Create.DueDateInPast, null);
            }

            string? assigneeUserName = null;
            if (requestDto.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(requestDto.AssigneeId.Value);
                if (assignee == null)
                {
                    return (TaskControllerMS.Create.AssigneeNotFound, null);
                }

                assigneeUserName = assignee.UserName;
            }

            // The history row's TaskId can only be known once the task's
            // identity value is generated, so this is the multi-aggregate-write
            // case (dotnet-patterns.md Recipe 8), not the single-SaveChanges()
            // case f01-workflow.md T2 describes for move/reopen/assign - those
            // mutate a task that already has a real id. Everything this
            // operation reads and writes happens inside the retried block.
            var strategy = unitOfWork.DbContext.Database.CreateExecutionStrategy();
            var task = await strategy.ExecuteAsync(requestDto, async (_, state, _) =>
            {
                unitOfWork.DbContext.ChangeTracker.Clear();

                var newTask = new TaskItem
                {
                    Title = state.Title,
                    Description = state.Description,
                    Status = TaskWorkflowStatus.Backlog,
                    Priority = state.Priority,
                    AssigneeId = state.AssigneeId,
                    DueDate = state.DueDate,
                    CompletedAt = null,
                    IsOverdue = TaskOverdueRule.IsOverdue(state.DueDate, TaskWorkflowStatus.Backlog, today),
                    Version = Guid.NewGuid(),
                };

                await using var transaction = await unitOfWork.DbContext.Database.BeginTransactionAsync();

                await unitOfWork.GetRepository<TaskItem>().Add(newTask);
                await unitOfWork.SaveChanges();

                var history = new TaskHistory
                {
                    TaskId = newTask.Id,
                    Event = TaskHistoryEvent.Created,
                    ActorUserId = actorUserId,
                };
                await unitOfWork.GetRepository<TaskHistory>().Add(history);
                await unitOfWork.SaveChanges();

                await transaction.CommitAsync();

                return newTask;
            }, verifySucceeded: null, cancellationToken: default);

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        public async Task<(string ErrorCode, TaskDetailResDto? Task)> Get(long id, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.Get.NotFound, null);
            }

            string? assigneeUserName = null;
            if (task.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(task.AssigneeId.Value);
                assigneeUserName = assignee?.UserName;
            }

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        // D3 + T2 validation order: existence -> version -> transition validity
        // (same-status, then the transition table) -> actor rule (D5). F02's
        // capacity guard is not implemented yet, so RequiresAssignee and
        // ConsumesCapacity are not checked here (T3: AssigneeRequired /
        // CapacityReached are reserved for F02).
        public async Task<(string ErrorCode, TaskDetailResDto? Task)> Move(long id, TaskMoveReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.Move.NotFound, null);
            }

            if (task.Version != requestDto.Version)
            {
                return (TaskControllerMS.Move.Conflict, null);
            }

            if (requestDto.Status == task.Status)
            {
                return (TaskControllerMS.Move.SameStatus, null);
            }

            var edge = TaskTransitions.FindMove(task.Status, requestDto.Status);
            if (edge == null)
            {
                return (TaskControllerMS.Move.InvalidTransition, null);
            }

            if (edge.ActorMustNotBeAssignee && task.AssigneeId.HasValue && task.AssigneeId.Value == actorUserId)
            {
                return (TaskControllerMS.Move.ActorIsAssignee, null);
            }

            var fromStatus = task.Status;
            task.Status = requestDto.Status;

            if (task.Status == TaskWorkflowStatus.Done)
            {
                var today = TodayInZone();
                task.CompletedAt = DateTimeHelper.GetDtOffset();
                task.IsOverdue = TaskOverdueRule.IsOverdue(task.DueDate, TaskWorkflowStatus.Done, today);
            }

            task.Version = Guid.NewGuid();

            var history = new TaskHistory
            {
                TaskId = task.Id,
                Event = TaskHistoryEvent.StatusChanged,
                ActorUserId = actorUserId,
                FromValue = fromStatus.ToString(),
                ToValue = task.Status.ToString(),
            };
            await unitOfWork.GetRepository<TaskHistory>().Add(history);

            // D7: a rework move raises the assignee's one in-app notification.
            // Named explicitly by edge rather than "ActorMustNotBeAssignee &&
            // RequiresAssignee", which Review->Done also satisfies.
            if (edge.From == TaskWorkflowStatus.Review && edge.To == TaskWorkflowStatus.InProgress && task.AssigneeId.HasValue)
            {
                var notification = new TaskNotification
                {
                    RecipientUserId = task.AssigneeId.Value,
                    TaskId = task.Id,
                    Event = TaskNotificationEvent.ReturnedForRework,
                };
                await unitOfWork.GetRepository<TaskNotification>().Add(notification);
            }

            try
            {
                // Exactly one SaveChanges(): task update + history insert +
                // notification insert commit together or not at all (AC31).
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (TaskControllerMS.Move.Conflict, null);
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                // Two real concurrent writers can make InnoDB pick a deadlock
                // victim instead of cleanly affecting 0 rows (confirmed
                // empirically under slice 1.2's AC29 test, ROADMAP R13): the
                // victim's whole transaction is already rolled back, so this
                // is the same "someone else moved it, please retry" case as
                // the optimistic-concurrency miss above, not a defect to
                // surface as 500. Pomelo's non-retrying execution strategy
                // wraps the MySqlException two levels deep, inside an
                // InvalidOperationException inside a DbUpdateException, so
                // the type itself can't be matched in the catch clause -
                // only the unwrapped chain can. Any other DbUpdateException
                // (lost connection, FK violation, ...) is deliberately left
                // uncaught here and surfaces as Task.Move.Exception (500) in
                // the controller - IsDeadlock only matches MySQL error 1213.
                logger.LogWarning(ex, "Move task {Id} hit a MySQL deadlock (1213); treated as Task.Move.Conflict", id);
                return (TaskControllerMS.Move.Conflict, null);
            }

            string? assigneeUserName = null;
            if (task.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(task.AssigneeId.Value);
                assigneeUserName = assignee?.UserName;
            }

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        // D6/AC6: validated against TaskTransitions.Reopens only - Done is not
        // reachable through Move (AC13). Same validation order as Move
        // (existence -> version -> transition) minus the actor rule, which
        // the Done->Review edge does not carry. AC21 (ReopenTasks) is the
        // [Authorize(Policy)] 403; AC22 (blank reason) is TaskReopenReq's
        // [Required] shape rule - neither needs a check here.
        public async Task<(string ErrorCode, TaskDetailResDto? Task)> Reopen(long id, TaskReopenReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.Reopen.NotFound, null);
            }

            if (task.Version != requestDto.Version)
            {
                return (TaskControllerMS.Reopen.Conflict, null);
            }

            var edge = TaskTransitions.FindReopen(task.Status, TaskWorkflowStatus.Review);
            if (edge == null)
            {
                return (TaskControllerMS.Reopen.InvalidTransition, null);
            }

            var today = TodayInZone();
            var wasOverdue = task.IsOverdue;

            task.Status = TaskWorkflowStatus.Review;
            task.CompletedAt = null;
            task.IsOverdue = TaskOverdueRule.IsOverdue(task.DueDate, task.Status, today);
            task.Version = Guid.NewGuid();

            var history = new TaskHistory
            {
                TaskId = task.Id,
                Event = TaskHistoryEvent.Reopened,
                ActorUserId = actorUserId,
                FromValue = nameof(TaskWorkflowStatus.Done),
                ToValue = nameof(TaskWorkflowStatus.Review),
                Reason = requestDto.Reason,
            };
            await unitOfWork.GetRepository<TaskHistory>().Add(history);

            // AC23 + f01-workflow.md T7 assumption: reopening a task whose due
            // date has already passed writes a second entry, OverdueMarked,
            // whose actor is the reopening user (not System - F04 AC8
            // constrains only the nightly job's actor). Both entries commit in
            // this same SaveChanges() (AC31, F04 AC10).
            if (!wasOverdue && task.IsOverdue)
            {
                var overdueHistory = new TaskHistory
                {
                    TaskId = task.Id,
                    Event = TaskHistoryEvent.OverdueMarked,
                    ActorUserId = actorUserId,
                };
                await unitOfWork.GetRepository<TaskHistory>().Add(overdueHistory);
            }

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (TaskControllerMS.Reopen.Conflict, null);
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                // Same MySQL 1213 lock-upgrade deadlock Move hits under
                // concurrent writers (ROADMAP R13); reopen inserts into
                // TaskHistories in the same SaveChanges() as its UPDATE, so the
                // identical mechanism applies here. See TaskService.Move's
                // catch clause for the full explanation.
                logger.LogWarning(ex, "Reopen task {Id} hit a MySQL deadlock (1213); treated as Task.Reopen.Conflict", id);
                return (TaskControllerMS.Reopen.Conflict, null);
            }

            string? assigneeUserName = null;
            if (task.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(task.AssigneeId.Value);
                assigneeUserName = assignee?.UserName;
            }

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        // Title/Description only - no history entry (f04-audit.md scope,
        // f01-workflow.md T5 endpoint 5). Same existence -> version guard as
        // every other mutating path; same two-catch concurrency shape (T2).
        public async Task<(string ErrorCode, TaskDetailResDto? Task)> Update(long id, TaskUpdateReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.Update.NotFound, null);
            }

            if (task.Version != requestDto.Version)
            {
                return (TaskControllerMS.Update.Conflict, null);
            }

            task.Title = requestDto.Title;
            task.Description = requestDto.Description;
            task.Version = Guid.NewGuid();

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (TaskControllerMS.Update.Conflict, null);
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                logger.LogWarning(ex, "Update task {Id} hit a MySQL deadlock (1213); treated as Task.Update.Conflict", id);
                return (TaskControllerMS.Update.Conflict, null);
            }

            string? assigneeUserName = null;
            if (task.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(task.AssigneeId.Value);
                assigneeUserName = assignee?.UserName;
            }

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        // F04 AC4: writes a PriorityChanged entry recording both values as
        // enum names (T2), unconditionally - there is no "same priority"
        // error code in the contract (unlike Move's SameStatus).
        public async Task<(string ErrorCode, TaskDetailResDto? Task)> UpdatePriority(long id, TaskUpdatePriorityReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.UpdatePriority.NotFound, null);
            }

            if (task.Version != requestDto.Version)
            {
                return (TaskControllerMS.UpdatePriority.Conflict, null);
            }

            var fromPriority = task.Priority;
            task.Priority = requestDto.Priority;
            task.Version = Guid.NewGuid();

            var history = new TaskHistory
            {
                TaskId = task.Id,
                Event = TaskHistoryEvent.PriorityChanged,
                ActorUserId = actorUserId,
                FromValue = fromPriority.ToString(),
                ToValue = task.Priority.ToString(),
            };
            await unitOfWork.GetRepository<TaskHistory>().Add(history);

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (TaskControllerMS.UpdatePriority.Conflict, null);
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                logger.LogWarning(ex, "Update priority of task {Id} hit a MySQL deadlock (1213); treated as Task.UpdatePriority.Conflict", id);
                return (TaskControllerMS.UpdatePriority.Conflict, null);
            }

            string? assigneeUserName = null;
            if (task.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(task.AssigneeId.Value);
                assigneeUserName = assignee?.UserName;
            }

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        // F04 AC5: writes a DueDateChanged entry with null represented
        // explicitly on either side. A past date is rejected the same way
        // Create rejects one (T3), unless the due date is being cleared to
        // null. IsOverdue is recomputed in the same commit (TaskOverdueRule).
        public async Task<(string ErrorCode, TaskDetailResDto? Task)> UpdateDueDate(long id, TaskUpdateDueDateReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.UpdateDueDate.NotFound, null);
            }

            if (task.Version != requestDto.Version)
            {
                return (TaskControllerMS.UpdateDueDate.Conflict, null);
            }

            var today = TodayInZone();
            if (requestDto.DueDate.HasValue && requestDto.DueDate.Value < today)
            {
                return (TaskControllerMS.UpdateDueDate.DueDateInPast, null);
            }

            var fromDueDate = task.DueDate?.ToString(DateTimeFormats.Date);
            task.DueDate = requestDto.DueDate;
            task.IsOverdue = TaskOverdueRule.IsOverdue(task.DueDate, task.Status, today);
            task.Version = Guid.NewGuid();

            var history = new TaskHistory
            {
                TaskId = task.Id,
                Event = TaskHistoryEvent.DueDateChanged,
                ActorUserId = actorUserId,
                FromValue = fromDueDate,
                ToValue = task.DueDate?.ToString(DateTimeFormats.Date),
            };
            await unitOfWork.GetRepository<TaskHistory>().Add(history);

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (TaskControllerMS.UpdateDueDate.Conflict, null);
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                logger.LogWarning(ex, "Update due date of task {Id} hit a MySQL deadlock (1213); treated as Task.UpdateDueDate.Conflict", id);
                return (TaskControllerMS.UpdateDueDate.Conflict, null);
            }

            string? assigneeUserName = null;
            if (task.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(task.AssigneeId.Value);
                assigneeUserName = assignee?.UserName;
            }

            return (string.Empty, BuildDetail(task, assigneeUserName, actorUserId, callerPermissions));
        }

        // F04 AC3: writes an AssigneeChanged entry naming both the old and
        // new assignee (as user ids, T2). Existence check reuses Create's
        // AssigneeNotFound guard. F02's capacity/AssigneeRequired codes are
        // reserved - not implemented here (f01-workflow.md T3).
        public async Task<(string ErrorCode, TaskDetailResDto? Task)> Assign(long id, TaskAssignReqDto requestDto, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return (TaskControllerMS.Assign.NotFound, null);
            }

            if (task.Version != requestDto.Version)
            {
                return (TaskControllerMS.Assign.Conflict, null);
            }

            string? newAssigneeUserName = null;
            if (requestDto.AssigneeId.HasValue)
            {
                var assignee = await unitOfWork.GetRepository<User>().FindById(requestDto.AssigneeId.Value);
                if (assignee == null)
                {
                    return (TaskControllerMS.Assign.AssigneeNotFound, null);
                }

                newAssigneeUserName = assignee.UserName;
            }

            var fromAssigneeId = task.AssigneeId;
            task.AssigneeId = requestDto.AssigneeId;
            task.Version = Guid.NewGuid();

            var history = new TaskHistory
            {
                TaskId = task.Id,
                Event = TaskHistoryEvent.AssigneeChanged,
                ActorUserId = actorUserId,
                FromValue = fromAssigneeId?.ToString(),
                ToValue = task.AssigneeId?.ToString(),
            };
            await unitOfWork.GetRepository<TaskHistory>().Add(history);

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (TaskControllerMS.Assign.Conflict, null);
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                logger.LogWarning(ex, "Assign task {Id} hit a MySQL deadlock (1213); treated as Task.Assign.Conflict", id);
                return (TaskControllerMS.Assign.Conflict, null);
            }

            return (string.Empty, BuildDetail(task, newAssigneeUserName, actorUserId, callerPermissions));
        }

        // F04 AC9/D6: soft delete + a Deleted entry in the same commit, trail
        // untouched. UnitOfWork.SaveChangesSoftDelete resets the tracked
        // entry to Unchanged and flips only IsDeleted (CLAUDE.md "Soft
        // delete"), so Version is NOT bumped here - the token stays in the
        // WHERE clause (IsConcurrencyToken), so the version guard still holds.
        public async Task<string> Delete(long id, Guid version, long actorUserId)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
            var task = query.FirstOrDefault();

            if (task == null)
            {
                return TaskControllerMS.Delete.NotFound;
            }

            if (task.Version != version)
            {
                return TaskControllerMS.Delete.Conflict;
            }

            await unitOfWork.GetRepository<TaskItem>().Delete(task);

            var history = new TaskHistory
            {
                TaskId = task.Id,
                Event = TaskHistoryEvent.Deleted,
                ActorUserId = actorUserId,
            };
            await unitOfWork.GetRepository<TaskHistory>().Add(history);

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return TaskControllerMS.Delete.Conflict;
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                logger.LogWarning(ex, "Delete task {Id} hit a MySQL deadlock (1213); treated as Task.Delete.Conflict", id);
                return TaskControllerMS.Delete.Conflict;
            }

            return string.Empty;
        }

        // f01-workflow.md T5 endpoint 3: always five columns in lifecycle
        // order, empty ones present with zero totals. F01 ships CreatedAt
        // asc, Id asc and 50/column; F03 changes the order-by and the page
        // sizes later, additive on this same shape.
        public async Task<BoardResDto> GetBoard(long actorUserId, string[] callerPermissions)
        {
            var board = new BoardResDto();

            foreach (var status in BoardColumnOrder)
            {
                var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Status == status);
                query = query.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id);

                var totalItems = await query.CountAsync();
                var pageItems = await query.Take(BoardColumnPageSize).ToListAsync();
                var items = await BuildSummariesAsync(pageItems, actorUserId, callerPermissions);

                board.Columns.Add(new BoardColumnResDto
                {
                    Status = status,
                    Items = items,
                    TotalItems = totalItems,
                    CurrentCount = items.Count,
                    PageSize = BoardColumnPageSize,
                });
            }

            return board;
        }

        // f01-workflow.md T5 endpoint 4: page 2+ of one column, same
        // CreatedAt asc, Id asc ordering as the board (F03 changes the
        // order-by later, additive).
        public async Task<Pagination<TaskSummaryResDto>> GetList(TaskWorkflowStatus? status, int start, int length, long actorUserId, string[] callerPermissions)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryAll();

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            query = query.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id);

            var page = await Pagination<TaskItem>.CreateAsync(query, start, length);
            var items = await BuildSummariesAsync(page.Items, actorUserId, callerPermissions);

            return new Pagination<TaskSummaryResDto>
            {
                PageNumber = page.PageNumber,
                PageSize = page.PageSize,
                TotalItems = page.TotalItems,
                TotalPages = page.TotalPages,
                Items = items,
            };
        }

        private async Task<List<TaskSummaryResDto>> BuildSummariesAsync(List<TaskItem> tasks, long actorUserId, string[] callerPermissions)
        {
            var assigneeIds = tasks.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value).Distinct().ToList();
            var userNames = new Dictionary<long, string?>();
            if (assigneeIds.Count > 0)
            {
                var users = await unitOfWork.GetRepository<User>().QueryCondition(u => assigneeIds.Contains(u.Id));
                userNames = await users.ToDictionaryAsync(u => u.Id, u => u.UserName);
            }

            return [.. tasks.Select(task => new TaskSummaryResDto
            {
                Id = task.Id,
                Title = task.Title,
                Status = task.Status,
                Priority = task.Priority,
                AssigneeId = task.AssigneeId,
                AssigneeUserName = task.AssigneeId.HasValue && userNames.TryGetValue(task.AssigneeId.Value, out var name) ? name : null,
                DueDate = task.DueDate,
                IsOverdue = task.IsOverdue,
                CompletedAt = task.CompletedAt,
                Version = task.Version,
                AllowedTransitions = BuildAllowedTransitions(task, actorUserId, callerPermissions),
            })];
        }

        private static List<AllowedTransitionResDto> BuildAllowedTransitions(TaskItem task, long actorUserId, string[] callerPermissions)
        {
            return [.. TaskTransitions
                .AllowedFor(task.Status, task.AssigneeId, actorUserId, callerPermissions)
                .Select(edge => new AllowedTransitionResDto
                {
                    Status = edge.To.ToString(),
                    Operation = edge.Operation,
                    RequiresReason = edge.RequiresReason,
                })];
        }

        private DateOnly TodayInZone()
        {
            return DateOnly.FromDateTime(DateTimeHelper.GetDtOffset().ToTimezone(_timeZone).Date);
        }

        private static TaskDetailResDto BuildDetail(TaskItem task, string? assigneeUserName, long actorUserId, string[] callerPermissions)
        {
            return new TaskDetailResDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                AssigneeId = task.AssigneeId,
                AssigneeUserName = assigneeUserName,
                DueDate = task.DueDate,
                IsOverdue = task.IsOverdue,
                CompletedAt = task.CompletedAt,
                Version = task.Version,
                AllowedTransitions = BuildAllowedTransitions(task, actorUserId, callerPermissions),
                CreatedAt = task.CreatedAt,
                CreatedBy = task.CreatedBy,
                UpdatedAt = task.UpdatedAt,
            };
        }
    }
}
