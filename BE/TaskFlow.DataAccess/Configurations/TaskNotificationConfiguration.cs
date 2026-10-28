using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class TaskNotificationConfiguration : IEntityTypeConfiguration<TaskNotification>
    {
        public void Configure(EntityTypeBuilder<TaskNotification> builder)
        {
            builder
                .HasOne<Entities.User>()
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // No navigation property, same reasoning as TaskHistoryConfiguration.
            builder
                .HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(n => n.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            // No index beyond EF's automatic FK indexes: the contract names
            // none for this table, and the inbox read query lands in slice 1.3.
        }
    }
}
