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

            // R2-2 (f06-notification-history.md "Data"): the actor who sent
            // the task back. No navigation property, same reasoning as above.
            builder
                .HasOne<Entities.User>()
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // R2-2: supports the bell's own list/count query (RecipientUserId
            // + IsRead filter, CreatedAt/Id order).
            builder.HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt, n.Id })
                .HasDatabaseName("IX_TaskNotifications_RecipientUserId_IsRead_CreatedAt_Id");
        }
    }
}
