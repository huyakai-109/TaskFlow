using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Bug: AuthService.Login looked up the existing UserToken row by DeviceGuid
    // alone. When a second, different user logged in from the same device, the
    // lookup found the first user's row and the code tried to mutate its
    // UserId - part of UserToken's (UserId, LoginProvider, Name) primary key -
    // which EF Core rejects, surfacing as an unhandled 500 through the
    // controller's generic try/catch. The lookup must scope by DeviceGuid AND
    // UserId, matching RefreshToken's existing pattern, so a different user on
    // the same device gets its own inserted row instead of colliding.
    [Collection(DatabaseCollection.Name)]
    public class LoginDeviceTokenTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string DevPassword = "LoginDeviceTest@12345";

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public LoginDeviceTokenTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("UserTokens");
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Login_Succeeds_WhenADifferentUserLogsInFromTheSameDevice()
        {
            // Arrange
            var client = _factory.CreateClient();
            await EnsureUserAsync("login-device-a");
            await EnsureUserAsync("login-device-b");

            var deviceGuid = Guid.NewGuid();

            var firstResponse = await client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { deviceGuid, username = "login-device-a", password = DevPassword });
            var firstBody = await firstResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.True(firstBody!.Success);

            // Act - a different user logs in from the same browser/device.
            var secondResponse = await client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { deviceGuid, username = "login-device-b", password = DevPassword });
            var secondBody = await secondResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            // Assert - must succeed, not surface the UserToken primary-key collision as a 500.
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.NotNull(secondBody);
            Assert.True(secondBody!.Success);
            Assert.NotNull(secondBody.Result?.Token);
        }

        private async Task EnsureUserAsync(string userName)
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            if (await userManager.FindByNameAsync(userName) != null)
            {
                return;
            }

            var user = new User
            {
                UserName = userName,
                Email = $"{userName}@taskflow.test",
                EmailConfirmed = true,
                LockoutEnabled = true,
            };

            var result = await userManager.CreateAsync(user, DevPassword);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
