using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // f07-presence.md: hub integration tests against the real Program.cs,
    // using the test-only Microsoft.AspNetCore.SignalR.Client dependency
    // (approved for R2-7). LongPolling, not WebSockets: TestServer's
    // in-memory transport doesn't support a real WebSocket upgrade.
    [Collection(DatabaseCollection.Name)]
    public class PresenceHubTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "R2Slice7HubTest@12345";

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public PresenceHubTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Join_Board_ReturnsASnapshotIncludingTheCaller()
        {
            var (token, userId) = await LoginAndGetTokenAsync();
            await using var connection = BuildConnection(token);
            await connection.StartAsync();

            var viewers = await connection.InvokeAsync<List<PresenceViewerDto>>("Join", "board");

            Assert.Contains(viewers, v => v.UserId == userId);
        }

        [Fact]
        public async Task Join_InvalidScope_ThrowsHubException()
        {
            var (token, _) = await LoginAndGetTokenAsync();
            await using var connection = BuildConnection(token);
            await connection.StartAsync();

            var ex = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(
                () => connection.InvokeAsync<List<PresenceViewerDto>>("Join", "not-a-valid-scope"));

            Assert.Contains("Presence.Join.InvalidScope", ex.Message);
        }

        [Fact]
        public async Task Join_FifthScope_ThrowsTooManyScopes()
        {
            var (token, _) = await LoginAndGetTokenAsync();
            await using var connection = BuildConnection(token);
            await connection.StartAsync();

            await connection.InvokeAsync<List<PresenceViewerDto>>("Join", "board");
            await connection.InvokeAsync<List<PresenceViewerDto>>("Join", "task:1");
            await connection.InvokeAsync<List<PresenceViewerDto>>("Join", "task:2");
            await connection.InvokeAsync<List<PresenceViewerDto>>("Join", "task:3");

            var ex = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(
                () => connection.InvokeAsync<List<PresenceViewerDto>>("Join", "task:4"));

            Assert.Contains("Presence.Join.TooManyScopes", ex.Message);
        }

        [Fact]
        public async Task Connect_WithoutAnyToken_IsRefused()
        {
            await using var connection = BuildConnection(accessToken: null);

            await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        }

        [Fact]
        public async Task Deactivate_AbortsTheUsersLiveConnection_ViaUsersController()
        {
            var admin = await LoginAndGetTokenAsync(roleName: "Admin");
            var (memberToken, memberUserId) = await LoginAndGetTokenAsync(roleName: "Member");

            await using var connection = BuildConnection(memberToken);
            var closedTcs = new TaskCompletionSource();
            connection.Closed += _ =>
            {
                closedTcs.TrySetResult();
                return Task.CompletedTask;
            };

            await connection.StartAsync();
            await connection.InvokeAsync<List<PresenceViewerDto>>("Join", "board");

            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);
            var deactivateResponse = await adminClient.PutAsync($"api/v1/users/{memberUserId}/deactivate", null);
            Assert.True(deactivateResponse.IsSuccessStatusCode);

            var closed = await Task.WhenAny(closedTcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(closed == closedTcs.Task, "expected the presence connection to close after deactivation");

            // Reactivate so this test leaves no deactivated user behind for
            // any other test in the same run.
            await adminClient.PutAsync($"api/v1/users/{memberUserId}/activate", null);
        }

        private HubConnection BuildConnection(string? accessToken)
        {
            var server = _factory.Server;

            return new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/presence", options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    if (accessToken != null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    }
                })
                .Build();
        }

        private async Task<(string Token, long UserId)> LoginAndGetTokenAsync(string roleName = "Member")
        {
            var userName = $"r2s7hub-{Guid.NewGuid():N}";

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
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid = Guid.NewGuid(), username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, $"login failed: {loginBody?.Error}");

            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByNameAsync(userName);
                return (loginBody!.Result!.Token!, user!.Id);
            }
        }

        // Shape mirrors TaskFlow.Api.Presence.PresenceViewer - this test
        // project cannot reference the Api project's internal record for
        // deserialization convenience beyond what SignalR's client already
        // needs, so it declares its own matching DTO.
        private class PresenceViewerDto
        {
            public long UserId { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string? DisplayName { get; set; }

            public string? AvatarUrl { get; set; }
        }
    }
}
