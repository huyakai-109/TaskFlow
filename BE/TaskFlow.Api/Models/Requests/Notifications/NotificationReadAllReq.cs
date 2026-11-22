using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Notifications
{
    // f06-notification-history.md API: upToId = the newest id the caller has
    // seen, so a notification that arrives while the popover is open stays
    // unread. Omitted means "all".
    public class NotificationReadAllReq
    {
        [Range(1, long.MaxValue)]
        public long? UpToId { get; set; }
    }
}
