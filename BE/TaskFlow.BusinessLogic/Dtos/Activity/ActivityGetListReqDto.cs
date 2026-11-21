using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Activity
{
    public class ActivityGetListReqDto
    {
        public string? Cursor { get; set; }

        public int Length { get; set; } = 30;

        // A user id as text, or "system" meaning ActorUserId IS NULL.
        public string? Actor { get; set; }

        public DateOnly? From { get; set; }

        public DateOnly? To { get; set; }

        public List<TaskHistoryEvent>? Events { get; set; }
    }
}
