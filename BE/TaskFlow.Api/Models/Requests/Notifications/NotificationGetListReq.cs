using TaskFlow.Common.Constants;

namespace TaskFlow.Api.Models.Requests.Notifications
{
    // start/length follow the paging vocabulary pinned in f01-workflow.md T5:
    // start is a 1-based page number, length is the page size (max 100,
    // clamped by Pagination<T>.CreateAsync - no attribute needed here).
    public class NotificationGetListReq
    {
        public bool? IsRead { get; set; }

        public int Start { get; set; } = GlobalConstants.PageConfig.Start;

        public int Length { get; set; } = GlobalConstants.PageConfig.Length;
    }
}
