using Microsoft.EntityFrameworkCore;
using TaskFlow.BusinessLogic.Dtos.Base;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.DataAccess
{
    // Pagination<T>.CreateAsync (f01-workflow.md T5) - paged asynchronously
    // against a real EF query, unlike the existing synchronous constructors
    // (CountAsync/ToListAsync need a real EF query provider, not LINQ-to-objects).
    [Collection(DatabaseCollection.Name)]
    public class PaginationCreateAsyncTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;

        public PaginationCreateAsyncTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetTablesAsync("TaskHistories", "TaskNotifications", "Tasks");

            await using var context = _fixture.CreateContext();
            for (var i = 0; i < 5; i++)
            {
                context.Add(new TaskItem
                {
                    Title = $"Paged task {i}",
                    Status = TaskWorkflowStatus.Backlog,
                    Priority = TaskPriority.Normal,
                    Version = Guid.NewGuid(),
                });
            }

            await context.SaveChangesAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task CreateAsync_ReturnsFirstPage_WithCorrectTotalsAndItemCount()
        {
            // Arrange
            await using var context = _fixture.CreateContext();
            var query = context.Set<TaskItem>().AsNoTracking().OrderBy(t => t.Id);

            // Act
            var page = await Pagination<TaskItem>.CreateAsync(query, pageNumber: 1, pageSize: 2);

            // Assert
            Assert.Equal(5, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(1, page.PageNumber);
            Assert.Equal(2, page.PageSize);
            Assert.Equal(2, page.Items.Count);
        }

        [Fact]
        public async Task CreateAsync_ReturnsRemainder_OnLastPage()
        {
            // Arrange
            await using var context = _fixture.CreateContext();
            var query = context.Set<TaskItem>().AsNoTracking().OrderBy(t => t.Id);

            // Act
            var page = await Pagination<TaskItem>.CreateAsync(query, pageNumber: 3, pageSize: 2);

            // Assert
            Assert.Equal(5, page.TotalItems);
            Assert.Single(page.Items);
        }

        [Fact]
        public async Task CreateAsync_ClampsPageSize_ToMaxLength()
        {
            // Arrange
            await using var context = _fixture.CreateContext();
            var query = context.Set<TaskItem>().AsNoTracking().OrderBy(t => t.Id);

            // Act
            var page = await Pagination<TaskItem>.CreateAsync(query, pageNumber: 1, pageSize: 1000);

            // Assert
            Assert.Equal(100, page.PageSize);
        }
    }
}
