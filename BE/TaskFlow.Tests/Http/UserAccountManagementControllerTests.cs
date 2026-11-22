using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Profile;
using TaskFlow.Api.Models.Responses.Users;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // R2-3 (f08-accounts-profile.md): admin account management (UsersController),
    // self profile (ProfileController) and the auth changes (deactivated,
    // MustChangePassword) that go with them.
    [Collection(DatabaseCollection.Name)]
    public class UserAccountManagementControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "R2Slice3Test@12345";

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public UserAccountManagementControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- Permission matrix: POST /users ----

        [Fact]
        public async Task Create_Returns403_ForMember()
        {
            var member = await CreateUserAndLoginAsync("Member");

            var response = await member.Client.PostAsJsonAsync("api/v1/users", new { userName = Unique("newuser"), role = "Member" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_Returns403_ForManager()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.PostAsJsonAsync("api/v1/users", new { userName = Unique("newuser"), role = "Member" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_Returns401_WhenAnonymous()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("api/v1/users", new { userName = Unique("newuser"), role = "Member" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_Succeeds_ForAdmin_AndReturnsOneTimeTemporaryPassword()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var userName = Unique("newuser");

            var response = await admin.Client.PostAsJsonAsync("api/v1/users", new { userName, displayName = "New Person", role = "Member" });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<UserCreatedRes>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal(userName, body.Result!.User.UserName);
            Assert.Equal("Member", body.Result.User.Role);
            Assert.True(body.Result.User.MustChangePassword);
            Assert.False(string.IsNullOrEmpty(body.Result.TemporaryPassword));
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }

        [Fact]
        public async Task Create_Returns_UserNameTaken_ForDuplicateUserName()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var userName = Unique("dupe");
            await admin.Client.PostAsJsonAsync("api/v1/users", new { userName, role = "Member" });

            var response = await admin.Client.PostAsJsonAsync("api/v1/users", new { userName, role = "Member" });
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(UserControllerMS.Create.UserNameTaken, body!.ErrorCode);
        }

        // ---- Last-admin guard ----
        //
        // ManageUsers is Admin-only (A4), so whoever calls these endpoints
        // is necessarily an active Admin distinct from the target unless
        // acting on themselves - the only way the guard's count (every
        // *other* active Admin) can reach zero is self-service: Deactivate's
        // LastAdmin branch is defensive (Self already intercepts the same
        // case first), and ChangeRole's self-demotion path is the one
        // reachable scenario, tested below. The seeded "admin" is
        // reactivated in `finally` either way: other test classes (e.g.
        // RefreshTokenTests) log in as it and must find it active again
        // regardless of this test's outcome or execution order.

        [Fact]
        public async Task ChangeRole_Returns_LastAdmin_WhenAnAdminDemotesThemself_AsTheOnlyActiveAdmin()
        {
            var admin1 = await CreateUserAndLoginAsync("Admin");

            // This test run accumulates other active Admin users from
            // earlier tests in this class (and the seeded "admin"), so
            // "admin1 is the only active Admin" has to be forced directly
            // in the database rather than assumed - deactivate every other
            // currently-active Admin for the duration of this test only,
            // and restore each one's exact prior DeactivatedAt in `finally`
            // regardless of outcome, since other tests (including ones that
            // already ran, and RefreshTokenTests' seeded-admin login) must
            // see them active again afterward.
            var othersToRestore = await DeactivateEveryOtherActiveAdminAsync(admin1.UserId);

            try
            {
                var response = await admin1.Client.PutAsJsonAsync($"api/v1/users/{admin1.UserId}/role", new { role = "Member" });
                var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(UserControllerMS.ChangeRole.LastAdmin, body!.ErrorCode);
            }
            finally
            {
                await RestoreDeactivatedAtAsync(othersToRestore);
            }
        }

        [Fact]
        public async Task ChangeRole_Succeeds_DemotingOneOfTwoActiveAdmins()
        {
            var admin1 = await CreateUserAndLoginAsync("Admin");
            var admin2 = await CreateUserAndLoginAsync("Admin");

            var response = await admin1.Client.PutAsJsonAsync($"api/v1/users/{admin2.UserId}/role", new { role = "Member" });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<UserAdminRes>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal("Member", body.Result!.Role);
        }

        // ---- Self-deactivate guard ----

        [Fact]
        public async Task Deactivate_Returns_Self_WhenAdminTargetsOwnAccount()
        {
            var admin = await CreateUserAndLoginAsync("Admin");

            var response = await admin.Client.PutAsync($"api/v1/users/{admin.UserId}/deactivate", null);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(UserControllerMS.Deactivate.Self, body!.ErrorCode);
        }

        // ---- Idempotency ----

        [Fact]
        public async Task Deactivate_IsIdempotent_WhenCalledTwice()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var target = await CreateUserAndLoginAsync("Member");

            var first = await admin.Client.PutAsync($"api/v1/users/{target.UserId}/deactivate", null);
            var second = await admin.Client.PutAsync($"api/v1/users/{target.UserId}/deactivate", null);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        [Fact]
        public async Task Activate_IsIdempotent_WhenCalledTwice()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var target = await CreateUserAndLoginAsync("Member");
            await admin.Client.PutAsync($"api/v1/users/{target.UserId}/deactivate", null);

            var first = await admin.Client.PutAsync($"api/v1/users/{target.UserId}/activate", null);
            var second = await admin.Client.PutAsync($"api/v1/users/{target.UserId}/activate", null);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        // ---- Deactivated users: listing, login, assignment ----

        [Fact]
        public async Task GetList_ListsADeactivatedUser_AsInactive()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var target = await CreateUserAndLoginAsync("Member");
            await admin.Client.PutAsync($"api/v1/users/{target.UserId}/deactivate", null);

            var response = await admin.Client.GetAsync("api/v1/users");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<List<UserRes>>>();

            var entry = Assert.Single(body!.Result!, u => u.Id == target.UserId);
            Assert.False(entry.IsActive);
        }

        [Fact]
        public async Task Login_Fails_ForADeactivatedUser_WithGenericInvalidCredential()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var target = await CreateUserAndLoginAsync("Member");
            await admin.Client.PutAsync($"api/v1/users/{target.UserId}/deactivate", null);

            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid = Guid.NewGuid(), username = target.UserName, password = TestPassword });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- MustChangePassword flow ----

        [Fact]
        public async Task Login_ReturnsPasswordChangeRequired_ForANewlyCreatedUser()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var userName = Unique("mustchange");
            var createResponse = await admin.Client.PostAsJsonAsync("api/v1/users", new { userName, role = "Member" });
            var createBody = await createResponse.Content.ReadFromJsonAsync<ResultRes<UserCreatedRes>>();

            var client = _factory.CreateClient();
            var deviceGuid = Guid.NewGuid();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid, username = userName, password = createBody!.Result!.TemporaryPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
            Assert.Equal(AuthControllerMS.Login.PasswordChangeRequired, loginBody!.ErrorCode);
        }

        [Fact]
        public async Task ChangeRequiredPassword_ThenLogin_Succeeds()
        {
            var admin = await CreateUserAndLoginAsync("Admin");
            var userName = Unique("mustchange2");
            var createResponse = await admin.Client.PostAsJsonAsync("api/v1/users", new { userName, role = "Member" });
            var createBody = await createResponse.Content.ReadFromJsonAsync<ResultRes<UserCreatedRes>>();

            var client = _factory.CreateClient();
            const string newPassword = "BrandNewPass@123";

            var changeResponse = await client.PostAsJsonAsync("api/v1/auth/password/change-required", new
            {
                userName,
                currentPassword = createBody!.Result!.TemporaryPassword,
                newPassword,
            });
            var changeBody = await changeResponse.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.True(changeResponse.IsSuccessStatusCode && changeBody!.Success, changeBody?.Error);

            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid = Guid.NewGuid(), username = userName, password = newPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode && loginBody!.Success, loginBody?.Error);
        }

        [Fact]
        public async Task ChangeRequiredPassword_Returns_InvalidCredential_WhenMustChangePasswordIsNotSet()
        {
            var existing = await CreateUserAndLoginAsync("Member");
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("api/v1/auth/password/change-required", new
            {
                userName = existing.UserName,
                currentPassword = TestPassword,
                newPassword = "SomethingElse@123",
            });
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(AuthControllerMS.ChangeRequired.InvalidCredential, body!.ErrorCode);
        }

        // ---- Self profile (ProfileController) ----

        [Fact]
        public async Task GetMe_ReturnsTheCallersOwnProfile()
        {
            var member = await CreateUserAndLoginAsync("Member");

            var response = await member.Client.GetAsync("api/v1/me");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<MeRes>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal(member.UserId, body.Result!.Id);
            Assert.Equal(member.UserName, body.Result.UserName);
        }

        [Fact]
        public async Task UpdateMe_ChangesOnlyTheCallersOwnDisplayName()
        {
            var member = await CreateUserAndLoginAsync("Member");
            var otherMember = await CreateUserAndLoginAsync("Member");

            var response = await member.Client.PutAsJsonAsync("api/v1/me", new { displayName = "My Display Name" });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<MeRes>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal("My Display Name", body.Result!.DisplayName);

            var otherResponse = await otherMember.Client.GetAsync("api/v1/me");
            var otherBody = await otherResponse.Content.ReadFromJsonAsync<ResultRes<MeRes>>();
            Assert.Null(otherBody!.Result!.DisplayName);
        }

        [Fact]
        public async Task UpdateMe_ClearsDisplayName_WhenBlank()
        {
            var member = await CreateUserAndLoginAsync("Member");
            await member.Client.PutAsJsonAsync("api/v1/me", new { displayName = "Something" });

            var response = await member.Client.PutAsJsonAsync("api/v1/me", new { displayName = "" });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<MeRes>>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Null(body.Result!.DisplayName);
        }

        [Fact]
        public async Task ChangePassword_Returns_WrongPassword_ForAnIncorrectCurrentPassword()
        {
            var member = await CreateUserAndLoginAsync("Member");

            var response = await member.Client.PutAsJsonAsync("api/v1/me/password", new { currentPassword = "WrongOne@123", newPassword = "NewOne@12345" });
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ProfileControllerMS.ChangePassword.WrongPassword, body!.ErrorCode);
        }

        [Fact]
        public async Task ChangePassword_Succeeds_WithTheCorrectCurrentPassword()
        {
            var member = await CreateUserAndLoginAsync("Member");
            const string newPassword = "NewOne@12345";

            var response = await member.Client.PutAsJsonAsync("api/v1/me/password", new { currentPassword = TestPassword, newPassword });
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);

            var loginResponse = await _factory.CreateClient().PostAsJsonAsync("api/v1/auth/login", new { deviceGuid = Guid.NewGuid(), username = member.UserName, password = newPassword });
            Assert.True(loginResponse.IsSuccessStatusCode);
        }

        // ---- Avatar validation (no MinIO reachable in this environment -
        // these exercise only the pure validation path, which returns
        // before any storage call; the success/StorageUnavailable paths are
        // NOT VERIFIED here, see handoff). ----

        [Fact]
        public async Task PutAvatar_Returns_UnsupportedType_ForAnSvgRenamedAsPng()
        {
            var member = await CreateUserAndLoginAsync("Member");

            using var content = new MultipartFormDataContent();
            var bytes = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "file", "avatar.png");

            var response = await member.Client.PutAsync("api/v1/me/avatar", content);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ProfileControllerMS.Avatar.UnsupportedType, body!.ErrorCode);
        }

        [Fact]
        public async Task PutAvatar_Returns_UnsupportedType_ForAnEmptyFile()
        {
            var member = await CreateUserAndLoginAsync("Member");

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent([]);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "file", "avatar.png");

            var response = await member.Client.PutAsync("api/v1/me/avatar", content);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ProfileControllerMS.Avatar.Empty, body!.ErrorCode);
        }

        [Fact]
        public async Task PutAvatar_DetectsPngByMagicBytes_EvenWithJpgExtension()
        {
            var member = await CreateUserAndLoginAsync("Member");

            using var content = new MultipartFormDataContent();
            byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
            var fileContent = new ByteArrayContent(pngHeader);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "file", "avatar.jpg");

            var response = await member.Client.PutAsync("api/v1/me/avatar", content);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            // Type sniffing passes (PNG by magic bytes despite the .jpg
            // name/content-type) - whatever happens next depends on a real
            // MinIO connection, NOT VERIFIED in this environment. The
            // assertion that matters here is that it is NOT rejected as
            // UnsupportedType/Empty.
            Assert.NotEqual(ProfileControllerMS.Avatar.UnsupportedType, body?.ErrorCode);
            Assert.NotEqual(ProfileControllerMS.Avatar.Empty, body?.ErrorCode);
        }

        // ---- Helpers ----

        private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 50)];

        // Direct-DB test setup/teardown (not through the API): every active
        // Admin other than `excludingUserId` is deactivated, and its prior
        // DeactivatedAt returned so the caller can restore it exactly.
        private async Task<List<(long UserId, DateTimeOffset? PriorDeactivatedAt)>> DeactivateEveryOtherActiveAdminAsync(long excludingUserId)
        {
            await using var context = _dbFixture.CreateContext();

            var otherActiveAdminIds = await (
                from ur in context.Set<UserRole>()
                join u in context.Set<User>() on ur.UserId equals u.Id
                where ur.RoleId == TaskFlow.Common.Constants.RolePolicies.Admin.Id
                    && u.Id != excludingUserId
                    && u.DeactivatedAt == null
                select u.Id).ToListAsync();

            var restoreList = new List<(long, DateTimeOffset?)>();
            foreach (var userId in otherActiveAdminIds)
            {
                var user = await context.Set<User>().SingleAsync(u => u.Id == userId);
                restoreList.Add((userId, user.DeactivatedAt));
                user.DeactivatedAt = DateTimeOffset.UtcNow;
            }

            await context.SaveChangesAsync();
            return restoreList;
        }

        private async Task RestoreDeactivatedAtAsync(List<(long UserId, DateTimeOffset? PriorDeactivatedAt)> entries)
        {
            await using var context = _dbFixture.CreateContext();
            foreach (var (userId, priorDeactivatedAt) in entries)
            {
                var user = await context.Set<User>().SingleAsync(u => u.Id == userId);
                user.DeactivatedAt = priorDeactivatedAt;
            }

            await context.SaveChangesAsync();
        }

        private async Task<(HttpClient Client, long UserId, string UserName)> CreateUserAndLoginAsync(string roleName)
        {
            var userName = Unique(roleName.ToLowerInvariant());

            using (var scope = _factory.Services.CreateScope())
            {
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
            }

            var client = _factory.CreateClient();
            var deviceGuid = Guid.NewGuid();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid, username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, $"login failed: {loginBody?.Error}");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Result!.Token);

            long userId = 0;
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByNameAsync(userName);
                userId = user!.Id;
            }

            return (client, userId, userName);
        }
    }
}
