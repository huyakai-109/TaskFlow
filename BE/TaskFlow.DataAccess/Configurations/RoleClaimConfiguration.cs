using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
    {
        public void Configure(EntityTypeBuilder<RoleClaim> builder)
        {
            builder.HasKey(roleClaim => roleClaim.Id).HasName("RoleClaimId");

            builder
                .HasOne(roleClaim => roleClaim.Role)
                .WithMany(role => role.RoleClaims)
                .HasForeignKey(roleClaim => roleClaim.RoleId);

            var id = 1;
            foreach (var claimValue in RolePolicies.Admin.AllowedPermissions)
            {
                builder.HasData([CreateRoleClaim(id++, RolePolicies.Admin.Id, claimValue)]);
            }
        }

        private static RoleClaim CreateRoleClaim(int id, long roleId, string claimValue) => new()
        {
            Id = id,
            RoleId = roleId,
            ClaimType = RolePolicies.ClaimType,
            ClaimValue = claimValue,
            CreatedAt = new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            UpdatedAt = new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            CreatedBy = 1,
            UpdatedBy = 1,
        };
    }
}
