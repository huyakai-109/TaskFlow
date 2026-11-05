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

            // f05-overdue.md Technical contract T3: serves the scan's
            // candidate query (equality on IsDeleted, IsOverdue = 0, then
            // Status, then a DueDate range) and the isOverdue=true list filter
            // (T8, prefix IsDeleted, IsOverdue = 1). Status != Done is a range
            // predicate in the scan, so it must come after IsOverdue (equality)
            // to keep narrowing - refines F01 T1's (IsDeleted, Status,
            // IsOverdue, DueDate) sketch, where Done tasks with past due dates
            // would otherwise accumulate in the index forever.
            builder.HasIndex(i => new { i.IsDeleted, i.IsOverdue, i.Status, i.DueDate })
                .HasDatabaseName("IX_Tasks_IsDeleted_IsOverdue_Status_DueDate");
        }
    }
}
