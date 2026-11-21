using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Responses.Activity;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // R2-4 (f04-activity-feed.md, ADR 0007): a read-only, keyset-paged
    // cross-task feed built from the existing F04 history data.
    [Collection(DatabaseCollection.Name)]
    public class ActivityControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "R2Slice4Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public ActivityControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task GetList_Returns401_WhenAnonymous()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("api/v1/activity");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetList_ReturnsEntriesNewestFirst_AcrossTasks()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Task A");
            var older = await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created, createdAt: DateTimeOffset.UtcNow.AddMinutes(-10));
            var newer = await SeedHistoryAsync(task.Id, TaskHistoryEvent.StatusChanged, fromValue: "Backlog", toValue: "Todo", createdAt: DateTimeOffset.UtcNow);

            var response = await caller.Client.GetAsync("api/v1/activity");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal(new[] { newer, older }, body.Result!.Select(r => r.Id));
            Assert.Equal(task.Id, body.Result![0].Task.Id);
            Assert.Equal(task.Title, body.Result[0].Task.Title);
        }

        [Fact]
        public async Task GetList_SystemActor_MarkedWithNullActorId_NeverZero()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Flagged task");
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.OverdueMarked, actorUserId: null);

            var response = await caller.Client.GetAsync("api/v1/activity");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            var entry = Assert.Single(body!.Result!);
            Assert.Null(entry.ActorUserId);
        }

        [Fact]
        public async Task GetList_IncludesEntriesOfADeletedTask_MarkedDeleted()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Will be deleted");
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created);

            await using (var context = _dbFixture.CreateContext())
            {
                var row = await context.Set<TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == task.Id);
                row.IsDeleted = true;
                await context.SaveChangesAsync();
            }

            var response = await caller.Client.GetAsync("api/v1/activity");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            var entry = Assert.Single(body!.Result!);
            Assert.True(entry.Task.IsDeleted);
        }

        [Fact]
        public async Task GetList_FiltersByActor()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var otherActor = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Filter by actor");
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created, actorUserId: caller.UserId);
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.StatusChanged, actorUserId: otherActor.UserId, fromValue: "Backlog", toValue: "Todo");

            var response = await caller.Client.GetAsync($"api/v1/activity?actor={caller.UserId}");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            var entry = Assert.Single(body!.Result!);
            Assert.Equal(caller.UserId, entry.ActorUserId);
        }

        [Fact]
        public async Task GetList_FiltersByActorSystem()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Filter by system");
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created, actorUserId: caller.UserId);
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.OverdueMarked, actorUserId: null);

            var response = await caller.Client.GetAsync("api/v1/activity?actor=system");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            var entry = Assert.Single(body!.Result!);
            Assert.Null(entry.ActorUserId);
        }

        [Fact]
        public async Task GetList_FiltersByEventType()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Filter by event");
            var created = await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created);
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.StatusChanged, fromValue: "Backlog", toValue: "Todo");

            var response = await caller.Client.GetAsync("api/v1/activity?events=Created");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            var entry = Assert.Single(body!.Result!);
            Assert.Equal(created, entry.Id);
        }

        [Fact]
        public async Task GetList_UnknownEventValue_ReturnsValidationError()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/activity?events=9999");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetList_FromLaterThanTo_Returns_InvalidDateRange()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/activity?from=2026-11-20&to=2026-11-10");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ActivityControllerMS.GetList.InvalidDateRange, body!.ErrorCode);
        }

        [Fact]
        public async Task GetList_GarbledCursor_Returns_InvalidCursor()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/activity?cursor=not-a-real-cursor!!!");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ActivityControllerMS.GetList.InvalidCursor, body!.ErrorCode);
        }

        [Fact]
        public async Task GetList_EmptyResult_HasMoreFalse()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/activity");
            var body = await response.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);

            Assert.True(body!.Success);
            Assert.Empty(body.Result!);
            Assert.False(body.Page!.HasMore);
        }

        [Fact]
        public async Task GetList_PagesWithCursor_NoSkipNoRepeat_AcrossTwoPages()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Paging task");

            var ids = new List<long>();
            for (var i = 0; i < 5; i++)
            {
                ids.Add(await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created, createdAt: DateTimeOffset.UtcNow.AddMinutes(-5 + i)));
            }

            var firstResponse = await caller.Client.GetAsync("api/v1/activity?length=2");
            var firstBody = await firstResponse.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);
            Assert.True(firstBody!.Page!.HasMore);
            Assert.Equal(2, firstBody.Result!.Count);

            var secondResponse = await caller.Client.GetAsync($"api/v1/activity?length=2&cursor={Uri.EscapeDataString(firstBody.Page.NextCursor!)}");
            var secondBody = await secondResponse.Content.ReadFromJsonAsync<CursorResultRes<List<ActivityRes>>>(JsonOptions);
            Assert.Equal(2, secondBody!.Result!.Count);

            var firstIds = firstBody.Result.Select(r => r.Id).ToList();
            var secondIds = secondBody.Result.Select(r => r.Id).ToList();
            Assert.Empty(firstIds.Intersect(secondIds));

            // Newest-first: page 2's newest id must be older than (or equal
            // to, impossible here with distinct timestamps) page 1's oldest.
            Assert.True(secondIds.Max() < firstIds.Min() || !ids.Contains(secondIds.Max()));
        }

        [Fact]
        public async Task GetList_NeverWritesAnyRow()
        {
            var caller = await CreateUserAndLoginAsync("Member");
            var task = await SeedTaskAsync("Read only check");
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created);

            int countBefore;
            await using (var context = _dbFixture.CreateContext())
            {
                countBefore = await context.Set<TaskHistory>().CountAsync();
            }

            await caller.Client.GetAsync("api/v1/activity");

            int countAfter;
            await using (var context = _dbFixture.CreateContext())
            {
                countAfter = await context.Set<TaskHistory>().CountAsync();
            }

            Assert.Equal(countBefore, countAfter);
        }

        // ---- Helpers ----

        private async Task<TaskItem> SeedTaskAsync(string title)
        {
            await using var context = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = title,
                Status = TaskWorkflowStatus.Backlog,
                Priority = TaskPriority.Normal,
                Version = Guid.NewGuid(),
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task<long> SeedHistoryAsync(
            long taskId,
            TaskHistoryEvent historyEvent,
            long? actorUserId = null,
            string? fromValue = null,
            string? toValue = null,
            DateTimeOffset? createdAt = null)
        {
            await using var context = _dbFixture.CreateContext();
            var history = new TaskHistory
            {
                TaskId = taskId,
                Event = historyEvent,
                ActorUserId = actorUserId,
                FromValue = fromValue,
                ToValue = toValue,
            };
            context.Add(history);
            await context.SaveChangesAsync();

            if (createdAt.HasValue)
            {
                history.CreatedAt = createdAt.Value;
                await context.SaveChangesAsync();
            }

            return history.Id;
        }

        private async Task<(HttpClient Client, long UserId)> CreateUserAndLoginAsync(string roleName)
        {
            var userName = $"r2s4-{Guid.NewGuid():N}";

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

            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByNameAsync(userName);
                return (client, user!.Id);
            }
        }
    }
}
