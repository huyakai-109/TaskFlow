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
using MySqlConnector;

namespace TaskFlow.BusinessLogic.Services
{
    public interface ITaskService
    {
        Task<(string ErrorCode, TaskDetailResDto? Task)> Create(TaskCreateReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Get(long id, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Move(long id, TaskMoveReqDto requestDto, long actorUserId, string[] callerPermissions);
    }

    public class TaskService(
        IConfiguration configuration,
        IUnitOfWork unitOfWork,
        ILogger<TaskService> logger) : ITaskService
    {
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
            catch (Exception ex) when (IsDeadlock(ex))
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

        private static bool IsDeadlock(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is MySqlException { ErrorCode: MySqlErrorCode.LockDeadlock })
                {
                    return true;
                }
            }

            return false;
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
                AllowedTransitions = [.. TaskTransitions
                    .AllowedFor(task.Status, task.AssigneeId, actorUserId, callerPermissions)
                    .Select(edge => new AllowedTransitionResDto
                    {
                        Status = edge.To.ToString(),
                        Operation = edge.Operation,
                        RequiresReason = edge.RequiresReason,
                    })],
                CreatedAt = task.CreatedAt,
                CreatedBy = task.CreatedBy,
                UpdatedAt = task.UpdatedAt,
            };
        }
    }
}
