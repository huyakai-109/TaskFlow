using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
    {
        public void Configure(EntityTypeBuilder<TaskItem> builder)
        {
            builder.Property(i => i.Title).HasMaxLength(200).IsRequired();
            builder.Property(i => i.Description).HasMaxLength(4000);
            builder.Property(i => i.Version).IsConcurrencyToken();

            builder
                .HasOne<Entities.User>()
                .WithMany()
                .HasForeignKey(i => i.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(i => !i.IsDeleted);

            // IsDeleted leads every composite: MySQL has no filtered indexes,
            // so the query filter appends "IsDeleted = 0" to every read.
            builder.HasIndex(i => new { i.IsDeleted, i.Status, i.Priority, i.DueDate })
                .HasDatabaseName("IX_Tasks_IsDeleted_Status_Priority_DueDate");

            builder.HasIndex(i => new { i.IsDeleted, i.Status, i.CompletedAt })
                .HasDatabaseName("IX_Tasks_IsDeleted_Status_CompletedAt");

            // f02-capacity.md Technical contract T3: the capacity count's
            // indexed query (AssigneeId == target && Status == InProgress),
            // IsDeleted leading per the same MySQL-has-no-filtered-indexes
            // convention as the composites above.
            builder.HasIndex(i => new { i.IsDeleted, i.AssigneeId, i.Status })
                .HasDatabaseName("IX_Tasks_IsDeleted_AssigneeId_Status");
        }
    }
}
