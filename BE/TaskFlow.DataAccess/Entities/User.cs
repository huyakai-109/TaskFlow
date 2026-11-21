using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.DataAccess.IEntities;
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.DataAccess.Entities
{
    [Table("Users")]
    public class User : IdentityUser<long>, IBaseEntity, IIsDeletedEntity
    {
        public bool IsDeleted { get; set; }

        // R2-3 (f08-accounts-profile.md "Data"), additive. Fallback to
        // UserName when null. AvatarObjectKey is the private-bucket MinIO
        // key, never shown to clients directly. DeactivatedAt null = active
        // - distinct from IsDeleted (soft delete, hides the name everywhere)
        // and from Identity lockout (failed-login-attempts mechanism).
        public string DisplayName { get; set; }

        public string AvatarObjectKey { get; set; }

        public DateTimeOffset? DeactivatedAt { get; set; }

        public bool MustChangePassword { get; set; }

        public virtual ICollection<UserClaim> UserClaims { get; set; }

        public virtual ICollection<UserLogin> UserLogins { get; set; }

        public virtual ICollection<UserToken> UserTokens { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; }

        public long CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
