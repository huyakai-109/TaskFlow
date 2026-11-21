using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Reports
{
    // f09-reports.md "Technical contract" endpoint 1: userId absent = team
    // scope (checked in the service, R1).
    public class ReportScorecardReq
    {
        [Range(1, long.MaxValue)]
        public long? UserId { get; set; }

        [Required]
        public DateOnly? From { get; set; }

        [Required]
        public DateOnly? To { get; set; }

        [EnumDataType(typeof(ReportGranularity))]
        public ReportGranularity Granularity { get; set; } = ReportGranularity.Week;
    }
}
