using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.DataAccess
{
    // SeedTaskPermissions migration: Admin and Manager get all 7 Tasks
    // permissions, Member gets 5 (no DeleteTasks, no ReopenTasks).
    [Collection(DatabaseCollection.Name)]
    public class TaskPermissionSeedTests
    {
        private readonly DatabaseFixture _fixture;

        public TaskPermissionSeedTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(RolePolicies.ClaimType, "Admin")]
        [InlineData(RolePolicies.ClaimType, "Manager")]
        public async Task RoleClaims_SeedsAllSevenTasksPermissions_ForAdminAndManager(string claimType, string roleName)
        {
            await AssertTasksPermissions(claimType, roleName, expectedCount: 7, expectDeleteAndReopen: true);
        }

        [Fact]
        public async Task RoleClaims_SeedsFiveTasksPermissions_ForMember_ExcludingDeleteAndReopen()
        {
            await AssertTasksPermissions(RolePolicies.ClaimType, "Member", expectedCount: 5, expectDeleteAndReopen: false);
        }

        private async Task AssertTasksPermissions(string claimType, string roleName, int expectedCount, bool expectDeleteAndReopen)
        {
            await using var context = _fixture.CreateContext();

            var role = await context.Set<Role>().SingleAsync(r => r.Name == roleName);
            var tasksPermissions = typeof(Permissions.Tasks)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToHashSet();

            var seededClaims = await context.Set<RoleClaim>()
                .Where(c => c.RoleId == role.Id && c.ClaimType == claimType && tasksPermissions.Contains(c.ClaimValue!))
                .Select(c => c.ClaimValue!)
                .ToListAsync();

            Assert.Equal(expectedCount, seededClaims.Count);
            Assert.Contains(Permissions.Tasks.ViewTasks, seededClaims);
            Assert.Contains(Permissions.Tasks.CreateTasks, seededClaims);
            Assert.Contains(Permissions.Tasks.UpdateTasks, seededClaims);
            Assert.Contains(Permissions.Tasks.MoveTasks, seededClaims);
            Assert.Contains(Permissions.Tasks.AssignTasks, seededClaims);

            if (expectDeleteAndReopen)
            {
                Assert.Contains(Permissions.Tasks.DeleteTasks, seededClaims);
                Assert.Contains(Permissions.Tasks.ReopenTasks, seededClaims);
            }
            else
            {
                Assert.DoesNotContain(Permissions.Tasks.DeleteTasks, seededClaims);
                Assert.DoesNotContain(Permissions.Tasks.ReopenTasks, seededClaims);
            }
        }
    }
}
