using Microsoft.AspNetCore.Http;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.Repositories;
using TaskFlow.Repositories.UoW;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.DataAccess
{
    // Pins the EF Core/Pomelo behaviour F02's AC17 (soft-deleted assignee id)
    // depends on: BaseRepository<User>.FindById (a DbSet.FindAsync primary-
    // key lookup) must still honour UserConfiguration's
    // HasQueryFilter(!IsDeleted). EF's documented Find/FindAsync behaviour
    // does not apply global query filters in every version/provider
    // combination, so this is pinned, not assumed. Promoted from a
    // throwaway probe run during slice 3.1 review (docs/reports/slice-3.1.md)
    // to a permanent regression test, so a future EF Core/Pomelo upgrade that
    // silently changes this surfaces here instead of as a live AC17 defect
    // (a soft-deleted user resolving to a valid assignee).
    [Collection(DatabaseCollection.Name)]
    public class UserRepositorySoftDeleteTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;

        public UserRepositorySoftDeleteTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task FindById_ReturnsNull_ForASoftDeletedUser_FromAFreshContext()
        {
            // Arrange: seed a user directly (precedent: ExampleSoftDeleteTests),
            // then soft-delete it the way the application does - Delete()
            // through UnitOfWork, which SaveChangesInternal converts into
            // IsDeleted = true (CLAUDE.md "Soft delete") - never the field set
            // by hand.
            await using var seedContext = _fixture.CreateContext();
            var userName = $"softdelete-{Guid.NewGuid():N}";
            var user = new User
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@taskflow.test",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            };
            seedContext.Add(user);
            await seedContext.SaveChangesAsync();
            var userId = user.Id;

            await using var workContext = _fixture.CreateContext();
            var unitOfWork = new UnitOfWork(workContext, new HttpContextAccessor());
            var toDelete = await unitOfWork.GetRepository<User>().FindById(userId);
            Assert.NotNull(toDelete);

            // Act
            await unitOfWork.GetRepository<User>().Delete(toDelete!);
            await unitOfWork.SaveChanges();

            // Assert - a completely fresh context, so nothing is already
            // tracked and the lookup must go through the query filter.
            await using var freshContext = _fixture.CreateContext();
            var freshRepository = new BaseRepository<User>(freshContext);
            var found = await freshRepository.FindById(userId);

            Assert.Null(found);
        }
    }
}
