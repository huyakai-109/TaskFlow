using TaskFlow.BusinessLogic.Dtos.Base;
using TaskFlow.BusinessLogic.Dtos.Notifications;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.BusinessLogic.Services
{
    public interface INotificationService
    {
        Task<Pagination<NotificationResDto>> GetList(long actorUserId, bool? isRead, int start, int length);

        Task<string> MarkRead(long id, long actorUserId);

        Task<int> MarkAllRead(long actorUserId, long? upToId);
    }

    public class NotificationService(IUnitOfWork unitOfWork) : INotificationService
    {
        // Own rows only (RecipientUserId == actorUserId), inner-joined to
        // Tasks - TaskItem's own IIsDeletedEntity query filter makes that join
        // an implicit "non-deleted tasks only" filter, so a notification for a
        // soft-deleted task disappears from the inbox without IgnoreQueryFilters
        // (f01-workflow.md T1, T5 endpoint 13). Newest first, id as a
        // deterministic tie-break.
        public async Task<Pagination<NotificationResDto>> GetList(long actorUserId, bool? isRead, int start, int length)
        {
            var notifications = await unitOfWork.GetRepository<TaskNotification>().QueryAll();
            var tasks = await unitOfWork.GetRepository<TaskItem>().QueryAll();

            var query =
                from n in notifications
                join t in tasks on n.TaskId equals t.Id
                where n.RecipientUserId == actorUserId
                select new NotificationRow { Notification = n, TaskId = t.Id, TaskTitle = t.Title };

            if (isRead.HasValue)
            {
                query = query.Where(row => row.Notification.IsRead == isRead.Value);
            }

            query = query.OrderByDescending(row => row.Notification.CreatedAt).ThenByDescending(row => row.Notification.Id);

            var page = await Pagination<NotificationRow>.CreateAsync(query, start, length);

            // Actor read through IgnoreQueryFilters, same reasoning as
            // TaskHistoryService: a since-deleted actor's name still renders
            // (f06-notification-history.md follows F04's precedent here).
            var actorIds = page.Items
                .Where(row => row.Notification.ActorUserId.HasValue)
                .Select(row => row.Notification.ActorUserId!.Value)
                .Distinct()
                .ToList();

            var actorNames = new Dictionary<long, string?>();
            if (actorIds.Count > 0)
            {
                actorNames = await unitOfWork.DbContext.Set<User>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(u => actorIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName);
            }

            return new Pagination<NotificationResDto>
            {
                PageNumber = page.PageNumber,
                PageSize = page.PageSize,
                TotalItems = page.TotalItems,
                TotalPages = page.TotalPages,
                Items = [.. page.Items.Select(row => BuildDto(row, actorNames))],
            };
        }

        // Idempotent (f01-workflow.md T5 endpoint 14): marking an already-read
        // notification again is a no-op success, not an error. Returns NotFound
        // - never a permission error - for another user's notification id, so
        // existence isn't leaked (T3).
        public async Task<string> MarkRead(long id, long actorUserId)
        {
            var query = await unitOfWork.GetRepository<TaskNotification>().QueryCondition(n => n.Id == id, disableTracking: false);
            var notification = query.FirstOrDefault();

            if (notification == null || notification.RecipientUserId != actorUserId)
            {
                return NotificationControllerMS.MarkRead.NotFound;
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTimeHelper.GetDtOffset();
                await unitOfWork.SaveChanges();
            }

            return string.Empty;
        }

        // f06-notification-history.md D4/B4: the caller's own unread rows up
        // to upToId (all if null), joined to non-deleted tasks, one
        // SaveChanges() for the whole batch - never one PUT per row.
        // Idempotent: a second call with nothing left to mark returns 0.
        public async Task<int> MarkAllRead(long actorUserId, long? upToId)
        {
            // Two round trips, not one join: joining this tracked query with
            // TaskItem's own no-tracking QueryAll() would make the whole
            // combined query no-tracking (EF Core applies AsNoTracking to
            // the query as a whole, not per joined source), and the rows
            // below would then never persist via SaveChanges().
            var notifications = await unitOfWork.GetRepository<TaskNotification>().QueryCondition(
                n => n.RecipientUserId == actorUserId && !n.IsRead && (!upToId.HasValue || n.Id <= upToId.Value),
                disableTracking: false);

            var rows = await notifications.ToListAsync();
            if (rows.Count == 0)
            {
                return 0;
            }

            var taskIds = rows.Select(r => r.TaskId).Distinct().ToList();
            var nonDeletedTaskIds = await (await unitOfWork.GetRepository<TaskItem>().QueryAll())
                .Where(t => taskIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync();
            var nonDeletedTaskIdSet = nonDeletedTaskIds.ToHashSet();

            var toMark = rows.Where(r => nonDeletedTaskIdSet.Contains(r.TaskId)).ToList();
            if (toMark.Count == 0)
            {
                return 0;
            }

            var now = DateTimeHelper.GetDtOffset();
            foreach (var row in toMark)
            {
                row.IsRead = true;
                row.ReadAt = now;
            }

            await unitOfWork.SaveChanges();

            return toMark.Count;
        }

        private static NotificationResDto BuildDto(NotificationRow row, Dictionary<long, string?> actorNames)
        {
            var notification = row.Notification;
            NotificationActorResDto? actor = null;
            if (notification.ActorUserId.HasValue
                && actorNames.TryGetValue(notification.ActorUserId.Value, out var actorName)
                && actorName != null)
            {
                actor = new NotificationActorResDto { Id = notification.ActorUserId.Value, UserName = actorName };
            }

            return new NotificationResDto
            {
                Id = notification.Id,
                TaskId = notification.TaskId,
                Event = notification.Event,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedAt = notification.CreatedAt,
                Task = new NotificationTaskResDto { Id = row.TaskId, Title = row.TaskTitle },
                Actor = actor,
            };
        }

        // Join projection shape only - not returned past this service.
        private class NotificationRow
        {
            public TaskNotification Notification { get; set; } = null!;

            public long TaskId { get; set; }

            public string TaskTitle { get; set; } = string.Empty;
        }
    }
}
