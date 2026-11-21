using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Activity
{
    public class ActivityGetListReq
    {
        [MaxLength(100)]
        public string? Cursor { get; set; }

        [Range(1, 100)]
        public int Length { get; set; } = 30;

        [RegularExpression("^(system|[1-9][0-9]{0,18})$")]
        public string? Actor { get; set; }

        public DateOnly? From { get; set; }

        public DateOnly? To { get; set; }

        public List<TaskHistoryEvent>? Events { get; set; }
    }
}
