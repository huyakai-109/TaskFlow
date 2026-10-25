using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(role => role.Id).HasName("RoleId");
            builder.HasIndex(role => role.NormalizedName).HasDatabaseName("RoleNameIndex").IsUnique();
            builder.Property(role => role.ConcurrencyStamp).IsConcurrencyToken();
            builder.Property(role => role.Name).HasMaxLength(256);
            builder.Property(role => role.NormalizedName).HasMaxLength(256);

            builder.HasData(
            [
                CreateRole(RolePolicies.Admin.Id, RolePolicies.Admin.Name, RolePolicies.Admin.DisplayName, "005cf705-2e40-4e1c-8e73-afaec8ab2268"),
                CreateRole(RolePolicies.Manager.Id, RolePolicies.Manager.Name, RolePolicies.Manager.DisplayName, "7b0e5f3a-4c1d-4a8e-9b62-3d5f1c8a2e41"),
                CreateRole(RolePolicies.Member.Id, RolePolicies.Member.Name, RolePolicies.Member.DisplayName, "c94a2d17-6e83-4f05-a1b9-8e7d03f5b6c2"),
            ]);
        }

        private static Role CreateRole(long id, string code, string displayName, string concurrencyStamp) => new()
        {
            Id = id,
            Name = code,
            NormalizedName = code.ToUpper(),
            DisplayName = displayName,
            ConcurrencyStamp = concurrencyStamp,
            CreatedAt = new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            UpdatedAt = new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            CreatedBy = 1,
            UpdatedBy = 1,
        };
    }
}
