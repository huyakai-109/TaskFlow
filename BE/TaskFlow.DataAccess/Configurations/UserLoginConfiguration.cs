using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
    {
        public void Configure(EntityTypeBuilder<UserLogin> builder)
        {
            builder.HasKey(userLogin => new { userLogin.LoginProvider, userLogin.ProviderKey });
            builder.Property(userLogin => userLogin.LoginProvider).HasMaxLength(128);
            builder.Property(userLogin => userLogin.ProviderKey).HasMaxLength(128);

            builder
                .HasOne(userLogin => userLogin.User)
                .WithMany(user => user.UserLogins)
                .HasForeignKey(userLogin => userLogin.UserId);
        }
    }
}
