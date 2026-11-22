using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Users
{
    public class UserCreateReq
    {
        [Required]
        [RegularExpression(@"^[A-Za-z0-9._-]{3,50}$")]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DisplayName { get; set; }

        [Required]
        [EnumDataType(typeof(AccountRole))]
        public AccountRole Role { get; set; }
    }
}
