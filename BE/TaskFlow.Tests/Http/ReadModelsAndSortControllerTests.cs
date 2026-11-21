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
using TaskFlow.Api.Models.Responses.Profile;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.Api.Models.Responses.Users;
using TaskFlow.Api.Models.Responses.Workflow;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // R2-5 (f10-read-models-and-sort.md): board sort, assigneeId filter,
    // lastTransition, GET /workflow, GET /users/workload, GET /me/summary.
    [Collection(DatabaseCollection.Name)]
    public class ReadModelsAndSortControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "R2Slice5Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public ReadModelsAndSortControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Board_SortDueDate_OrdersByDueDateIgnoringPriority()
        {
            var member = await CreateUserAndLoginAsync("Member");
            var urgentFarDue = await SeedTaskAsync("Urgent far due", TaskPriority.Urgent, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));
            var normalSoonDue = await SeedTaskAsync("Normal soon due", TaskPriority.Normal, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

            var response = await member.Client.GetAsync("api/v1/tasks/board?sort=dueDate");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal(TaskSortOrder.DueDate, body.Result!.Sort);
            var backlog = body.Result.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog).Items;
            AssertOrder(backlog, normalSoonDue.Id, urgentFarDue.Id);
        }

        [Fact]
        public async Task Board_SortNewest_OrdersByCreatedAtDescending()
        {
            var member = await CreateUserAndLoginAsync("Member");
            var older = await SeedTaskAsync("Older", TaskPriority.Normal, null, createdAt: DateTimeOffset.UtcNow.AddMinutes(-10));
            var newer = await SeedTaskAsync("Newer", TaskPriority.Normal, null, createdAt: DateTimeOffset.UtcNow);

            var response = await member.Client.GetAsync("api/v1/tasks/board?sort=newest");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            var backlog = body!.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog).Items;
            AssertOrder(backlog, newer.Id, older.Id);
        }

        [Fact]
        public async Task Board_UnknownSortValue_ReturnsValidationError()
        {
            var member = await CreateUserAndLoginAsync("Member");

            var response = await member.Client.GetAsync("api/v1/tasks/board?sort=totallyUnknown");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Board_Done_IgnoresSort_SameOrderForEveryValue()
        {
            var member = await CreateUserAndLoginAsync("Member");
            var earlier = await SeedTaskAsync("Completed earlier", TaskPriority.Urgent, null, status: TaskWorkflowStatus.Done, completedAt: DateTimeOffset.UtcNow.AddDays(-2));
            var later = await SeedTaskAsync("Completed later", TaskPriority.Normal, null, status: TaskWorkflowStatus.Done, completedAt: DateTimeOffset.UtcNow.AddDays(-1));

            var priorityOrder = await GetDoneColumnAsync(member.Client, "priority");
            var dueDateOrder = await GetDoneColumnAsync(member.Client, "dueDate");
            var newestOrder = await GetDoneColumnAsync(member.Client, "newest");

            AssertOrder(priorityOrder, later.Id, earlier.Id);
            AssertOrder(dueDateOrder, later.Id, earlier.Id);
            AssertOrder(newestOrder, later.Id, earlier.Id);
        }

        [Fact]
        public async Task GetList_FiltersByAssigneeId()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var assigneeA = await CreateUserAndLoginAsync("Member");
            var assigneeB = await CreateUserAndLoginAsync("Member");

            var taskForA = await CreateTaskAsync(manager.Client, assigneeA.UserId);
            await CreateTaskAsync(manager.Client, assigneeB.UserId);

            var response = await manager.Client.GetAsync($"api/v1/tasks?assigneeId={assigneeA.UserId}");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.All(body.Result!, t => Assert.Equal(assigneeA.UserId, t.Assignee!.Id));
            Assert.Contains(body.Result!, t => t.Id == taskForA.Id);
        }

        [Fact]
        public async Task GetDetail_ExposesLastTransition_AfterAMove()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var assignee = await CreateUserAndLoginAsync("Member");
            var task = await CreateTaskAsync(manager.Client, assignee.UserId);

            var atTodo = await MoveAsync(manager.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);
            var atInProgress = await MoveAsync(assignee.Client, task.Id, TaskWorkflowStatus.InProgress, atTodo.Version);

            var response = await manager.Client.GetAsync($"api/v1/tasks/{task.Id}");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.NotNull(body.Result!.LastTransition);
            Assert.Equal("Todo", body.Result.LastTransition!.From);
            Assert.Equal("InProgress", body.Result.LastTransition.To);
            Assert.Equal(assignee.UserId, body.Result.LastTransition.Actor!.Id);
        }

        [Fact]
        public async Task GetWorkflow_ReturnsConfiguredLimitAndTimeZone()
        {
            var member = await CreateUserAndLoginAsync("Member");

            var response = await member.Client.GetAsync("api/v1/workflow");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<WorkflowRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal(3, body.Result!.MaxInProgressPerAssignee);
            Assert.Equal("Asia/Ho_Chi_Minh", body.Result.TimeZone);
            Assert.NotEqual(default, body.Result.Today);
            // Quartz is never started under "Testing" (CoreDependencyConfiguration's
            // skipQuartz) - "null when not scheduled" is the documented
            // behavior here, not a missing registration.
            Assert.Null(body.Result.OverdueScan.LastFiredAt);
            Assert.Null(body.Result.OverdueScan.NextFireAt);
        }

        [Fact]
        public async Task GetWorkload_CountsNonDeletedAssignedTasksByStatus()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var assignee = await CreateUserAndLoginAsync("Member");
            var task = await CreateTaskAsync(manager.Client, assignee.UserId);
            await MoveAsync(manager.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);

            var response = await manager.Client.GetAsync("api/v1/users/workload");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<List<WorkloadRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            var entry = Assert.Single(body.Result!, w => w.UserId == assignee.UserId);
            Assert.Equal(1, entry.Todo);
            Assert.Equal(0, entry.InProgress);
            Assert.Equal(0, entry.Review);
        }

        [Fact]
        public async Task GetSummary_CountsCallersOwnInProgressTasks()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var assignee = await CreateUserAndLoginAsync("Member");
            var task = await CreateTaskAsync(manager.Client, assignee.UserId);
            var atTodo = await MoveAsync(manager.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);
            await MoveAsync(assignee.Client, task.Id, TaskWorkflowStatus.InProgress, atTodo.Version);

            var response = await assignee.Client.GetAsync("api/v1/me/summary");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<MeSummaryRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal(1, body.Result!.InProgress);
            Assert.True(body.Result.WeekStart < body.Result.WeekEnd);
        }

        // ---- Helpers ----

        private static void AssertOrder(List<TaskSummaryRes> items, params long[] expectedIdsInOrder)
        {
            var actual = items.Select(i => i.Id).Where(id => expectedIdsInOrder.Contains(id)).ToArray();
            Assert.Equal(expectedIdsInOrder, actual);
        }

        private async Task<List<TaskSummaryRes>> GetDoneColumnAsync(HttpClient client, string sort)
        {
            var response = await client.GetAsync($"api/v1/tasks/board?sort={sort}");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            return body.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Done).Items;
        }

        private async Task<TaskItem> SeedTaskAsync(
            string title,
            TaskPriority priority,
            DateOnly? dueDate,
            DateTimeOffset? createdAt = null,
            TaskWorkflowStatus status = TaskWorkflowStatus.Backlog,
            DateTimeOffset? completedAt = null)
        {
            await using var context = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = title,
                Status = status,
                Priority = priority,
                DueDate = dueDate,
                Version = Guid.NewGuid(),
                CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
                CompletedAt = completedAt,
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, long assigneeId)
        {
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = $"Task {Guid.NewGuid():N}",
                assigneeId,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            return body.Result!;
        }

        private async Task<TaskDetailRes> MoveAsync(HttpClient client, long taskId, TaskWorkflowStatus status, Guid version)
        {
            var response = await client.PutAsJsonAsync($"api/v1/tasks/{taskId}/status", new { status, version }, JsonOptions);
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            return body.Result!;
        }

        private async Task<(HttpClient Client, long UserId)> CreateUserAndLoginAsync(string roleName)
        {
            var userName = $"r2s5-{Guid.NewGuid():N}";

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
