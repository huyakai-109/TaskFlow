using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });

            builder
                .HasOne(userRole => userRole.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(userRole => userRole.RoleId);

            builder
                .HasOne(userRole => userRole.User)
                .WithMany(user => user.UserRoles)
                .HasForeignKey(userRole => userRole.UserId);

            builder.HasData(
            [
                CreateUserRole(1, RolePolicies.Admin.Id),
            ]);
        }

        private static UserRole CreateUserRole(long userId, long roleId) => new()
        {
            UserId = userId,
            RoleId = roleId,
            CreatedAt = new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            UpdatedAt = new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            CreatedBy = 1,
            UpdatedBy = 1
        };
    }
}
