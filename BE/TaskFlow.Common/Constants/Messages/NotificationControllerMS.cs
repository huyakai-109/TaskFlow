namespace TaskFlow.Common.Constants.Messages
{
    // f01-workflow.md T3/T5 endpoints 13-14. MarkRead returns NotFound - never
    // Forbidden - for another user's notification id, so existence isn't leaked.
    public static class NotificationControllerMS
    {
        public struct GetList
        {
            public const string Exception = "Notification.GetList.Exception";
        }

        public struct MarkRead
        {
            public const string NotFound = "Notification.MarkRead.NotFound";
            public const string Exception = "Notification.MarkRead.Exception";
        }
    }
}
