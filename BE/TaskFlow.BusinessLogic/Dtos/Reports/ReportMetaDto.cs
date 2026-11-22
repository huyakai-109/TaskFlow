using TaskFlow.Common.Enums;

namespace TaskFlow.BusinessLogic.Dtos.Reports
{
    public class ReportMetaDto
    {
        public string Scope { get; set; } = string.Empty;

        public long? UserId { get; set; }

        public DateOnly From { get; set; }

        public DateOnly To { get; set; }

        public ReportGranularity Granularity { get; set; }

        public string TimeZone { get; set; } = string.Empty;

        public DateOnly? DataAvailableSince { get; set; }

        public DateTimeOffset GeneratedAt { get; set; }

        public int SmallSampleThreshold { get; set; }

        public int InProgressLimit { get; set; }
    }

    public class ReportUserRefDto
    {
        public long Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; }
    }
}
