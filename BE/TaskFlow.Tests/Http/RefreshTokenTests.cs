using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // R3: AuthService.RefreshToken must (1) keep the RolePolicy claims a fresh
    // login would carry, (2) scope the UserToken lookup to the caller's own
    // user - not DeviceGuid alone, and (3) validate the old access token's
    // signature/issuer/audience explicitly, failing closed instead of letting
    // a tampered token reach the controller's generic try/catch.
    [Collection(DatabaseCollection.Name)]
    public class RefreshTokenTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string DevPassword = "R3Test@12345";

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public RefreshTokenTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
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
        public async Task RefreshToken_KeepsRolePolicyClaimsAndPermissions_WhenAdminTokenIsRefreshed()
        {
            // Arrange
            var client = CreateClient();
            var deviceGuid = Guid.NewGuid();

            var (loginBody, refreshCookie) = await LoginAsync(client, deviceGuid, "admin", "Admin!1234");
            Assert.True(loginBody.Success);
            Assert.NotEmpty(loginBody.Result!.Permissions);

            // Act
            var (refreshBody, _) = await RefreshAsync(client, deviceGuid, loginBody.Result!.Token!, refreshCookie);

            // Assert
            Assert.True(refreshBody.Success);
            Assert.NotEmpty(refreshBody.Result!.Permissions);

            var refreshedJwt = new JwtSecurityTokenHandler().ReadJwtToken(refreshBody.Result!.Token);
            Assert.Contains(refreshedJwt.Claims, c => c.Type == RolePolicies.ClaimType);
        }

        [Fact]
        public async Task RefreshToken_Refuses_WhenUserTokenRowBelongsToADifferentUser()
        {
            // Arrange
            var client = CreateClient();
            await EnsureUserAsync("r3-bug2-a");
            await EnsureUserAsync("r3-bug2-b");

            var deviceGuidA = Guid.NewGuid();
            var deviceGuidB = Guid.NewGuid();

            var (_, refreshCookieA) = await LoginAsync(client, deviceGuidA, "r3-bug2-a", DevPassword);
            var (loginB, refreshCookieB) = await LoginAsync(client, deviceGuidB, "r3-bug2-b", DevPassword);
            Assert.NotEmpty(refreshCookieA);

            // Simulate a UserToken row for device A whose stored refresh token value
            // collides with user B's actual refresh token - the DeviceGuid-only
            // lookup (pre-fix) would accept this and refresh as user B anyway.
            await using (var context = _dbFixture.CreateContext())
            {
                var rowA = await context.Set<UserToken>().SingleAsync(t => t.DeviceGuid == deviceGuidA);
                rowA.RefreshToken = refreshCookieB;
                await context.SaveChangesAsync();
            }

            // Act: device A, but the access+refresh token pair belongs to user B.
            var (refreshResult, _) = await RefreshAsync(client, deviceGuidA, loginB.Result!.Token!, refreshCookieB);

            // Assert
            Assert.False(refreshResult.Success);
        }

        [Fact]
        public async Task RefreshToken_Refuses_WhenAccessTokenSignatureIsTampered()
        {
            // Arrange
            var client = CreateClient();
            var deviceGuid = Guid.NewGuid();

            var (loginBody, refreshCookie) = await LoginAsync(client, deviceGuid, "admin", "Admin!1234");
            var tamperedToken = TamperSignature(loginBody.Result!.Token!);

            using var request = new HttpRequestMessage(HttpMethod.Put, "api/v1/auth/refresh")
            {
                Content = JsonContent.Create(new { deviceGuid, token = tamperedToken })
            };
            request.Headers.Add("Cookie", $"refreshToken={refreshCookie}");

            // Act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ResultRes<RefreshTokenRes>>();

            // Assert - a clean 400, never an unhandled exception surfacing as a 500.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(body);
            Assert.False(body!.Success);
        }

        private HttpClient CreateClient()
        {
            // Cookies are managed by hand in these tests (read Set-Cookie, craft the
            // next request's Cookie header) so a tampered/cross-user value can be
            // injected deliberately - the automatic cookie jar would get in the way.
            return _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
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

        private static async Task<(ResultRes<LoginRes> Body, string RefreshCookie)> LoginAsync(
            HttpClient client, Guid deviceGuid, string username, string password)
        {
            var response = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid, username, password });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(response.IsSuccessStatusCode, $"login failed: {body?.Error}");

            return (body!, ExtractSetCookieValue(response, "refreshToken"));
        }

        private static async Task<(ResultRes<RefreshTokenRes> Body, string? RefreshCookie)> RefreshAsync(
            HttpClient client, Guid deviceGuid, string token, string refreshCookie)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "api/v1/auth/refresh")
            {
                Content = JsonContent.Create(new { deviceGuid, token })
            };
            request.Headers.Add("Cookie", $"refreshToken={refreshCookie}");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ResultRes<RefreshTokenRes>>();

            var newCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? ExtractCookieValue(cookies, "refreshToken")
                : null;

            return (body!, newCookie);
        }

        private static string ExtractSetCookieValue(HttpResponseMessage response, string name)
        {
            Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "expected a Set-Cookie header");
            var value = ExtractCookieValue(cookies!, name);
            Assert.NotNull(value);
            return value!;
        }

        private static string? ExtractCookieValue(IEnumerable<string> cookies, string name)
        {
            var prefix = $"{name}=";
            var cookie = cookies.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.Ordinal));
            if (cookie == null)
            {
                return null;
            }

            var value = cookie[prefix.Length..];
            var semicolonIndex = value.IndexOf(';');
            return semicolonIndex >= 0 ? value[..semicolonIndex] : value;
        }

        private static string TamperSignature(string jwt)
        {
            var parts = jwt.Split('.');
            var signature = parts[2];
            var lastChar = signature[^1];
            var replacement = lastChar == 'A' ? 'B' : 'A';
            parts[2] = signature[..^1] + replacement;

            return string.Join('.', parts);
        }
    }
}
