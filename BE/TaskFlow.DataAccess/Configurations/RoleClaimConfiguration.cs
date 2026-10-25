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

            // Each role owns a fixed id block, so appending a permission to one role never renumbers another role's seed rows.
            SeedRole(builder, RolePolicies.Admin.Id, 1, RolePolicies.Admin.AllowedPermissions);
            SeedRole(builder, RolePolicies.Manager.Id, 1001, RolePolicies.Manager.AllowedPermissions);
            SeedRole(builder, RolePolicies.Member.Id, 2001, RolePolicies.Member.AllowedPermissions);
        }

        private static void SeedRole(EntityTypeBuilder<RoleClaim> builder, long roleId, int firstId, string[] permissions)
        {
            var id = firstId;
            foreach (var claimValue in permissions)
            {
                builder.HasData([CreateRoleClaim(id++, roleId, claimValue)]);
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
