using Microsoft.EntityFrameworkCore;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.DataAccess
{
    // Confirms the TaskNotifications table and its FKs to Tasks/Users exist
    // and work (slice 1.1 adds the entity now, ahead of the writer that lands
    // in slice 1.2's move endpoint).
    [Collection(DatabaseCollection.Name)]
    public class TaskNotificationTableTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;

        public TaskNotificationTableTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync()
        {
            return _fixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task TaskNotifications_PersistsAndReadsBack_WithValidTaskAndRecipientForeignKeys()
        {
            // Arrange
            await using var seedContext = _fixture.CreateContext();
            var task = new TaskItem
            {
                Title = "Needs rework",
                Status = TaskWorkflowStatus.InProgress,
                Priority = TaskPriority.Normal,
                Version = Guid.NewGuid(),
            };
            seedContext.Add(task);
            await seedContext.SaveChangesAsync();

            var adminId = 1L; // seeded admin user, present in every environment

            // Act
            await using var workContext = _fixture.CreateContext();
            var notification = new TaskNotification
            {
                RecipientUserId = adminId,
                TaskId = task.Id,
                Event = TaskNotificationEvent.ReturnedForRework,
                IsRead = false,
            };
            workContext.Add(notification);
            await workContext.SaveChangesAsync();

            // Assert
            await using var assertContext = _fixture.CreateContext();
            var persisted = await assertContext.Set<TaskNotification>()
                .AsNoTracking()
                .SingleOrDefaultAsync(n => n.Id == notification.Id);

            Assert.NotNull(persisted);
            Assert.Equal(task.Id, persisted!.TaskId);
            Assert.Equal(adminId, persisted.RecipientUserId);
            Assert.Equal(TaskNotificationEvent.ReturnedForRework, persisted.Event);
            Assert.False(persisted.IsRead);
        }

        [Fact]
        public async Task TaskNotifications_Rejects_WhenTaskIdDoesNotExist()
        {
            // Arrange
            await using var context = _fixture.CreateContext();
            var notification = new TaskNotification
            {
                RecipientUserId = 1,
                TaskId = 999_999,
                Event = TaskNotificationEvent.ReturnedForRework,
                IsRead = false,
            };
            context.Add(notification);

            // Act + Assert - FK_TaskNotifications_Tasks_TaskId refuses the insert.
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
