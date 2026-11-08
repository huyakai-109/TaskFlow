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
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Slice 5.3 (F05 Unit 5): the isOverdue filter on GET /tasks
    // (f05-overdue.md Technical contract T8). No ordering change - F03's
    // active-column rule / Done window apply exactly as before; this filter
    // composes with them.
    [Collection(DatabaseCollection.Name)]
    public class TasksOverdueListControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice53Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksOverdueListControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- isOverdue=true: only overdue tasks, across statuses, active-column order ----

        [Fact]
        public async Task List_IsOverdueTrue_ReturnsOnlyOverdueTasks_AcrossStatuses_ActiveColumnOrder()
        {
            var overdueUrgent = await SeedTaskAsync("Overdue urgent, Todo", TaskPriority.Urgent, status: TaskWorkflowStatus.Todo, isOverdue: true);
            var overdueNormal = await SeedTaskAsync("Overdue normal, InProgress", TaskPriority.Normal, status: TaskWorkflowStatus.InProgress, isOverdue: true);
            await SeedTaskAsync("Not overdue, Backlog", TaskPriority.Urgent, status: TaskWorkflowStatus.Backlog, isOverdue: false);

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks?isOverdue=true&start=1&length=50");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            // Urgent outranks Normal under the active rule, even though the
            // Normal task was seeded second and sits in a later column.
            AssertOrder(body.Result!, overdueUrgent.Id, overdueNormal.Id);
            Assert.All(body.Result!, i => Assert.True(i.IsOverdue));
        }

        // ---- isOverdue=false: only non-overdue tasks ----

        [Fact]
        public async Task List_IsOverdueFalse_ReturnsOnlyNonOverdueTasks()
        {
            var notOverdue = await SeedTaskAsync("Not overdue", TaskPriority.Normal, isOverdue: false);
            await SeedTaskAsync("Overdue", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: true);

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks?isOverdue=false&start=1&length=50");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Single(body.Result!);
            Assert.Equal(notOverdue.Id, body.Result![0].Id);
            Assert.False(body.Result![0].IsOverdue);
        }

        // ---- omitted: unfiltered set, regression ----

        [Fact]
        public async Task List_IsOverdueOmitted_ReturnsUnfilteredSet()
        {
            var notOverdue = await SeedTaskAsync("Not overdue", TaskPriority.Normal, isOverdue: false);
            var overdue = await SeedTaskAsync("Overdue", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: true);

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks?start=1&length=50");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(2, body.Result!.Count);
            Assert.Contains(body.Result, i => i.Id == notOverdue.Id);
            Assert.Contains(body.Result, i => i.Id == overdue.Id);
        }

        // ---- combined with status= ----

        [Fact]
        public async Task List_IsOverdueTrue_CombinedWithStatus_FiltersOnTopOfStatusScopedSet()
        {
            var overdueTodo = await SeedTaskAsync("Overdue, Todo", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: true);
            await SeedTaskAsync("Not overdue, Todo", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: false);
            await SeedTaskAsync("Overdue, InProgress", TaskPriority.Normal, status: TaskWorkflowStatus.InProgress, isOverdue: true);

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks?status=Todo&isOverdue=true&start=1&length=50");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Single(body.Result!);
            Assert.Equal(overdueTodo.Id, body.Result![0].Id);
        }

        // ---- status=Done&isOverdue=true -> empty, not an error ----

        [Fact]
        public async Task List_StatusDoneAndIsOverdueTrue_ReturnsEmpty_NotAnError()
        {
            await SeedTaskAsync("Completed, never overdue", TaskPriority.Normal, status: TaskWorkflowStatus.Done, isOverdue: false, completedAt: DateTimeOffset.UtcNow);

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks?status=Done&isOverdue=true&start=1&length=50");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Empty(body.Result!);
            Assert.Equal(0, body.Pagination!.TotalItems);
        }

        // ---- soft-deleted excluded regardless of isOverdue ----

        [Fact]
        public async Task List_IsOverdueTrue_SoftDeletedTask_IsExcluded()
        {
            var visible = await SeedTaskAsync("Overdue, visible", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: true);
            await SeedTaskAsync("Overdue, soft-deleted", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: true, isDeleted: true);

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks?isOverdue=true&start=1&length=50");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Single(body.Result!);
            Assert.Equal(visible.Id, body.Result![0].Id);
        }

        // ---- paging correctness with the filter active ----

        [Fact]
        public async Task List_IsOverdueTrue_PagingThroughResult_NoGapOrDuplicate()
        {
            var ids = new List<long>();
            for (var i = 0; i < 12; i++)
            {
                var task = await SeedTaskAsync($"Overdue {i}", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: true);
                ids.Add(task.Id);
            }

            for (var i = 0; i < 5; i++)
            {
                await SeedTaskAsync($"Not overdue filler {i}", TaskPriority.Normal, status: TaskWorkflowStatus.Todo, isOverdue: false);
            }

            var member = await CreateAuthenticatedClientAsync("Member");
            var seen = new List<long>();
            for (var page = 1; page <= 3; page++)
            {
                var response = await member.Client.GetAsync($"api/v1/tasks?isOverdue=true&start={page}&length=5");
                var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);
                Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
                Assert.Equal(12, body.Pagination!.TotalItems);
                seen.AddRange(body.Result!.Select(i => i.Id));
            }

            Assert.Equal(ids.Count, seen.Distinct().Count());
            Assert.Equal(ids.OrderBy(i => i), seen.OrderBy(i => i));
        }

        // ---- non-boolean value -> R1 shape-error envelope, not a 500 ----

        [Fact]
        public async Task List_IsOverdueNonBoolean_ReturnsShapeError_NotInternalServerError()
        {
            var member = await CreateAuthenticatedClientAsync("Member");

            var response = await member.Client.GetAsync("api/v1/tasks?isOverdue=notabool");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.NotEmpty(body.Validates!);
        }

        // ---- Helpers ----

        private static void AssertOrder(List<TaskSummaryRes> items, params long[] expectedIdsInOrder)
        {
            var actual = items.Select(i => i.Id).Where(id => expectedIdsInOrder.Contains(id)).ToArray();
            Assert.Equal(expectedIdsInOrder, actual);
        }

        private async Task<TaskItem> SeedTaskAsync(
            string title,
            TaskPriority priority,
            TaskWorkflowStatus status = TaskWorkflowStatus.Backlog,
            bool isOverdue = false,
            bool isDeleted = false,
            DateTimeOffset? completedAt = null)
        {
            await using var context = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = title,
                Status = status,
                Priority = priority,
                DueDate = null,
                IsOverdue = isOverdue,
                IsDeleted = isDeleted,
                Version = Guid.NewGuid(),
                CompletedAt = completedAt,
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice53-{Guid.NewGuid():N}";
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
