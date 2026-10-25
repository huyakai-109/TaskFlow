using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.DataAccess.IEntities;
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.DataAccess.Entities
{
    [Table("UserClaims")]
    public class UserClaim : IdentityUserClaim<long>, IBaseEntity
    {
        public virtual User User { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
