using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Auth
{
    public class ChangeRequiredPasswordReq
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;
    }
}
