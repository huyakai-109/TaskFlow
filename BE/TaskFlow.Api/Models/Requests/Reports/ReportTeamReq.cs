using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Reports
{
    public class ReportTeamReq
    {
        [Required]
        public DateOnly? From { get; set; }

        [Required]
        public DateOnly? To { get; set; }
    }
}
