using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Reports;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // f09-reports.md R1/R17/AC12/AC16/AC17/AC19/AC21.
    [Collection(DatabaseCollection.Name)]
    public class ReportsControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "R3Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public ReportsControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync() => _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- AC21: Activity removed, per-task history unaffected ----

        [Fact]
        public async Task Activity_Removed_Returns404()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/activity");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ---- AC12: scope and permission matrix ----

        [Theory]
        [InlineData("api/v1/reports/scorecard?from=2026-03-02&to=2026-03-08")]
        [InlineData("api/v1/reports/team?from=2026-03-02&to=2026-03-08")]
        [InlineData("api/v1/reports/tasks?from=2026-03-02&to=2026-03-08&metric=Done")]
        [InlineData("api/v1/reports/export?from=2026-03-02&to=2026-03-08&dataset=Buckets")]
        public async Task Anonymous_Returns401_OnEveryEndpoint(string route)
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync(route);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetScorecard_Member_NoUserId_Returns_SelfOnly()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/reports/scorecard?from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ReportControllerMS.GetScorecard.SelfOnly, body!.ErrorCode);
        }

        [Fact]
        public async Task GetScorecard_Member_AnotherUsersId_Returns_SelfOnly_EvenWhenThatUserDoesNotExist()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/reports/scorecard?userId=999999&from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ReportControllerMS.GetScorecard.SelfOnly, body!.ErrorCode);
        }

        [Fact]
        public async Task GetScorecard_Member_OwnId_Returns200()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync($"api/v1/reports/scorecard?userId={caller.UserId}&from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportScorecardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal("Member", body.Result!.Meta.Scope);
        }

        [Fact]
        public async Task GetTeam_Member_Returns403()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/reports/team?from=2026-03-02&to=2026-03-08");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Export_Member_Returns403()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync("api/v1/reports/export?from=2026-03-02&to=2026-03-08&dataset=Buckets");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // ---- AC20: seeded permissions reach the login response ----

        [Fact]
        public async Task Login_Manager_PermissionsArray_IncludesViewAndExportReports()
        {
            var userName = $"r3s1-{Guid.NewGuid():N}";
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = new User { UserName = userName, Email = $"{userName}@taskflow.test", EmailConfirmed = true, LockoutEnabled = true, DisplayName = userName };
                var createResult = await userManager.CreateAsync(user, TestPassword);
                Assert.True(createResult.Succeeded);

                await using var context = _dbFixture.CreateContext();
                var roleEntity = await context.Set<Role>().SingleAsync(r => r.Name == "Manager");
                context.Add(new UserRole { UserId = user.Id, RoleId = roleEntity.Id });
                await context.SaveChangesAsync();
            }

            var client = _factory.CreateClient();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid = Guid.NewGuid(), username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, loginBody?.Error);
            Assert.Contains("ViewReports", loginBody!.Result!.Permissions);
            Assert.Contains("ExportReports", loginBody.Result!.Permissions);
        }

        [Fact]
        public async Task GetScorecard_Manager_TeamScope_Returns200()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/scorecard?from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportScorecardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            Assert.Equal("Team", body.Result!.Meta.Scope);
        }

        [Fact]
        public async Task GetScorecard_Manager_AnyUser_Returns200()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var member = await CreateUserAndLoginAsync("Member");

            var response = await manager.Client.GetAsync($"api/v1/reports/scorecard?userId={member.UserId}&from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportScorecardRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
        }

        [Fact]
        public async Task GetScorecard_Manager_NonExistentUser_Returns_UserNotFound()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/scorecard?userId=999999&from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ReportControllerMS.GetScorecard.UserNotFound, body!.ErrorCode);
        }

        // ---- AC13: validation ----

        [Fact]
        public async Task GetScorecard_FromAfterTo_Returns_InvalidRange()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/scorecard?from=2026-03-08&to=2026-03-02");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(ReportControllerMS.GetScorecard.InvalidRange, body!.ErrorCode);
        }

        [Fact]
        public async Task GetScorecard_ToInFuture_Returns_RangeInFuture()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var farFuture = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

            var response = await manager.Client.GetAsync($"api/v1/reports/scorecard?from=2026-01-01&to={farFuture}");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(ReportControllerMS.GetScorecard.RangeInFuture, body!.ErrorCode);
        }

        [Fact]
        public async Task GetScorecard_RangeOver366Days_Returns_RangeTooLarge()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/scorecard?from=2024-01-01&to=2026-01-02");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(ReportControllerMS.GetScorecard.RangeTooLarge, body!.ErrorCode);
        }

        [Fact]
        public async Task GetScorecard_DayGranularityOver92Days_Returns_TooManyBuckets()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/scorecard?from=2025-12-01&to=2026-03-15&granularity=Day");
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>(JsonOptions);

            Assert.Equal(ReportControllerMS.GetScorecard.TooManyBuckets, body!.ErrorCode);
        }

        [Fact]
        public async Task GetScorecard_UnknownGranularity_Returns_ValidationFailed()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/scorecard?from=2026-03-02&to=2026-03-08&granularity=9999");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- AC17: read-only ----

        [Fact]
        public async Task GetScorecard_NeverWritesAnyRow()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var task = await SeedTaskAsync("Read only");
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created, DateTimeOffset.UtcNow);

            int tasksBefore;
            int historiesBefore;
            await using (var context = _dbFixture.CreateContext())
            {
                tasksBefore = await context.Set<TaskItem>().IgnoreQueryFilters().CountAsync();
                historiesBefore = await context.Set<TaskHistory>().CountAsync();
            }

            await manager.Client.GetAsync("api/v1/reports/scorecard?from=2026-03-02&to=2026-03-08");

            int tasksAfter;
            int historiesAfter;
            await using (var context = _dbFixture.CreateContext())
            {
                tasksAfter = await context.Set<TaskItem>().IgnoreQueryFilters().CountAsync();
                historiesAfter = await context.Set<TaskHistory>().CountAsync();
            }

            Assert.Equal(tasksBefore, tasksAfter);
            Assert.Equal(historiesBefore, historiesAfter);
        }

        // ---- AC16: team member order is by display name, never by a metric ----

        [Fact]
        public async Task GetTeam_MembersOrderedByDisplayName_RegardlessOfMetrics()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var zed = await CreateUserAndLoginAsync("Member", displayName: "Zed");
            var amy = await CreateUserAndLoginAsync("Member", displayName: "Amy");

            var response = await manager.Client.GetAsync("api/v1/reports/team?from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportTeamRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            var names = body.Result!.Members.Select(m => m.User.DisplayName).ToList();
            var amyIndex = names.IndexOf("Amy");
            var zedIndex = names.IndexOf("Zed");
            Assert.True(amyIndex < zedIndex);
        }

        // ---- AC22 (corrected 2026-11-21): contributors = members with
        // done >= 1, never the roster size; pooled take-over counts are
        // unaffected by contributors ----

        [Fact]
        public async Task GetTeam_Contributors_CountsOnlyMembersWithAtLeastOneCompletion()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var m1 = await CreateUserAndLoginAsync("Member", displayName: "M1");
            var m2 = await CreateUserAndLoginAsync("Member", displayName: "M2");

            // m1: two completions, credited at Done (T1, T2).
            var t1 = await SeedTaskAsync("T1", assigneeId: m1.UserId, dueDate: new DateOnly(2026, 3, 4));
            await SeedTrail(t1.Id, m1.UserId, Local(2026, 3, 2, 9, 0), Local(2026, 3, 2, 10, 0), Local(2026, 3, 3, 10, 0), Local(2026, 3, 4, 10, 0));
            var t2 = await SeedTaskAsync("T2", assigneeId: m1.UserId, dueDate: new DateOnly(2026, 3, 4));
            await SeedTrail(t2.Id, m1.UserId, Local(2026, 3, 2, 8, 0), Local(2026, 3, 2, 9, 0), Local(2026, 3, 3, 9, 0), Local(2026, 3, 4, 11, 0));

            // T3: created for m1, taken over by m2 (take-over credit), then
            // completed by m2 - m1's third completion, credited to m2.
            var t3 = await SeedTaskAsync("T3", assigneeId: m1.UserId, dueDate: null);
            await SeedHistoryAsync(t3.Id, TaskHistoryEvent.Created, Local(2026, 3, 1, 9, 0));
            await SeedHistoryAsync(t3.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 9, 0), "Todo", "InProgress");
            await SeedHistoryAsync(t3.Id, TaskHistoryEvent.AssigneeChanged, Local(2026, 3, 3, 9, 0), m1.UserId.ToString(), m2.UserId.ToString(), m2.UserId);
            await SeedHistoryAsync(t3.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 9, 0), "InProgress", "Review");
            await SeedHistoryAsync(t3.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 4, 18, 0), "Review", "Done");
            await using (var context = _dbFixture.CreateContext())
            {
                var task = await context.Set<TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == t3.Id);
                task.AssigneeId = m2.UserId;
                task.Status = TaskWorkflowStatus.Done;
                task.CompletedAt = Local(2026, 3, 4, 18, 0);
                await context.SaveChangesAsync();
            }

            // m1 needs a third completion to reach done=3 (T1, T2 above are
            // only 2 - T3 went to m2). Add T5, credited to m1.
            var t5 = await SeedTaskAsync("T5", assigneeId: m1.UserId, dueDate: null);
            await SeedTrail(t5.Id, m1.UserId, Local(2026, 3, 3, 8, 0), Local(2026, 3, 3, 11, 0), Local(2026, 3, 4, 9, 0), Local(2026, 3, 5, 9, 0));

            // T4: m2's second completion.
            var t4 = await SeedTaskAsync("T4", assigneeId: m2.UserId, dueDate: null);
            await SeedTrail(t4.Id, m2.UserId, Local(2026, 3, 5, 8, 0), Local(2026, 3, 6, 8, 0), Local(2026, 3, 8, 8, 0), Local(2026, 3, 8, 12, 0));

            var response = await manager.Client.GetAsync("api/v1/reports/team?from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportTeamRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);

            // Full R11 roster (every active user, across this long-lived
            // test database - not reset between runs, unlike Tasks/
            // TaskHistories) means the exact roster size is not asserted
            // (other test classes' users persist in Users too); instead,
            // assert this fixture's three users are all present by id.
            var memberIds = body.Result!.Members.Select(m => m.User.Id).ToList();
            Assert.Contains(manager.UserId, memberIds);
            Assert.Contains(m1.UserId, memberIds);
            Assert.Contains(m2.UserId, memberIds);

            var mgrRow = body.Result.Members.Single(m => m.User.Id == manager.UserId);
            Assert.Equal(0, mgrRow.Totals.Delivery.Done);

            // contributors excludes mgr (done = 0): 2, never the roster size (3).
            Assert.Equal(2, body.Result.Team.Contributors);
            Assert.Equal(2, body.Result.TeamMedian.Contributors);

            // Pooled take-over counts (T3, m1 -> m2) are unaffected by
            // contributors - a take-over needs no completion.
            Assert.Equal(1, body.Result.Team.Totals.Attribution.TakeOvers);
            Assert.Equal(1, body.Result.Team.Totals.Attribution.TakenFrom);

            var m1Row = body.Result.Members.Single(m => m.User.Id == m1.UserId);
            var m2Row = body.Result.Members.Single(m => m.User.Id == m2.UserId);
            Assert.Equal(3, m1Row.Totals.Delivery.Done);
            Assert.Equal(2, m2Row.Totals.Delivery.Done);
        }

        // ---- R8 (2026-11-22 contract change): team ticketsWorked is a
        // pooled distinct-task count, like every other TeamTotals field
        // (R12) - no mean-of-members exception any more. A task handed over
        // mid-range counts for both holders at member scope, but once at
        // team scope, so the member sum can exceed the team total. ----

        [Fact]
        public async Task GetTeam_TicketsWorked_PooledDistinctCount_HandoverCountsOnceAtTeamScope()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var m1 = await CreateUserAndLoginAsync("Member", displayName: "M1");
            var m2 = await CreateUserAndLoginAsync("Member", displayName: "M2");

            // Task A: held by m1 for the whole week - counts once for m1.
            var taskA = await SeedTaskAsync("Task A", assigneeId: m1.UserId, dueDate: null);
            await SeedHistoryAsync(taskA.Id, TaskHistoryEvent.Created, Local(2026, 3, 1, 8, 0));
            await SeedHistoryAsync(taskA.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 0, 0), "Todo", "InProgress");

            // Task B: handed over from m1 to m2 mid-week - counts for both
            // m1 and m2 (R8), but once at team scope.
            var taskB = await SeedTaskAsync("Task B", assigneeId: m1.UserId, dueDate: null);
            await SeedHistoryAsync(taskB.Id, TaskHistoryEvent.Created, Local(2026, 3, 1, 8, 0));
            await SeedHistoryAsync(taskB.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 2, 0, 0), "Todo", "InProgress");
            await SeedHistoryAsync(taskB.Id, TaskHistoryEvent.AssigneeChanged, Local(2026, 3, 4, 9, 0), m1.UserId.ToString(), m2.UserId.ToString(), m2.UserId);
            await SeedHistoryAsync(taskB.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 6, 12, 0), "InProgress", "Review");

            var response = await manager.Client.GetAsync("api/v1/reports/team?from=2026-03-02&to=2026-03-08");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportTeamRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);

            var m1Row = body.Result!.Members.Single(m => m.User.Id == m1.UserId);
            var m2Row = body.Result.Members.Single(m => m.User.Id == m2.UserId);
            Assert.Equal(2, m1Row.Totals.Load!.TicketsWorked); // Task A, Task B
            Assert.Equal(1, m2Row.Totals.Load!.TicketsWorked); // Task B

            // Team scope pools across holders - Task B counts once, not
            // twice, so the team total (2: Task A, Task B) is less than the
            // member sum (2 + 1 = 3).
            Assert.Equal(2, body.Result.Team.Totals.Load!.TicketsWorked);
            Assert.True(m1Row.Totals.Load!.TicketsWorked + m2Row.Totals.Load!.TicketsWorked > body.Result.Team.Totals.Load!.TicketsWorked);
        }

        // ---- Heatmap: a week entirely before dataAvailableSince has no
        // data, so its ticketsWorked cell is null, not zero ----

        [Fact]
        public async Task GetTeam_Heatmap_NoDataWeek_ReturnsNullTicketsWorked()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var m1 = await CreateUserAndLoginAsync("Member", displayName: "Heatmap M1");

            // Earliest history row in the (reset) TaskHistories table is in
            // week 2 (03-09), so week 1 (03-02..03-08) is entirely before
            // dataAvailableSince.
            var task = await SeedTaskAsync("Week 2 task", assigneeId: m1.UserId, dueDate: null);
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.Created, Local(2026, 3, 9, 8, 0));
            await SeedHistoryAsync(task.Id, TaskHistoryEvent.StatusChanged, Local(2026, 3, 9, 9, 0), "Todo", "InProgress");

            var response = await manager.Client.GetAsync("api/v1/reports/team?from=2026-03-02&to=2026-03-15");
            var body = await response.Content.ReadFromJsonAsync<ResultRes<ReportTeamRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);

            var weekIndex1 = body.Result!.Heatmap.Weeks.FindIndex(w => w.Start == new DateOnly(2026, 3, 2));
            var weekIndex2 = body.Result.Heatmap.Weeks.FindIndex(w => w.Start == new DateOnly(2026, 3, 9));
            Assert.False(body.Result.Heatmap.Weeks[weekIndex1].HasData);
            Assert.True(body.Result.Heatmap.Weeks[weekIndex2].HasData);

            var m1Row = body.Result.Heatmap.Rows.Single(r => r.UserId == m1.UserId);
            Assert.Null(m1Row.TicketsWorked[weekIndex1]);
            Assert.Equal(1, m1Row.TicketsWorked[weekIndex2]);
        }

        // ---- AC19: export ----

        [Fact]
        public async Task Export_Manager_ReturnsCsvWithBomAndNoStoreAndAttachment()
        {
            var manager = await CreateUserAndLoginAsync("Manager");

            var response = await manager.Client.GetAsync("api/v1/reports/export?from=2026-03-02&to=2026-03-08&dataset=Buckets&granularity=Week");

            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var bom = Encoding.UTF8.GetPreamble();
            Assert.Equal(bom, bytes.Take(bom.Length).ToArray());

            var text = Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length);
            Assert.StartsWith("bucket_start,bucket_end,is_partial,done,", text);
        }

        // R17: the export log line carries the caller id, subject, range,
        // dataset and row count - and never the exported content (task
        // titles, display names). A dedicated capturing ILoggerFactory
        // swapped in for this one test's host (WithWebHostBuilder), since
        // the real pipeline is Serilog and no test-log-capture precedent
        // exists in this codebase yet.
        [Fact]
        public async Task Export_LogsCallerSubjectRangeDatasetRowCount_NeverReportContent()
        {
            var capturingFactory = new CapturingLoggerFactory();
            await using var hostFactory = _factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(capturingFactory))));

            var manager = await CreateUserAndLoginAsync("Manager", hostFactory);

            var secretTask = await SeedTaskAsync("Secret Project Nightingale", assigneeId: manager.UserId, dueDate: new DateOnly(2026, 3, 4));
            await SeedTrail(secretTask.Id, manager.UserId, Local(2026, 3, 2, 9, 0), Local(2026, 3, 2, 10, 0), Local(2026, 3, 3, 10, 0), Local(2026, 3, 4, 10, 0));

            var response = await manager.Client.GetAsync("api/v1/reports/export?from=2026-03-02&to=2026-03-08&dataset=Tasks&granularity=Week");
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

            var exportLog = capturingFactory.Messages.FirstOrDefault(m => m.Contains("Report export by"));
            Assert.NotNull(exportLog);
            Assert.Contains(manager.UserId.ToString(), exportLog);
            Assert.Contains("team", exportLog);
            Assert.Contains("2026-03-02", exportLog);
            Assert.Contains("2026-03-08", exportLog);
            Assert.Contains("Tasks", exportLog);
            Assert.Contains("rows=1", exportLog);

            Assert.DoesNotContain(capturingFactory.Messages, m => m.Contains("Secret") || m.Contains("Nightingale"));
        }

        [Fact]
        public async Task Export_Member_Scorecard_ReturnsValidationError_ForMalformedMetric()
        {
            var caller = await CreateUserAndLoginAsync("Member");

            var response = await caller.Client.GetAsync($"api/v1/reports/tasks?userId={caller.UserId}&from=2026-03-02&to=2026-03-08&metric=NotAMetric");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- AC18: drill-down ----

        [Fact]
        public async Task GetTasks_LateMetric_ReturnsExactlyTheLateTask()
        {
            var manager = await CreateUserAndLoginAsync("Manager");
            var member = await CreateUserAndLoginAsync("Member");

            var onTimeTask = await SeedTaskAsync("On time task", assigneeId: member.UserId, dueDate: new DateOnly(2026, 3, 4));
            await SeedTrail(onTimeTask.Id, member.UserId, Local(2026, 3, 2, 9, 0), Local(2026, 3, 2, 10, 0), Local(2026, 3, 3, 10, 0), Local(2026, 3, 4, 10, 0));

            var lateTask = await SeedTaskAsync("Late task", assigneeId: member.UserId, dueDate: new DateOnly(2026, 3, 4));
            await SeedTrail(lateTask.Id, member.UserId, Local(2026, 3, 2, 8, 0), Local(2026, 3, 2, 12, 0), Local(2026, 3, 3, 12, 0), Local(2026, 3, 5, 9, 0));

            var response = await manager.Client.GetAsync($"api/v1/reports/tasks?userId={member.UserId}&from=2026-03-02&to=2026-03-08&metric=Late");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<ReportTaskRes>>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.Error);
            var row = Assert.Single(body.Result!);
            Assert.Equal(lateTask.Id, row.TaskId);
        }

        // ---- Helpers ----

        private async Task<TaskItem> SeedTaskAsync(string title, long? assigneeId = null, DateOnly? dueDate = null)
        {
            await using var context = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = title,
                Status = TaskWorkflowStatus.Backlog,
                Priority = TaskPriority.Normal,
                AssigneeId = assigneeId,
                DueDate = dueDate,
                Version = Guid.NewGuid(),
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task SeedTrail(long taskId, long assigneeId, DateTimeOffset created, DateTimeOffset toInProgress, DateTimeOffset toReview, DateTimeOffset toDone)
        {
            await SeedHistoryAsync(taskId, TaskHistoryEvent.Created, created);
            await SeedHistoryAsync(taskId, TaskHistoryEvent.StatusChanged, toInProgress, "Todo", "InProgress");
            await SeedHistoryAsync(taskId, TaskHistoryEvent.StatusChanged, toReview, "InProgress", "Review");
            await SeedHistoryAsync(taskId, TaskHistoryEvent.StatusChanged, toDone, "Review", "Done");

            await using var context = _dbFixture.CreateContext();
            var task = await context.Set<TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == taskId);
            task.Status = TaskWorkflowStatus.Done;
            task.CompletedAt = toDone;
            await context.SaveChangesAsync();
        }

        private async Task<long> SeedHistoryAsync(long taskId, TaskHistoryEvent historyEvent, DateTimeOffset createdAt, string? fromValue = null, string? toValue = null, long? actorUserId = null)
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

            history.CreatedAt = createdAt;
            await context.SaveChangesAsync();

            return history.Id;
        }

        private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
            new(year, month, day, hour, minute, 0, TimeSpan.FromHours(7));

        private Task<(HttpClient Client, long UserId)> CreateUserAndLoginAsync(string roleName, string? displayName = null) =>
            CreateUserAndLoginAsync(roleName, _factory, displayName);

        private async Task<(HttpClient Client, long UserId)> CreateUserAndLoginAsync(string roleName, WebApplicationFactory<Program> factory, string? displayName = null)
        {
            var userName = $"r3s1-{Guid.NewGuid():N}";

            using (var scope = factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = new User
                {
                    UserName = userName,
                    Email = $"{userName}@taskflow.test",
                    EmailConfirmed = true,
                    LockoutEnabled = true,
                    DisplayName = displayName ?? userName,
                };

                var createResult = await userManager.CreateAsync(user, TestPassword);
                Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

                await using var context = _dbFixture.CreateContext();
                var roleEntity = await context.Set<Role>().SingleAsync(r => r.Name == roleName);
                context.Add(new UserRole { UserId = user.Id, RoleId = roleEntity.Id });
                await context.SaveChangesAsync();
            }

            var client = factory.CreateClient();
            var deviceGuid = Guid.NewGuid();
            var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new { deviceGuid, username = userName, password = TestPassword });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<ResultRes<LoginRes>>();

            Assert.True(loginResponse.IsSuccessStatusCode, $"login failed: {loginBody?.Error}");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Result!.Token);

            using (var scope = factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByNameAsync(userName);
                return (client, user!.Id);
            }
        }
    }
}
