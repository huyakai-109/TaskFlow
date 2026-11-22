using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Reports
{
    public class ReportTasksReq
    {
        [Range(1, long.MaxValue)]
        public long? UserId { get; set; }

        [Required]
        public DateOnly? From { get; set; }

        [Required]
        public DateOnly? To { get; set; }

        [Required]
        [EnumDataType(typeof(ReportTaskMetric))]
        public ReportTaskMetric? Metric { get; set; }

        [Range(1, int.MaxValue)]
        public int Start { get; set; } = GlobalConstants.PageConfig.Start;

        [Range(1, 100)]
        public int Length { get; set; } = 30;
    }
}
