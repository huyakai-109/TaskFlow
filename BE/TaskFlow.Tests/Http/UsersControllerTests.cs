using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Users;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Slice 3.3: GET /users (f02-capacity.md T4, ROADMAP sign-off #6) - the
    // assignee picker's source list.
    [Collection(DatabaseCollection.Name)]
    public class UsersControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice33Test@12345";

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public UsersControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task GetList_ListsSeededUsers_WithTheirRole_OrderedByUserName()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var memberName = $"zz-member-{suffix}";
            var managerName = $"aa-manager-{suffix}";

            var member = await CreateUserAsync(memberName, "Member");
            var manager = await CreateUserAsync(managerName, "Manager");

            var caller = await CreateAuthenticatedClientAsync(memberName, member.Id);

            var response = await caller.GetAsync("api/v1/users");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<List<UserRes>>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);

            var memberEntry = Assert.Single(body.Result!, u => u.Id == member.Id);
            Assert.Equal(memberName, memberEntry.UserName);
            Assert.Equal("Member", memberEntry.Role);

            var managerEntry = Assert.Single(body.Result!, u => u.Id == manager.Id);
            Assert.Equal(managerName, managerEntry.UserName);
            Assert.Equal("Manager", managerEntry.Role);

            var managerIndex = body.Result!.IndexOf(managerEntry);
            var memberIndex = body.Result!.IndexOf(memberEntry);
            Assert.True(managerIndex < memberIndex, "expected 'aa-manager-...' to sort before 'zz-member-...'");
        }

        [Fact]
        public async Task GetList_ExcludesASoftDeletedUser()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var activeName = $"active-{suffix}";
            var deletedName = $"deleted-{suffix}";

            var activeUser = await CreateUserAsync(activeName, "Member");
            var deletedUser = await CreateUserAsync(deletedName, "Member");

            using (var scope = _factory.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TaskFlow.Repositories.UoW.IUnitOfWork>();
                var userRepo = unitOfWork.GetRepository<User>();
                var tracked = await userRepo.FindById(deletedUser.Id);
                await userRepo.Delete(tracked!);
                await unitOfWork.SaveChanges();
            }

            var caller = await CreateAuthenticatedClientAsync(activeName, activeUser.Id);

            var response = await caller.GetAsync("api/v1/users");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<List<UserRes>>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Contains(body.Result!, u => u.Id == activeUser.Id);
            Assert.DoesNotContain(body.Result!, u => u.Id == deletedUser.Id);
        }

        [Fact]
        public async Task GetList_Returns401_WhenAnonymous()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("api/v1/users");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---- Helpers ----

        private async Task<User> CreateUserAsync(string userName, string roleName)
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = userName,
                Email = $"{userName}@taskflow.test",
                EmailConfirmed = true,
                LockoutEnabled = true,
            };

            var createResult = await userManager.CreateAsync(user, TestPassword);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

            await using var context = _dbFixture.CreateContext();
            var roleEntity = await context.Set<Role>().SingleAsync(r => r.Name == roleName);
            context.Add(new UserRole { UserId = user.Id, RoleId = roleEntity.Id });
            await context.SaveChangesAsync();

            return user;
        }

        private async Task<HttpClient> CreateAuthenticatedClientAsync(string userName, long userId)
        {
            var client = _factory.CreateClient();
            var deviceGuid = Guid.NewGuid();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid, username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, $"login failed: {loginBody?.Error}");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Result!.Token);

            return client;
        }
    }
}
