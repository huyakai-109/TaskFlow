using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Profile
{
    public class ChangePasswordReq
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;
    }
}
