using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Slice 1.1: POST /tasks and GET /tasks/{id}. Exercises F01 AC18 (Backlog),
    // AC24, AC26, and F04 AC1 (plus the DB-level substance of AC17/AC21, since
    // the history read endpoint itself lands in slice 1.4).
    [Collection(DatabaseCollection.Name)]
    public class TasksControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        // Program.cs registers JsonStringEnumConverter globally for the real
        // app; HttpClient's ReadFromJsonAsync doesn't pick that up on its own,
        // so responses carrying an enum (Status, Priority) need it mirrored here.
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            // Never reset Users/UserRoles/UserTokens here: they hold the
            // migration-seeded admin and other tests' fixtures within the same
            // shared "mydb_test" database (DatabaseCollection runs serially).
            // Each test below creates its own uniquely-named user instead.
            return _dbFixture.ResetTablesAsync("TaskHistories", "TaskNotifications", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Create_ReturnsBacklogTask_WithOneCreatedHistoryEntry_WhenRequestIsValid()
        {
            // Arrange
            var (client, userId) = await CreateAuthenticatedClientAsync("Member");

            // Act
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = "Fix the login redirect",
                priority = "Normal",
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            // Assert - response shape (F01 AC18 Backlog case)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body!.Success);
            Assert.Equal(TaskWorkflowStatus.Backlog, body.Result!.Status);
            Assert.NotEqual(Guid.Empty, body.Result.Version);
            var allowed = Assert.Single(body.Result.AllowedTransitions);
            Assert.Equal("Todo", allowed.Status);
            Assert.Equal("Move", allowed.Operation);

            // Assert - F04 AC1/AC17/AC21 (DB-level): exactly one Created entry,
            // its structured fields correct.
            await using var context = _dbFixture.CreateContext();
            var historyEntries = await context.Set<TaskHistory>()
                .Where(h => h.TaskId == body.Result.Id)
                .ToListAsync();

            var entry = Assert.Single(historyEntries);
            Assert.Equal(TaskHistoryEvent.Created, entry.Event);
            Assert.Equal(userId, entry.ActorUserId);
            Assert.Null(entry.FromValue);
            Assert.Null(entry.ToValue);
        }

        [Fact]
        public async Task Create_Refuses_WhenDueDateIsInThePast()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync("Member");

            // Act
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = "Backdated task",
                priority = "Normal",
                dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.Equal(TaskControllerMS.Create.DueDateInPast, body.ErrorCode);

            await using var context = _dbFixture.CreateContext();
            Assert.False(await context.Set<TaskFlow.DataAccess.Entities.TaskItem>().AnyAsync(t => t.Title == "Backdated task"));
        }

        [Fact]
        public async Task Create_Refuses_WhenAssigneeDoesNotExist()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync("Member");

            // Act
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = "Assign to nobody",
                priority = "Normal",
                assigneeId = 999_999,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.Equal(TaskControllerMS.Create.AssigneeNotFound, body.ErrorCode);
        }

        [Fact]
        public async Task Create_ReturnsShapeError_WhenPriorityIsOutOfRange()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync("Member");

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/tasks")
            {
                Content = JsonContent.Create(new { title = "Bad priority", priority = 999 })
            };

            // Act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            // Assert - AC15-style: refused at the boundary, before any business rule.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.NotEmpty(body.Validates!);
        }

        [Fact]
        public async Task Create_Refuses_WhenCallerLacksCreateTasksPermission()
        {
            // Arrange - a user in no role has no RolePolicy claims at all.
            var (client, _) = await CreateAuthenticatedClientAsync(role: null);

            // Act
            var response = await client.PostAsJsonAsync("api/v1/tasks", new { title = "Should be forbidden", priority = "Normal" });

            // Assert (AC25)
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Get_ReturnsTaskDetail_WithAllowedTransitions()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync("Manager");
            var createResponse = await client.PostAsJsonAsync("api/v1/tasks", new { title = "Readable task", priority = "High" });
            var created = (await createResponse.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions))!.Result!;

            // Act
            var response = await client.GetAsync($"api/v1/tasks/{created.Id}");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body!.Success);
            Assert.Equal(created.Id, body.Result!.Id);
            Assert.Equal(TaskPriority.High, body.Result.Priority);
            var allowed = Assert.Single(body.Result.AllowedTransitions);
            Assert.Equal("Todo", allowed.Status);
        }

        [Fact]
        public async Task Get_Refuses_WhenTaskDoesNotExist()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync("Member");

            // Act
            var response = await client.GetAsync("api/v1/tasks/999999");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            // Assert (AC24)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Get.NotFound, body!.ErrorCode);
        }

        [Fact]
        public async Task Get_Refuses_WhenTaskIsSoftDeleted()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync("Member");
            var createResponse = await client.PostAsJsonAsync("api/v1/tasks", new { title = "Will be deleted", priority = "Normal" });
            var created = (await createResponse.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions))!.Result!;

            await using (var context = _dbFixture.CreateContext())
            {
                var entity = await context.Set<TaskFlow.DataAccess.Entities.TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == created.Id);
                entity.IsDeleted = true;
                await context.SaveChangesAsync();
            }

            // Act (AC24: soft-deleted included)
            var response = await client.GetAsync($"api/v1/tasks/{created.Id}");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Get.NotFound, body!.ErrorCode);
        }

        [Fact]
        public async Task Get_Refuses_WhenCallerIsUnauthenticated()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("api/v1/tasks/1");

            // Assert (AC26)
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Get_Refuses_WhenCallerLacksViewTasksPermission()
        {
            // Arrange
            var (client, _) = await CreateAuthenticatedClientAsync(role: null);

            // Act
            var response = await client.GetAsync("api/v1/tasks/1");

            // Assert (F04 AC14 / F01 AC25-equivalent for reads)
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice11-{Guid.NewGuid():N}";
            long newUserId;

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
                newUserId = user.Id;
            }

            // AddDefaultIdentity<User>() registers no IRoleStore, so UserManager
            // can't assign roles; AuthService reads permissions straight out of
            // UserRoles/Roles/RoleClaims instead (AuthService.GetRolePolicyPermissions),
            // so a role is granted the same way here: a direct UserRoles row.
            if (role != null)
            {
                await using var context = _dbFixture.CreateContext();
                var roleEntity = await context.Set<Role>().SingleAsync(r => r.Name == role);
                context.Add(new UserRole { UserId = newUserId, RoleId = roleEntity.Id });
                await context.SaveChangesAsync();
            }

            var client = _factory.CreateClient();
            var deviceGuid = Guid.NewGuid();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid, username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, $"login failed: {loginBody?.Error}");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Result!.Token);

            return (client, newUserId);
        }
    }
}
