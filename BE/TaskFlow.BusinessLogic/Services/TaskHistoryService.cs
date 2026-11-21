using TaskFlow.BusinessLogic.Dtos.Activity;
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

        Task<(string ErrorCode, ActivityFeedResultDto? Feed)> GetFeed(ActivityGetListReqDto request);
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

        // f04-activity-feed.md "Technical contract": keyset paging on
        // (CreatedAt, Id) descending (ADR 0007), newest first across every
        // task - R1/R2. Both the task and actor joins go through
        // IgnoreQueryFilters() (R4/AC4: a soft-deleted task's earlier
        // entries still appear, marked deleted; R6/AC5: a since-deleted
        // actor's name still resolves). No write path, no tracking (AC22).
        public async Task<(string ErrorCode, ActivityFeedResultDto? Feed)> GetFeed(ActivityGetListReqDto request)
        {
            if (request.From.HasValue && request.To.HasValue && request.From.Value > request.To.Value)
            {
                return (ActivityControllerMS.GetList.InvalidDateRange, null);
            }

            DateTimeOffset? cursorCreatedAt = null;
            long? cursorId = null;
            if (!string.IsNullOrEmpty(request.Cursor))
            {
                if (!ActivityCursor.TryDecode(request.Cursor, out var decodedCreatedAt, out var decodedId))
                {
                    return (ActivityControllerMS.GetList.InvalidCursor, null);
                }

                cursorCreatedAt = decodedCreatedAt;
                cursorId = decodedId;
            }

            var actorIsSystem = string.Equals(request.Actor, "system", StringComparison.Ordinal);
            long? actorUserId = !actorIsSystem && long.TryParse(request.Actor, out var parsedActorId) ? parsedActorId : null;

            // R7/AC13: [from 00:00 local, (to+1) 00:00 local), each bound
            // converted once to UTC in the configured zone.
            var fromUtc = request.From?.LocalStartOfDayToUtc(_timeZone);
            var toUtc = request.To?.AddDays(1).LocalStartOfDayToUtc(_timeZone);

            var histories = await unitOfWork.GetRepository<TaskHistory>().QueryAll();
            var tasks = unitOfWork.DbContext.Set<TaskItem>().IgnoreQueryFilters().AsNoTracking();
            var users = unitOfWork.DbContext.Set<User>().IgnoreQueryFilters().AsNoTracking();

            var query =
                from h in histories
                join t in tasks on h.TaskId equals t.Id
                join u in users on h.ActorUserId equals (long?)u.Id into actorJoin
                from actor in actorJoin.DefaultIfEmpty()
                select new ActivityRow
                {
                    History = h,
                    TaskTitle = t.Title,
                    TaskIsDeleted = t.IsDeleted,
                    ActorName = actor != null ? actor.UserName : null,
                };

            if (actorIsSystem)
            {
                query = query.Where(r => r.History.ActorUserId == null);
            }
            else if (actorUserId.HasValue)
            {
                query = query.Where(r => r.History.ActorUserId == actorUserId.Value);
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(r => r.History.CreatedAt >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(r => r.History.CreatedAt < toUtc.Value);
            }

            if (request.Events is { Count: > 0 })
            {
                query = query.Where(r => request.Events.Contains(r.History.Event));
            }

            // Keyset condition (R8/AC8): strictly older than the cursor row,
            // by the same (CreatedAt, Id) descending order the page is read in.
            if (cursorCreatedAt.HasValue && cursorId.HasValue)
            {
                query = query.Where(r =>
                    r.History.CreatedAt < cursorCreatedAt.Value
                    || (r.History.CreatedAt == cursorCreatedAt.Value && r.History.Id < cursorId.Value));
            }

            query = query.OrderByDescending(r => r.History.CreatedAt).ThenByDescending(r => r.History.Id);

            // length+1 rows read to compute hasMore without a second COUNT
            // query (AC7/AC18 use hasMore, never a total - ADR 0007).
            var rows = await query.Take(request.Length + 1).ToListAsync();
            var hasMore = rows.Count > request.Length;
            var page = rows.Take(request.Length).ToList();

            // One batched lookup of user names referenced by
            // AssigneeChanged values, same pattern as the per-task GetList.
            var assigneeIds = new List<long>();
            foreach (var row in page)
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

            var items = page.Select(row =>
            {
                string? fromAssigneeName = null;
                string? toAssigneeName = null;
                if (row.History.Event == TaskHistoryEvent.AssigneeChanged)
                {
                    fromAssigneeName = long.TryParse(row.History.FromValue, out var fromId) && assigneeNames.TryGetValue(fromId, out var fromName) ? fromName : null;
                    toAssigneeName = long.TryParse(row.History.ToValue, out var toId) && assigneeNames.TryGetValue(toId, out var toName) ? toName : null;
                }

                var occurredAtLocal = row.History.CreatedAt.ToTimezone(_timeZone);

                return new ActivityResDto
                {
                    Id = row.History.Id,
                    Event = row.History.Event,
                    TaskId = row.History.TaskId,
                    TaskTitle = row.TaskTitle,
                    TaskIsDeleted = row.TaskIsDeleted,
                    ActorUserId = row.History.ActorUserId,
                    ActorName = row.ActorName,
                    FromValue = row.History.FromValue,
                    ToValue = row.History.ToValue,
                    Reason = row.History.Reason,
                    OccurredAt = row.History.CreatedAt,
                    OccurredAtLocal = occurredAtLocal,
                    Message = TaskHistoryMessages.Build(row.History.Event, row.ActorName, row.History.FromValue, row.History.ToValue, row.History.Reason, fromAssigneeName, toAssigneeName),
                };
            }).ToList();

            var nextCursor = hasMore && page.Count > 0
                ? ActivityCursor.Encode(page[^1].History.CreatedAt, page[^1].History.Id)
                : null;

            return (string.Empty, new ActivityFeedResultDto
            {
                Items = items,
                NextCursor = nextCursor,
                HasMore = hasMore,
                PageSize = request.Length,
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

        // Join projection shape only - not returned past this service.
        private class ActivityRow
        {
            public TaskHistory History { get; set; } = null!;

            public string TaskTitle { get; set; } = string.Empty;

            public bool TaskIsDeleted { get; set; }

            public string? ActorName { get; set; }
        }
    }
}
