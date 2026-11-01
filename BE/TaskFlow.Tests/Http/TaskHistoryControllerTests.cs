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
using TaskFlow.Api.Models.Responses.TaskHistories;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Slice 1.4: GET /tasks/{taskId}/history. Exercises F04 AC15, AC16-AC21
    // (f04-audit.md Technical contract T3).
    [Collection(DatabaseCollection.Name)]
    public class TaskHistoryControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TaskHistoryControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- AC17: the empty-but-Created case ----

        [Fact]
        public async Task GetList_ReturnsExactlyTheCreatedEntry_ForANewTask()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);

            var response = await member.Client.GetAsync($"api/v1/tasks/{task.Id}/history");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskHistoryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            var entry = Assert.Single(body.Result!);
            Assert.Equal(TaskHistoryEvent.Created, entry.Event);
            Assert.Equal(member.UserId, entry.ActorUserId);
        }

        // ---- AC15, AC21: full chronological trail, response shape ----

        [Fact]
        public async Task GetList_ReturnsFullTrail_OldestFirst_WithMessageAndBothTimestamps()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client, priority: "Normal");
            await UpdatePriorityAsync(member.Client, task.Id, "Urgent", task.Version);

            var response = await member.Client.GetAsync($"api/v1/tasks/{task.Id}/history");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskHistoryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(2, body.Result!.Count);
            Assert.Equal(TaskHistoryEvent.Created, body.Result[0].Event);
            Assert.Equal(TaskHistoryEvent.PriorityChanged, body.Result[1].Event);
            Assert.True(body.Result[0].OccurredAt <= body.Result[1].OccurredAt);

            var priorityEntry = body.Result[1];
            Assert.False(string.IsNullOrEmpty(priorityEntry.Message));
            Assert.Equal(member.UserId, priorityEntry.ActorUserId);
            Assert.NotEqual(default, priorityEntry.OccurredAt);
            Assert.NotEqual(default, priorityEntry.OccurredAtLocal);
            // DateTimeOffset equality compares the instant, not the offset -
            // UTC and the configured zone (+07:00, Asia/Ho_Chi_Minh) render
            // the same instant with different offsets (D4).
            Assert.Equal(priorityEntry.OccurredAt, priorityEntry.OccurredAtLocal);
        }

        // ---- AC18: deterministic tie-break ----

        [Fact]
        public async Task GetList_OrdersTiedTimestamps_ByIdDeterministically()
        {
            await using var seedContext = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = "Tie-break fixture",
                Status = TaskWorkflowStatus.Backlog,
                Priority = TaskPriority.Normal,
                Version = Guid.NewGuid(),
            };
            seedContext.Add(task);
            await seedContext.SaveChangesAsync();

            var sameInstant = DateTimeOffset.UtcNow;
            var first = new TaskHistory { TaskId = task.Id, Event = TaskHistoryEvent.Created, CreatedAt = sameInstant };
            var second = new TaskHistory { TaskId = task.Id, Event = TaskHistoryEvent.StatusChanged, CreatedAt = sameInstant, FromValue = "Backlog", ToValue = "Todo" };
            seedContext.AddRange(first, second);
            await seedContext.SaveChangesAsync();

            var member = await CreateAuthenticatedClientAsync("Member");

            var firstRead = await GetHistoryAsync(member.Client, task.Id);
            var secondRead = await GetHistoryAsync(member.Client, task.Id);

            Assert.Equal(new[] { first.Id, second.Id }, firstRead.Select(e => e.Id));
            Assert.Equal(firstRead.Select(e => e.Id), secondRead.Select(e => e.Id));
        }

        // ---- AC19: genuinely missing id ----

        [Fact]
        public async Task GetList_Returns404Equivalent_WhenTaskDoesNotExist()
        {
            var member = await CreateAuthenticatedClientAsync("Member");

            var response = await member.Client.GetAsync("api/v1/tasks/999999/history");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskHistoryRes>>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskHistoryControllerMS.GetList.TaskNotFound, body!.ErrorCode);
        }

        // ---- AC20: trail of a soft-deleted task is still readable ----

        [Fact]
        public async Task GetList_StillReadsTrail_ForASoftDeletedTask()
        {
            var manager = await CreateAuthenticatedClientAsync("Manager");
            var task = await CreateTaskAsync(manager.Client);

            var deleteResponse = await manager.Client.DeleteAsync($"api/v1/tasks/{task.Id}?version={task.Version}");
            Assert.True(deleteResponse.IsSuccessStatusCode);

            var response = await manager.Client.GetAsync($"api/v1/tasks/{task.Id}/history");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskHistoryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(2, body.Result!.Count); // Created, Deleted
            Assert.Contains(body.Result, e => e.Event == TaskHistoryEvent.Deleted);
        }

        // ---- AC16: 403 without ViewTasks ----

        [Fact]
        public async Task GetList_Refuses_WhenCallerLacksViewTasksPermission()
        {
            var creator = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(creator.Client);
            var noPermissionCaller = await CreateAuthenticatedClientAsync(role: null);

            var response = await noPermissionCaller.Client.GetAsync($"api/v1/tasks/{task.Id}/history");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // ---- Helpers ----

        private async Task<List<TaskHistoryRes>> GetHistoryAsync(HttpClient client, long taskId)
        {
            var response = await client.GetAsync($"api/v1/tasks/{taskId}/history");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskHistoryRes>>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            return body.Result!;
        }

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, string priority = "Normal")
        {
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = $"History test {Guid.NewGuid():N}",
                priority,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(body!.Success, body.Error);
            return body.Result!;
        }

        private async Task<TaskDetailRes> UpdatePriorityAsync(HttpClient client, long id, string priority, Guid version)
        {
            var response = await client.PutAsJsonAsync($"api/v1/tasks/{id}/priority", new { priority, version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, $"priority update failed: {body?.ErrorCode}");
            return body!.Result!;
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice14hist-{Guid.NewGuid():N}";
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
