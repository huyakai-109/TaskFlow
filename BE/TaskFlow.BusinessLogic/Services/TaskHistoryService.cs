using TaskFlow.BusinessLogic.Dtos.Base;
using TaskFlow.BusinessLogic.Dtos.TaskHistories;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TaskFlow.BusinessLogic.Services
{
    public interface ITaskHistoryService
    {
        Task<(string ErrorCode, Pagination<TaskHistoryResDto>? Page)> GetList(long taskId, int start, int length);
    }

    public class TaskHistoryService(IConfiguration configuration, IUnitOfWork unitOfWork) : ITaskHistoryService
    {
        private readonly string _timeZone = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;

        // f04-audit.md T3: oldest first, ORDER BY CreatedAt, Id (AC18's
        // deterministic tie-break). The existence check and the actor join
        // both use IgnoreQueryFilters() - a soft-deleted task's trail still
        // reads (AC20) and an entry by a since-deleted user still resolves a
        // name - while a genuinely missing id still fails (AC19). No
        // repository method exposes IgnoreQueryFilters, so this is the one
        // read that goes through IUnitOfWork.DbContext directly rather than
        // GetRepository<T>().
        public async Task<(string ErrorCode, Pagination<TaskHistoryResDto>? Page)> GetList(long taskId, int start, int length)
        {
            var taskExists = await unitOfWork.DbContext.Set<TaskItem>()
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == taskId);

            if (!taskExists)
            {
                return (TaskHistoryControllerMS.GetList.TaskNotFound, null);
            }

            var histories = await unitOfWork.GetRepository<TaskHistory>().QueryAll();
            var users = unitOfWork.DbContext.Set<User>().IgnoreQueryFilters().AsNoTracking();

            var query =
                from h in histories
                where h.TaskId == taskId
                join u in users on h.ActorUserId equals (long?)u.Id into actorJoin
                from actor in actorJoin.DefaultIfEmpty()
                orderby h.CreatedAt, h.Id
                select new TaskHistoryRow { History = h, ActorName = actor != null ? actor.UserName : null };

            var page = await Pagination<TaskHistoryRow>.CreateAsync(query, start, length);

            // AssigneeChanged's FromValue/ToValue are user ids as text (T2);
            // resolving them to a display name for the message needs a
            // second batch lookup, since the actor join above only resolves
            // the acting user, not the assignees named in the entry's values.
            var assigneeIds = new List<long>();
            foreach (var row in page.Items)
            {
                if (row.History.Event != TaskHistoryEvent.AssigneeChanged)
                {
                    continue;
                }

                if (long.TryParse(row.History.FromValue, out var fromId))
                {
                    assigneeIds.Add(fromId);
                }

                if (long.TryParse(row.History.ToValue, out var toId))
                {
                    assigneeIds.Add(toId);
                }
            }

            var assigneeNames = new Dictionary<long, string?>();
            if (assigneeIds.Count > 0)
            {
                var distinctIds = assigneeIds.Distinct().ToList();
                assigneeNames = await unitOfWork.DbContext.Set<User>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(u => distinctIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName);
            }

            var items = page.Items.Select(row =>
            {
                string? fromAssigneeName = null;
                string? toAssigneeName = null;
                if (row.History.Event == TaskHistoryEvent.AssigneeChanged)
                {
                    fromAssigneeName = long.TryParse(row.History.FromValue, out var fromId) && assigneeNames.TryGetValue(fromId, out var fromName) ? fromName : null;
                    toAssigneeName = long.TryParse(row.History.ToValue, out var toId) && assigneeNames.TryGetValue(toId, out var toName) ? toName : null;
                }

                return BuildDto(row.History, row.ActorName, fromAssigneeName, toAssigneeName);
            }).ToList();

            return (string.Empty, new Pagination<TaskHistoryResDto>
            {
                PageNumber = page.PageNumber,
                PageSize = page.PageSize,
                TotalItems = page.TotalItems,
                TotalPages = page.TotalPages,
                Items = items,
            });
        }

        private TaskHistoryResDto BuildDto(TaskHistory history, string? actorName, string? fromAssigneeName, string? toAssigneeName)
        {
            var occurredAtLocal = history.CreatedAt.ToTimezone(_timeZone);

            return new TaskHistoryResDto
            {
                Id = history.Id,
                TaskId = history.TaskId,
                Event = history.Event,
                ActorUserId = history.ActorUserId,
                ActorName = actorName,
                FromValue = history.FromValue,
                ToValue = history.ToValue,
                Reason = history.Reason,
                OccurredAt = history.CreatedAt,
                OccurredAtLocal = occurredAtLocal,
                Message = TaskHistoryMessages.Build(history.Event, actorName, history.FromValue, history.ToValue, history.Reason, fromAssigneeName, toAssigneeName),
            };
        }

        // Join projection shape only - not returned past this service.
        private class TaskHistoryRow
        {
            public TaskHistory History { get; set; } = null!;

            public string? ActorName { get; set; }
        }
    }
}
