using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
    {
        public void Configure(EntityTypeBuilder<UserClaim> builder)
        {
            builder.HasKey(userClaim => userClaim.Id).HasName("UserClaimId");

            builder
                .HasOne(userClaim => userClaim.User)
                .WithMany(user => user.UserClaims)
                .HasForeignKey(userClaim => userClaim.UserId);
        }
    }
}
