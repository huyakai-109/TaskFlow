using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Profile
{
    public class ProfileUpdateReq
    {
        [MaxLength(100)]
        public string? DisplayName { get; set; }
    }
}
