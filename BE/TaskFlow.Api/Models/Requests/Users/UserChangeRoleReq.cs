using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Enums;

namespace TaskFlow.Api.Models.Requests.Users
{
    public class UserChangeRoleReq
    {
        [Required]
        [EnumDataType(typeof(AccountRole))]
        public AccountRole Role { get; set; }
    }
}
