using TaskFlow.BusinessLogic.Dtos.Base;
using TaskFlow.BusinessLogic.Dtos.Notifications;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;

namespace TaskFlow.BusinessLogic.Services
{
    public interface INotificationService
    {
        Task<Pagination<NotificationResDto>> GetList(long actorUserId, bool? isRead, int start, int length);

        Task<string> MarkRead(long id, long actorUserId);
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
                select n;

            if (isRead.HasValue)
            {
                query = query.Where(n => n.IsRead == isRead.Value);
            }

            query = query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);

            var page = await Pagination<TaskNotification>.CreateAsync(query, start, length);

            return new Pagination<NotificationResDto>
            {
                PageNumber = page.PageNumber,
                PageSize = page.PageSize,
                TotalItems = page.TotalItems,
                TotalPages = page.TotalPages,
                Items = [.. page.Items.Select(BuildDto)],
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

        private static NotificationResDto BuildDto(TaskNotification notification)
        {
            return new NotificationResDto
            {
                Id = notification.Id,
                TaskId = notification.TaskId,
                Event = notification.Event,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedAt = notification.CreatedAt,
            };
        }
    }
}
