using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.DataAccess.IEntities;
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.DataAccess.Entities
{
    [Table("RoleClaims")]
    public class RoleClaim : IdentityRoleClaim<long>, IBaseEntity
    {
        public virtual Role Role { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
