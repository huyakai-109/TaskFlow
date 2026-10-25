using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.DataAccess.IEntities;
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.DataAccess.Entities
{
    [Table("Roles")]
    public class Role : IdentityRole<long>, IBaseEntity
    {
        [MaxLength(100)]
        public string DisplayName { get; set; }

        public virtual ICollection<RoleClaim> RoleClaims { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
