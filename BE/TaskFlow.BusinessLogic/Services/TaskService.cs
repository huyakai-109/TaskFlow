using TaskFlow.BusinessLogic.Dtos.Tasks;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.Extensions.Configuration;

namespace TaskFlow.BusinessLogic.Services
{
    public interface ITaskService
    {
        Task<(string ErrorCode, TaskDetailResDto? Task)> Create(TaskCreateReqDto requestDto, long actorUserId, string[] callerPermissions);

        Task<(string ErrorCode, TaskDetailResDto? Task)> Get(long id, long actorUserId, string[] callerPermissions);
    }

    public class TaskService(
        IConfiguration configuration,
        IUnitOfWork unitOfWork) : ITaskService
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
