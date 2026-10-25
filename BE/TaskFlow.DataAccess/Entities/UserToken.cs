using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.DataAccess.IEntities;
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.DataAccess.Entities
{
    [Table("UserTokens")]
    public class UserToken : IdentityUserToken<long>, IBaseEntity
    {
        public Guid DeviceGuid { get; set; }

        [MaxLength(5000)]
        public string RefreshToken { get; set; }

        public DateTimeOffset RefreshTokenExpiryTime { get; set; }

        public DateTimeOffset LastLogin { get; set; }

        public virtual User User { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
