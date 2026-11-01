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
    // Slice 1.4: PUT /tasks/{id}, /priority, /due-date, /assignee, DELETE
    // /tasks/{id}, GET /tasks/board, GET /tasks. Exercises F04 AC3-AC5, AC9,
    // F03's board/list shape, and the remaining F01 primitives
    // (f01-workflow.md, f04-audit.md Technical contract).
    [Collection(DatabaseCollection.Name)]
    public class TasksReadsEditsControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksReadsEditsControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- PUT /tasks/{id} (Title/Description, no history entry) ----

        [Fact]
        public async Task Update_ChangesTitleAndDescription_AndWritesNoHistoryEntry()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}", new
            {
                title = "Renamed title",
                description = "New description",
                version = task.Version,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal("Renamed title", body.Result!.Title);
            Assert.Equal("New description", body.Result.Description);
            Assert.NotEqual(task.Version, body.Result.Version);

            // Only the Created entry from CreateTaskAsync - Update writes none (T5 row 5).
            await using var context = _dbFixture.CreateContext();
            Assert.Equal(1, await context.Set<TaskHistory>().CountAsync(h => h.TaskId == task.Id));
        }

        [Fact]
        public async Task Update_StaleVersion_IsRefusedWithConflict()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);
            var staleVersion = task.Version;

            await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}", new { title = "First edit", version = task.Version });

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}", new { title = "Second, stale edit", version = staleVersion });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Update.Conflict, body!.ErrorCode);
        }

        [Fact]
        public async Task Update_Refuses_WhenCallerLacksUpdateTasksPermission()
        {
            var creator = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(creator.Client);
            var noPermissionCaller = await CreateAuthenticatedClientAsync(role: null);

            var response = await noPermissionCaller.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}", new { title = "Should not apply", version = task.Version });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Equal(task.Title, reloaded.Title);
        }

        // ---- PUT /tasks/{id}/priority (F04 AC4) ----

        [Fact]
        public async Task UpdatePriority_ChangesPriority_AndRecordsPriorityChangedEntry()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client, priority: "Normal");

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/priority", new { priority = "Urgent", version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(TaskPriority.Urgent, body.Result!.Priority);

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.PriorityChanged);
            Assert.Equal(member.UserId, entry.ActorUserId);
            Assert.Equal(nameof(TaskPriority.Normal), entry.FromValue);
            Assert.Equal(nameof(TaskPriority.Urgent), entry.ToValue);
        }

        // ---- PUT /tasks/{id}/due-date (F04 AC5: both directions, including null) ----

        [Fact]
        public async Task UpdateDueDate_SetsDueDateFromNull_AndRecordsDueDateChangedEntry()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);
            Assert.Null(task.DueDate);

            var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/due-date", new { dueDate, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(dueDate, body.Result!.DueDate);

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.DueDateChanged);
            Assert.Null(entry.FromValue);
            Assert.Equal(dueDate.ToString("yyyy-MM-dd"), entry.ToValue);
        }

        [Fact]
        public async Task UpdateDueDate_ClearsDueDateToNull_AndRecordsDueDateChangedEntry()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);
            var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            var withDueDate = await UpdateDueDateAsync(member.Client, task.Id, dueDate, task.Version);

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/due-date", new { dueDate = (DateOnly?)null, version = withDueDate.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Null(body.Result!.DueDate);
            Assert.False(body.Result.IsOverdue);

            await using var context = _dbFixture.CreateContext();
            var entries = await context.Set<TaskHistory>()
                .Where(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.DueDateChanged)
                .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                .ToListAsync();
            var clearEntry = Assert.Single(entries, h => h.ToValue == null);
            Assert.Equal(dueDate.ToString("yyyy-MM-dd"), clearEntry.FromValue);
        }

        [Fact]
        public async Task UpdateDueDate_RefusesPastDate()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);
            var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/due-date", new { dueDate = pastDate, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.UpdateDueDate.DueDateInPast, body!.ErrorCode);
        }

        // ---- PUT /tasks/{id}/assignee (F04 AC3: names both old and new) ----

        [Fact]
        public async Task Assign_ReassignsFromOneMemberToAnother_AndRecordsBothOldAndNew()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var memberA = await CreateAuthenticatedClientAsync("Member");
            var memberB = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client, memberA.UserId);

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = memberB.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(memberB.UserId, body.Result!.Assignee!.Id);

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.AssigneeChanged);
            Assert.Equal(mover.UserId, entry.ActorUserId);
            Assert.Equal(memberA.UserId.ToString(), entry.FromValue);
            Assert.Equal(memberB.UserId.ToString(), entry.ToValue);
        }

        [Fact]
        public async Task Assign_RefusesUnknownAssigneeId()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = 999_999_999L, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.AssigneeNotFound, body!.ErrorCode);
        }

        // ---- DELETE /tasks/{id} (F04 AC9: trail survives, every earlier entry unchanged) ----

        [Fact]
        public async Task Delete_SoftDeletesTask_WritesDeletedEntry_AndLeavesEarlierEntriesUnchanged()
        {
            var manager = await CreateAuthenticatedClientAsync("Manager");
            var task = await CreateTaskAsync(manager.Client, priority: "High");
            var afterPriority = await UpdatePriorityAsync(manager.Client, task.Id, "Urgent", task.Version);

            await using (var before = _dbFixture.CreateContext())
            {
                var priorityEntry = await before.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.PriorityChanged);
                Assert.NotNull(priorityEntry);
            }

            var response = await manager.Client.DeleteAsync($"api/v1/tasks/{task.Id}?version={afterPriority.Version}");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == task.Id);
            Assert.True(reloaded.IsDeleted);

            var stillVisible = await context.Set<TaskItem>().SingleOrDefaultAsync(t => t.Id == task.Id);
            Assert.Null(stillVisible);

            var entries = await context.Set<TaskHistory>()
                .Where(h => h.TaskId == task.Id)
                .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                .ToListAsync();
            Assert.Equal(3, entries.Count); // Created, PriorityChanged, Deleted
            Assert.Equal(TaskHistoryEvent.Created, entries[0].Event);
            Assert.Equal(TaskHistoryEvent.PriorityChanged, entries[1].Event);
            Assert.Equal(nameof(TaskPriority.High), entries[1].FromValue);
            Assert.Equal(nameof(TaskPriority.Urgent), entries[1].ToValue);
            Assert.Equal(TaskHistoryEvent.Deleted, entries[2].Event);
            Assert.Equal(manager.UserId, entries[2].ActorUserId);
        }

        [Fact]
        public async Task Delete_StaleVersion_IsRefusedWithConflict()
        {
            var manager = await CreateAuthenticatedClientAsync("Manager");
            var task = await CreateTaskAsync(manager.Client);
            var staleVersion = task.Version;

            await manager.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}", new { title = "Bump version", version = task.Version });

            var response = await manager.Client.DeleteAsync($"api/v1/tasks/{task.Id}?version={staleVersion}");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Delete.Conflict, body!.ErrorCode);
        }

        [Fact]
        public async Task Delete_Refuses_WhenCallerLacksDeleteTasksPermission()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);

            var response = await member.Client.DeleteAsync($"api/v1/tasks/{task.Id}?version={task.Version}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.False(reloaded.IsDeleted);
        }

        // ---- GET /tasks/board: always five columns, even empty ----

        [Fact]
        public async Task GetBoard_ReturnsFiveColumnsInLifecycleOrder_EvenWhenEmpty()
        {
            var member = await CreateAuthenticatedClientAsync("Member");

            var response = await member.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            var columns = body.Result!.Columns;
            Assert.Equal(5, columns.Count);
            Assert.Equal(
                new[] { TaskWorkflowStatus.Backlog, TaskWorkflowStatus.Todo, TaskWorkflowStatus.InProgress, TaskWorkflowStatus.Review, TaskWorkflowStatus.Done },
                columns.Select(c => c.Status));
            Assert.All(columns, c => Assert.Empty(c.Items));
            Assert.All(columns, c => Assert.Equal(0, c.Pagination!.TotalItems));
        }

        [Fact]
        public async Task GetBoard_PlacesCreatedTask_InItsColumn()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(member.Client);

            var response = await member.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            var backlog = body!.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog);
            Assert.Contains(backlog.Items, i => i.Id == task.Id);
        }

        // ---- GET /tasks: pagination ----

        [Fact]
        public async Task GetList_ReturnsSecondPage_OfOneColumn()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 3; i++)
            {
                await CreateTaskAsync(member.Client);
            }

            var response = await member.Client.GetAsync("api/v1/tasks?status=Backlog&start=2&length=2");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Single(body.Result!);
            Assert.Equal(3, body.Pagination!.TotalItems);
            Assert.Equal(2, body.Pagination.PageNumber);
        }

        // ---- Helpers ----

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, long? assigneeId = null, string priority = "Normal")
        {
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = $"Reads/edits test {Guid.NewGuid():N}",
                priority,
                assigneeId,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(body!.Success, body.Error);
            return body.Result!;
        }

        private async Task<TaskDetailRes> UpdateDueDateAsync(HttpClient client, long id, DateOnly? dueDate, Guid version)
        {
            var response = await client.PutAsJsonAsync($"api/v1/tasks/{id}/due-date", new { dueDate, version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, $"due-date update failed: {body?.ErrorCode}");
            return body!.Result!;
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
            var userName = $"slice14-{Guid.NewGuid():N}";
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
