using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistory>
    {
        public void Configure(EntityTypeBuilder<TaskHistory> builder)
        {
            builder.Property(h => h.FromValue).HasMaxLength(100);
            builder.Property(h => h.ToValue).HasMaxLength(100);
            builder.Property(h => h.Reason).HasMaxLength(500);

            // No navigation property: a required navigation from this
            // unfiltered dependent to the filtered TaskItem would raise EF's
            // RequiredNavigationWithQueryFilterWarning and hide a deleted
            // task's history, which F04 AC20 forbids. Reads use explicit joins.
            builder
                .HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(h => h.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            builder
                .HasOne<Entities.User>()
                .WithMany()
                .HasForeignKey(h => h.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(h => new { h.TaskId, h.CreatedAt, h.Id })
                .HasDatabaseName("IX_TaskHistories_TaskId_CreatedAt_Id");
        }
    }
}
