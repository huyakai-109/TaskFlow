using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(role => role.Id).HasName("UserId");
            builder.HasIndex(user => user.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique();
            builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex");
            builder.Property(user => user.ConcurrencyStamp).IsConcurrencyToken();
            builder.Property(user => user.UserName).HasMaxLength(256);
            builder.Property(user => user.NormalizedUserName).HasMaxLength(256);
            builder.Property(user => user.Email).HasMaxLength(256);
            builder.Property(user => user.NormalizedEmail).HasMaxLength(256);

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasData(
            [
                CreateUser(1, "admin", "admin@imt-soft.com",
                    "AQAAAAEAACcQAAAAEELKNErj+EBVy3yZwAI32HSAQILEj5UAOooOEHTMPYU/yp0E28xNH1BjU/SEBw8kuA==", // Admin!1234
                    "616f1653-48e9-4a6f-81b3-1bdd52e565b5",
                    "ZY5BGSWBARTE74T6ZLO7WKKMMILBEB2E")
            ]);
        }

        private static User CreateUser(long id, string userName, string email, string passwordHash, string concurrencyStamp, string securityStamp) => new()
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpper(),
            Email = email,
            NormalizedEmail = email.ToUpper(),
            LockoutEnabled = true,
            PasswordHash = passwordHash,
            ConcurrencyStamp = concurrencyStamp,
            SecurityStamp = securityStamp,
            EmailConfirmed = true,
            CreatedAt = new DateTime(2023, 10, 09, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            UpdatedAt = new DateTime(2023, 10, 09, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(8363),
            CreatedBy = 1,
            UpdatedBy = 1
        };
    }
}
