using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Reports
{
    public class ReportExportReq
    {
        [Range(1, long.MaxValue)]
        public long? UserId { get; set; }

        [Required]
        public DateOnly? From { get; set; }

        [Required]
        public DateOnly? To { get; set; }

        [EnumDataType(typeof(ReportGranularity))]
        public ReportGranularity Granularity { get; set; } = ReportGranularity.Week;

        [Required]
        [EnumDataType(typeof(ReportExportDataset))]
        public ReportExportDataset? Dataset { get; set; }
    }
}
