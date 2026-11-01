using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.BusinessLogic.Dtos.Tasks;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Slice 1.3: PUT /tasks/{id}/reopen. Exercises F01 AC6, AC13 (regression),
    // AC21-AC23, D6, D7's plumbing is unchanged; F04 AC6 (f01-workflow.md,
    // f04-audit.md Technical contract).
    [Collection(DatabaseCollection.Name)]
    public class TasksReopenControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksReopenControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- Happy path (AC6, F04 AC6) ----

        [Fact]
        public async Task Reopen_MovesDoneTaskBackToReview_AndRecordsReopenedEntryWithReasonVerbatim()
        {
            var (task, _, _) = await AdvanceToDoneAsync();
            var manager = await CreateAuthenticatedClientAsync("Manager");

            const string reason = "QA found a regression in the login flow";
            var reopened = await ReopenAsync(manager.Client, task.Id, reason, task.Version);

            Assert.Equal(TaskWorkflowStatus.Review, reopened.Status);
            Assert.Null(reopened.CompletedAt);
            Assert.NotEqual(task.Version, reopened.Version);

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.Reopened);
            Assert.Equal(manager.UserId, entry.ActorUserId);
            Assert.Equal(nameof(TaskWorkflowStatus.Done), entry.FromValue);
            Assert.Equal(nameof(TaskWorkflowStatus.Review), entry.ToValue);
            Assert.Equal(reason, entry.Reason);
        }

        // ---- AC21: Member without ReopenTasks ----

        [Fact]
        public async Task Reopen_Refuses_WhenCallerLacksReopenTasksPermission()
        {
            var (task, _, _) = await AdvanceToDoneAsync();
            var member = await CreateAuthenticatedClientAsync("Member");

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/reopen", new { reason = "Needs another look", version = task.Version });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Equal(TaskWorkflowStatus.Done, reloaded.Status);
            Assert.False(await context.Set<TaskHistory>().AnyAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.Reopened));
        }

        // ---- AC22: blank reason is a shape error ----

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Reopen_ReturnsShapeError_WhenReasonIsBlank(string reason)
        {
            var (task, _, _) = await AdvanceToDoneAsync();
            var manager = await CreateAuthenticatedClientAsync("Manager");

            var response = await manager.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/reopen", new { reason, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.NotEmpty(body.Validates!);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Equal(TaskWorkflowStatus.Done, reloaded.Status);
            Assert.False(await context.Set<TaskHistory>().AnyAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.Reopened));
        }

        // ---- AC23: reopening a task whose due date is already past ----

        [Fact]
        public async Task Reopen_FlagsOverdue_AndWritesOverdueMarkedEntry_WhenDueDateIsAlreadyPast_WithReopeningUserAsActor()
        {
            var (task, _, _) = await AdvanceToDoneAsync();

            // Create validates DueDate >= today, so backdate it directly in the
            // store after the task reaches Done (Done always holds IsOverdue
            // false regardless of DueDate - TaskOverdueRule).
            await using (var context = _dbFixture.CreateContext())
            {
                var row = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
                row.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
                await context.SaveChangesAsync();
            }

            var reloadedBefore = await GetTaskAsync(task.Id);
            var manager = await CreateAuthenticatedClientAsync("Manager");

            var reopened = await ReopenAsync(manager.Client, task.Id, "Reopening an overdue task", reloadedBefore.Version);

            Assert.Equal(TaskWorkflowStatus.Review, reopened.Status);
            Assert.True(reopened.IsOverdue);

            await using var context2 = _dbFixture.CreateContext();
            var entries = await context2.Set<TaskHistory>()
                .Where(h => h.TaskId == task.Id)
                .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                .ToListAsync();

            var reopenedEntry = Assert.Single(entries, h => h.Event == TaskHistoryEvent.Reopened);
            var overdueEntry = Assert.Single(entries, h => h.Event == TaskHistoryEvent.OverdueMarked);
            Assert.Equal(manager.UserId, overdueEntry.ActorUserId);
            Assert.True(reopenedEntry.CreatedAt <= overdueEntry.CreatedAt);
        }

        // ---- AC13 regression: Done still refuses an ordinary move ----

        [Fact]
        public async Task Move_StillRefusesDoneToReview_ThroughTheOrdinaryMoveOperation_AfterReopenExists()
        {
            var (task, _, _) = await AdvanceToDoneAsync();
            var someCaller = await CreateAuthenticatedClientAsync("Manager");

            var response = await someCaller.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/status", new { status = "Review", version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Move.InvalidTransition, body!.ErrorCode);
        }

        // ---- Conflict on stale version ----

        [Fact]
        public async Task Reopen_StaleVersion_IsRefusedWithConflict()
        {
            var (task, _, _) = await AdvanceToDoneAsync();
            var staleVersion = task.Version;
            var manager = await CreateAuthenticatedClientAsync("Manager");

            // Bump the version with an irrelevant concurrent change is not
            // available pre-reopen (Done has no other legal move); simulate a
            // stale client by reopening once for real, then replaying the
            // original (now-stale) version.
            await ReopenAsync(manager.Client, task.Id, "First reopen", task.Version);

            var response = await manager.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/reopen", new { reason = "Second, stale attempt", version = staleVersion });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Reopen.Conflict, body!.ErrorCode);
        }

        [Fact]
        public async Task Reopen_TwoRacingSaveChanges_OnSeparateConnections_ThrowsConcurrencyException_ServiceTranslatesToConflict()
        {
            // Same mechanism Move's AC29 test proves (R13): both scopes read
            // the same original Version before either writes, so EF's
            // Id+Version WHERE clause - not the explicit version check - is
            // what decides the loser here. Confirms Reopen's catch shape
            // (DbUpdateConcurrencyException + the shared IsDeadlock unwrap)
            // behaves the same as Move's for its own SaveChanges().
            var (task, _, _) = await AdvanceToDoneAsync();

            var configuration = _factory.Services.GetRequiredService<IConfiguration>();

            await using var contextA = _dbFixture.CreateContext();
            await using var contextB = _dbFixture.CreateContext();
            var serviceA = new TaskService(configuration, new UnitOfWork(contextA, new HttpContextAccessor()), NullLogger<TaskService>.Instance);
            var serviceB = new TaskService(configuration, new UnitOfWork(contextB, new HttpContextAccessor()), NullLogger<TaskService>.Instance);

            var reopenDto = new TaskReopenReqDto { Reason = "Racing reopen", Version = task.Version };
            var permissions = new[] { Permissions.Tasks.ReopenTasks };

            var outcomeA = serviceA.Reopen(task.Id, reopenDto, 1, permissions);
            var outcomeB = serviceB.Reopen(task.Id, reopenDto, 1, permissions);
            await Task.WhenAll(outcomeA, outcomeB);

            var outcomes = new[] { await outcomeA, await outcomeB };
            Assert.Single(outcomes, o => o.ErrorCode == string.Empty);
            Assert.Single(outcomes, o => o.ErrorCode == TaskControllerMS.Reopen.Conflict);

            await using var assertContext = _dbFixture.CreateContext();
            var finalTask = await assertContext.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Equal(TaskWorkflowStatus.Review, finalTask.Status);

            var reopenedCount = await assertContext.Set<TaskHistory>()
                .CountAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.Reopened);
            Assert.Equal(1, reopenedCount);
        }

        // ---- Helpers ----

        private async Task<TaskDetailRes> GetTaskAsync(long id)
        {
            var admin = await CreateAuthenticatedClientAsync("Manager");
            var response = await admin.Client.GetAsync($"api/v1/tasks/{id}");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(body!.Success, body.Error);
            return body.Result!;
        }

        private async Task<TaskDetailRes> ReopenAsync(HttpClient client, long id, string reason, Guid version)
        {
            var response = await client.PutAsJsonAsync($"api/v1/tasks/{id}/reopen", new { reason, version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, $"reopen failed: {body?.ErrorCode}");
            return body!.Result!;
        }

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, long? assigneeId = null)
        {
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = $"Reopen test {Guid.NewGuid():N}",
                priority = "Normal",
                assigneeId,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(body!.Success, body.Error);
            return body.Result!;
        }

        private async Task<TaskDetailRes> MoveAsync(HttpClient client, long id, TaskWorkflowStatus status, Guid version)
        {
            var response = await client.PutAsJsonAsync($"api/v1/tasks/{id}/status", new { status = status.ToString(), version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, $"move to {status} failed: {body?.ErrorCode}");
            return body!.Result!;
        }

        // Walks a freshly created task Backlog -> Todo -> InProgress -> Review
        // -> Done, the mover driving everything up to Review and a third
        // caller (not the assignee) approving into Done, so D5 holds.
        private async Task<(TaskDetailRes Task, long AssigneeId, HttpClient AssigneeClient)> AdvanceToDoneAsync()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var assignee = await CreateAuthenticatedClientAsync("Member");
            var closer = await CreateAuthenticatedClientAsync("Manager");

            var current = await CreateTaskAsync(mover.Client, assignee.UserId);
            current = await MoveAsync(mover.Client, current.Id, TaskWorkflowStatus.Todo, current.Version);
            current = await MoveAsync(mover.Client, current.Id, TaskWorkflowStatus.InProgress, current.Version);
            current = await MoveAsync(mover.Client, current.Id, TaskWorkflowStatus.Review, current.Version);
            current = await MoveAsync(closer.Client, current.Id, TaskWorkflowStatus.Done, current.Version);

            return (current, assignee.UserId, assignee.Client);
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice13-{Guid.NewGuid():N}";
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
