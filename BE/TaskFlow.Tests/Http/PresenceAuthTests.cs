using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // ADR 0004 condition 2 (f07-presence.md AC10): access_token is accepted
    // via query string only on hub paths - a normal HTTP route with no
    // Authorization header must still reject it.
    [Collection(DatabaseCollection.Name)]
    public class PresenceAuthTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "R2Slice7Test@12345";

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public PresenceAuthTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task NonHubRoute_WithAccessTokenOnlyInQueryString_IsRejectedAsUnauthenticated()
        {
            var token = await LoginAndGetTokenAsync();
            var client = _factory.CreateClient();

            // No Authorization header at all - only the query string, which
            // only a /hubs/* path is allowed to honor.
            var response = await client.GetAsync($"api/v1/users?access_token={token}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task NonHubRoute_WithAuthorizationHeader_StillWorks()
        {
            var token = await LoginAndGetTokenAsync();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("api/v1/users");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<string> LoginAndGetTokenAsync()
        {
            var userName = $"r2s7-{Guid.NewGuid():N}";

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
                var roleEntity = await context.Set<Role>().SingleAsync(r => r.Name == "Member");
                context.Add(new UserRole { UserId = user.Id, RoleId = roleEntity.Id });
                await context.SaveChangesAsync();
            }

            var client = _factory.CreateClient();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid = Guid.NewGuid(), username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, $"login failed: {loginBody?.Error}");

            return loginBody!.Result!.Token!;
        }
    }
}
