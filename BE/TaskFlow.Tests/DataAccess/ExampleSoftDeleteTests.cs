using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.DataAccess
{
    // R4: UnitOfWork.SaveChanges() must not let EF emit a real DELETE for an
    // IIsDeletedEntity; it must always soft-delete instead.
    [Collection(DatabaseCollection.Name)]
    public class ExampleSoftDeleteTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;

        public ExampleSoftDeleteTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync()
        {
            return _fixture.ResetTablesAsync("Examples");
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task SaveChanges_SoftDeletesExample_WhenCommitOnlyContainsADelete()
        {
            // Arrange
            await using var seedContext = _fixture.CreateContext();
            var example = new Example
            {
                Name = "Example to delete",
                Description = "Seeded directly through MyDbContext",
                DateOfBirth = DateTimeOffset.UtcNow,
            };
            seedContext.Add(example);
            await seedContext.SaveChangesAsync();
            var exampleId = example.Id;

            await using var workContext = _fixture.CreateContext();
            var unitOfWork = new UnitOfWork(workContext, new HttpContextAccessor());
            var entityToDelete = await workContext.Set<Example>().SingleAsync(e => e.Id == exampleId);

            // Act
            await unitOfWork.GetRepository<Example>().Delete(entityToDelete);
            await unitOfWork.SaveChanges();

            // Assert
            await using var assertContext = _fixture.CreateContext();
            var persisted = await assertContext.Set<Example>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == exampleId);

            Assert.NotNull(persisted);
            Assert.True(persisted!.IsDeleted);
        }

        [Fact]
        public async Task SaveChanges_PersistsAddedEntity_WhenCommitOnlyContainsAnAdd()
        {
            // Arrange
            await using var workContext = _fixture.CreateContext();
            var unitOfWork = new UnitOfWork(workContext, new HttpContextAccessor());
            var example = new Example
            {
                Name = "Example to add",
                Description = "Added-only commit, regression for the existing path",
                DateOfBirth = DateTimeOffset.UtcNow,
            };

            // Act
            await unitOfWork.GetRepository<Example>().Add(example);
            await unitOfWork.SaveChanges();

            // Assert
            await using var assertContext = _fixture.CreateContext();
            var persisted = await assertContext.Set<Example>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == example.Id);

            Assert.NotNull(persisted);
            Assert.False(persisted!.IsDeleted);
        }

        [Fact]
        public async Task SaveChanges_AddsOneAndSoftDeletesTheOther_WhenCommitMixesAddAndDelete()
        {
            // Arrange
            await using var seedContext = _fixture.CreateContext();
            var existing = new Example
            {
                Name = "Existing example",
                Description = "Seeded directly through MyDbContext",
                DateOfBirth = DateTimeOffset.UtcNow,
            };
            seedContext.Add(existing);
            await seedContext.SaveChangesAsync();
            var existingId = existing.Id;

            await using var workContext = _fixture.CreateContext();
            var unitOfWork = new UnitOfWork(workContext, new HttpContextAccessor());
            var entityToDelete = await workContext.Set<Example>().SingleAsync(e => e.Id == existingId);
            var newExample = new Example
            {
                Name = "New example",
                Description = "Added in the same commit as the delete",
                DateOfBirth = DateTimeOffset.UtcNow,
            };

            // Act
            await unitOfWork.GetRepository<Example>().Add(newExample);
            await unitOfWork.GetRepository<Example>().Delete(entityToDelete);
            await unitOfWork.SaveChanges();

            // Assert
            await using var assertContext = _fixture.CreateContext();
            var deleted = await assertContext.Set<Example>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == existingId);
            var added = await assertContext.Set<Example>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == newExample.Id);

            Assert.NotNull(deleted);
            Assert.True(deleted!.IsDeleted);
            Assert.NotNull(added);
            Assert.False(added!.IsDeleted);
        }
    }
}
