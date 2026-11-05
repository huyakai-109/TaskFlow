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
    // Slice 4.1 (F03 Unit 4, the gate): the active-column ordering rule
    // (f03-sorting.md "Ordering rule" + Technical contract) - Priority asc,
    // "no due date" last, DueDate asc, CreatedAt asc, Id asc - applied in SQL
    // before pagination, across GET /tasks/board and GET /tasks. Done is out
    // of scope here (slice 4.2).
    [Collection(DatabaseCollection.Name)]
    public class TasksBoardOrderingControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice41Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksBoardOrderingControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- AC1: priority tie, earlier due date first ----

        [Fact]
        public async Task Board_TwoUrgentTasks_DueTomorrowBeforeDueNextWeek()
        {
            var tomorrow = await SeedTaskAsync("Urgent due tomorrow", TaskPriority.Urgent, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
            var nextWeek = await SeedTaskAsync("Urgent due next week", TaskPriority.Urgent, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

            var items = await GetBacklogColumnAsync();

            AssertOrder(items, tomorrow.Id, nextWeek.Id);
        }

        // ---- AC2: priority outranks due date ----

        [Fact]
        public async Task Board_HighUndated_BeforeNormalDueToday()
        {
            var high = await SeedTaskAsync("High undated", TaskPriority.High, dueDate: null);
            var normal = await SeedTaskAsync("Normal due today", TaskPriority.Normal, DateOnly.FromDateTime(DateTime.UtcNow));

            var items = await GetBacklogColumnAsync();

            AssertOrder(items, high.Id, normal.Id);
        }

        // ---- AC3: priority raised mid-list moves the task to the top ----

        [Fact]
        public async Task Board_PriorityRaisedToUrgent_AppearsFirst_OnNextRead()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            await SeedTaskAsync("Stays normal, created first", TaskPriority.Normal, dueDate: null);
            var toPromote = await SeedTaskAsync("Promoted later", TaskPriority.Normal, dueDate: null);
            await SeedTaskAsync("Stays normal, created last", TaskPriority.Normal, dueDate: null);

            await using (var context = _dbFixture.CreateContext())
            {
                var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == toPromote.Id);
                var promoteResponse = await member.Client.PutAsJsonAsync(
                    $"api/v1/tasks/{toPromote.Id}/priority",
                    new { priority = "Urgent", version = reloaded.Version });
                Assert.True(promoteResponse.IsSuccessStatusCode);
            }

            var items = await GetBacklogColumnAsync();

            Assert.Equal(toPromote.Id, items[0].Id);
        }

        // ---- AC4: same priority, dated before undated ----

        [Fact]
        public async Task Board_SamePriority_DatedBeforeUndated()
        {
            var undated = await SeedTaskAsync("Normal undated", TaskPriority.Normal, dueDate: null);
            var dated = await SeedTaskAsync("Normal dated", TaskPriority.Normal, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

            var items = await GetBacklogColumnAsync();

            AssertOrder(items, dated.Id, undated.Id);
        }

        // ---- AC5: every Urgent before every High before every Normal ----

        [Fact]
        public async Task Board_MixedPrioritiesAndDueDates_GroupsByPriorityFirst()
        {
            var normalSoon = await SeedTaskAsync("Normal due soon", TaskPriority.Normal, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
            var highLate = await SeedTaskAsync("High due late", TaskPriority.High, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
            var urgentUndated = await SeedTaskAsync("Urgent undated", TaskPriority.Urgent, dueDate: null);

            var items = await GetBacklogColumnAsync();

            AssertOrder(items, urgentUndated.Id, highLate.Id, normalSoon.Id);
        }

        // ---- AC6: same priority + same due date, tie-break stable across reads ----

        [Fact]
        public async Task Board_SamePriorityAndDueDate_StableAcrossTwoReads()
        {
            var sameDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
            var first = await SeedTaskAsync("Tie A", TaskPriority.Normal, sameDate, createdAt: DateTimeOffset.UtcNow.AddMinutes(-10));
            var second = await SeedTaskAsync("Tie B", TaskPriority.Normal, sameDate, createdAt: DateTimeOffset.UtcNow.AddMinutes(-10));

            var firstRead = await GetBacklogColumnAsync();
            var secondRead = await GetBacklogColumnAsync();

            AssertOrder(firstRead, first.Id, second.Id);
            Assert.Equal(firstRead.Select(i => i.Id), secondRead.Select(i => i.Id));
        }

        // ---- AC7: insertion order is irrelevant ----

        [Fact]
        public async Task Board_InsertionOrderUnrelatedToPriority_OrderFollowsTheRule_NotInsertion()
        {
            var normal = await SeedTaskAsync("Inserted first, Normal", TaskPriority.Normal, dueDate: null);
            var urgent = await SeedTaskAsync("Inserted second, Urgent", TaskPriority.Urgent, dueDate: null);
            var high = await SeedTaskAsync("Inserted third, High", TaskPriority.High, dueDate: null);

            var items = await GetBacklogColumnAsync();

            AssertOrder(items, urgent.Id, high.Id, normal.Id);
        }

        // ---- AC13: ordering applied before pagination ----

        [Fact]
        public async Task Board_MoreThan50TasksInColumn_Page1HoldsHighestRanked50()
        {
            var urgentIds = new List<long>();
            for (var i = 0; i < 10; i++)
            {
                var task = await SeedTaskAsync($"Urgent bulk {i}", TaskPriority.Urgent, dueDate: null);
                urgentIds.Add(task.Id);
            }

            for (var i = 0; i < 45; i++)
            {
                await SeedTaskAsync($"Normal filler {i}", TaskPriority.Normal, dueDate: null);
            }

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            var backlog = body.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog);

            Assert.Equal(55, backlog.Pagination!.TotalItems);
            Assert.Equal(50, backlog.Items.Count);
            Assert.True(urgentIds.All(id => backlog.Items.Any(i => i.Id == id)), "all 10 Urgent tasks must be on page 1");
        }

        // ---- AC14: concatenated pages, no data changing, no gap/duplicate ----

        [Fact]
        public async Task List_PagingThroughColumn_NoDataChanging_ConcatenatedPagesHaveNoGapOrDuplicate()
        {
            var ids = new List<long>();
            for (var i = 0; i < 12; i++)
            {
                var task = await SeedTaskAsync($"Page test {i}", TaskPriority.Normal, dueDate: null);
                ids.Add(task.Id);
            }

            var member = await CreateAuthenticatedClientAsync("Member");
            var seen = new List<long>();
            for (var page = 1; page <= 3; page++)
            {
                var response = await member.Client.GetAsync($"api/v1/tasks?status=Backlog&start={page}&length=5");
                var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);
                Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
                seen.AddRange(body.Result!.Select(i => i.Id));
            }

            Assert.Equal(ids.Count, seen.Distinct().Count());
            Assert.Equal(ids.OrderBy(i => i), seen.OrderBy(i => i));
        }

        // ---- AC15: list page continues exactly where board page 1 ended ----

        [Fact]
        public async Task List_PageTwo_ContinuesExactlyWhereBoardPageOneEnded_NoOverlap()
        {
            for (var i = 0; i < 60; i++)
            {
                await SeedTaskAsync($"Continuation test {i}", TaskPriority.Normal, dueDate: null);
            }

            var member = await CreateAuthenticatedClientAsync("Member");

            var boardResponse = await member.Client.GetAsync("api/v1/tasks/board");
            var boardBody = await boardResponse.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);
            Assert.True(boardResponse.IsSuccessStatusCode && boardBody!.Success, boardBody?.ErrorCode);
            var backlog = boardBody.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog);
            var pageSize = backlog.Pagination!.PageSize;
            var page1Ids = backlog.Items.Select(i => i.Id).ToList();

            var listResponse = await member.Client.GetAsync($"api/v1/tasks?status=Backlog&start=2&length={pageSize}");
            var listBody = await listResponse.Content.ReadFromJsonAsync<PaginationResultRes<List<TaskSummaryRes>>>(JsonOptions);
            Assert.True(listResponse.IsSuccessStatusCode && listBody!.Success, listBody?.ErrorCode);
            var page2Ids = listBody.Result!.Select(i => i.Id).ToList();

            Assert.Empty(page1Ids.Intersect(page2Ids));
        }

        // ---- AC17: soft-deleted tasks absent, hold no position (regression) ----

        [Fact]
        public async Task Board_SoftDeletedTask_IsAbsent_AndHoldsNoPosition()
        {
            var manager = await CreateAuthenticatedClientAsync("Manager");
            var visible = await SeedTaskAsync("Stays visible", TaskPriority.Urgent, dueDate: null);
            var deleted = await SeedTaskAsync("Deleted after seed", TaskPriority.Urgent, dueDate: null);

            await using (var context = _dbFixture.CreateContext())
            {
                var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == deleted.Id);
                var deleteResponse = await manager.Client.DeleteAsync($"api/v1/tasks/{deleted.Id}?version={reloaded.Version}");
                Assert.True(deleteResponse.IsSuccessStatusCode);
            }

            var items = await GetBacklogColumnAsync();

            Assert.DoesNotContain(items, i => i.Id == deleted.Id);
            Assert.Contains(items, i => i.Id == visible.Id);
        }

        // ---- AC18: caller without ViewTasks is forbidden, no data (regression) ----

        [Fact]
        public async Task Board_CallerWithoutViewTasksPermission_IsForbidden_NoData()
        {
            await SeedTaskAsync("Should not be visible", TaskPriority.Urgent, dueDate: null);
            var noPermissionCaller = await CreateAuthenticatedClientAsync(role: null);

            var response = await noPermissionCaller.Client.GetAsync("api/v1/tasks/board");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // ---- AC19: any authenticated caller with ViewTasks sees every task (regression) ----

        [Fact]
        public async Task Board_AnyCallerWithViewTasks_SeesEveryTask_NoPerUserFiltering()
        {
            var creator = await CreateAuthenticatedClientAsync("Member");
            var otherViewer = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync("Visible to everyone", TaskPriority.Urgent, dueDate: null);

            var response = await otherViewer.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            var backlog = body.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog);
            Assert.Contains(backlog.Items, i => i.Id == task.Id);
            _ = creator;
        }

        // ---- AC20: moved task sits at its rule position, not "drop" position ----

        [Fact]
        public async Task Board_TaskMovedBetweenColumns_SitsAtRulePosition_OnNextRead()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");

            var alreadyInTodo = await SeedTaskAsync("Already in Todo, Urgent", TaskPriority.Urgent, dueDate: null, status: TaskWorkflowStatus.Todo);
            var movedTask = await SeedTaskAsync("Moving into Todo, Normal in Backlog first", TaskPriority.Normal, dueDate: null);

            await using (var context = _dbFixture.CreateContext())
            {
                var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == movedTask.Id);
                var moveResponse = await mover.Client.PutAsJsonAsync(
                    $"api/v1/tasks/{movedTask.Id}/status",
                    new { status = "Todo", version = reloaded.Version });
                Assert.True(moveResponse.IsSuccessStatusCode);
            }

            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            var todo = body.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Todo);

            // Urgent outranks Normal regardless of which was moved in "last".
            AssertOrder(todo.Items, alreadyInTodo.Id, movedTask.Id);
        }

        // ---- AC12/AC16: regression, empty column present and well-formed ----

        [Fact]
        public async Task Board_GroupsByStatus_EmptyColumnPresent_AndWellFormed()
        {
            await SeedTaskAsync("Only backlog task", TaskPriority.Normal, dueDate: null);
            var member = await CreateAuthenticatedClientAsync("Member");

            var response = await member.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            var columns = body.Result!.Columns;
            Assert.Equal(5, columns.Count);
            Assert.Equal(
                new[] { TaskWorkflowStatus.Backlog, TaskWorkflowStatus.Todo, TaskWorkflowStatus.InProgress, TaskWorkflowStatus.Review, TaskWorkflowStatus.Done },
                columns.Select(c => c.Status));

            var todo = columns.Single(c => c.Status == TaskWorkflowStatus.Todo);
            Assert.Empty(todo.Items);
            Assert.Equal(0, todo.Pagination!.TotalItems);

            var backlog = columns.Single(c => c.Status == TaskWorkflowStatus.Backlog);
            Assert.Single(backlog.Items);
        }

        // ---- Helpers ----

        private static void AssertOrder(List<TaskSummaryRes> items, params long[] expectedIdsInOrder)
        {
            var actual = items.Select(i => i.Id).Where(id => expectedIdsInOrder.Contains(id)).ToArray();
            Assert.Equal(expectedIdsInOrder, actual);
        }

        private async Task<List<TaskSummaryRes>> GetBacklogColumnAsync()
        {
            var member = await CreateAuthenticatedClientAsync("Member");
            var response = await member.Client.GetAsync("api/v1/tasks/board");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<BoardRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            return body.Result!.Columns.Single(c => c.Status == TaskWorkflowStatus.Backlog).Items;
        }

        // Seeds directly via DbContext with a fixed CreatedAt so ordering/tie-break
        // assertions don't depend on real-time clock resolution (precedent:
        // TasksMoveControllerTests.cs TaskHistories_OrderByCreatedAtThenId_IsStable...).
        private async Task<TaskItem> SeedTaskAsync(
            string title,
            TaskPriority priority,
            DateOnly? dueDate,
            DateTimeOffset? createdAt = null,
            TaskWorkflowStatus status = TaskWorkflowStatus.Backlog)
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
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice41-{Guid.NewGuid():N}";
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
