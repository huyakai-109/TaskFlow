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
    // Slice 5.1: ITaskOverdueScanService.Run, called directly from a created
    // scope exactly as the future Quartz job will (no HttpContext, no HTTP
    // surface). Exercises F05 AC1-AC6, AC16-AC23, AC25, AC26, the data side of
    // AC24, and regression coverage for AC7-AC9, AC11, AC13-AC15
    // (f05-overdue.md Technical contract T1-T4, T7).
    [Collection(DatabaseCollection.Name)]
    public class TaskOverdueScanServiceTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice51Test@12345";
        private static readonly DateOnly FixedToday = new(2030, 1, 10);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TaskOverdueScanServiceTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- AC1/AC5: InProgress and Backlog, past due, both flagged ----

        [Fact]
        public async Task Run_FlagsPastDueTask_InProgressAndBacklog()
        {
            var inProgress = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-3));
            var backlog = await SeedTaskAsync(TaskWorkflowStatus.Backlog, FixedToday.AddDays(-1));

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(2, result.Examined);
            Assert.Equal(2, result.Flagged);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(0, result.Failed);

            await AssertFlaggedAsync(inProgress);
            await AssertFlaggedAsync(backlog);
        }

        // ---- AC2: due today is not overdue ----

        [Fact]
        public async Task Run_DoesNotFlag_WhenDueDateIsToday()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday);

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(0, result.Flagged);
            await AssertNotFlaggedAsync(task);
        }

        // ---- AC3: Done is never flagged ----

        [Fact]
        public async Task Run_DoesNotFlag_DoneTask_EvenWhenPastDue()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.Done, FixedToday.AddDays(-7));

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(0, result.Flagged);
            await AssertNotFlaggedAsync(task);
        }

        // ---- AC4: no due date, never flagged ----

        [Fact]
        public async Task Run_DoesNotFlag_TaskWithNoDueDate()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, dueDate: null);

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(0, result.Examined);
            Assert.Equal(0, result.Flagged);
            await AssertNotFlaggedAsync(task);
        }

        // ---- Keyset boundary: 150 candidates cross the 100-row batch size ----

        [Fact]
        public async Task Run_WithMoreThanOneBatch_FlagsEveryCandidate_AndIsIdempotentOnRerun()
        {
            const int seedCount = 150;
            var tasks = new List<TaskItem>();
            for (var i = 0; i < seedCount; i++)
            {
                tasks.Add(await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-1)));
            }

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(seedCount, result.Examined);
            Assert.Equal(seedCount, result.Flagged);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(0, result.Failed);

            await using (var context = _dbFixture.CreateContext())
            {
                foreach (var task in tasks)
                {
                    var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
                    Assert.True(reloaded.IsOverdue);

                    var entryCount = await context.Set<TaskHistory>()
                        .CountAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked);
                    Assert.Equal(1, entryCount);
                }
            }

            var secondResult = await RunScanAsync(FixedToday);

            Assert.Equal(0, secondResult.Flagged);
        }

        // ---- AC25: soft-deleted task, never flagged, no history row ----

        [Fact]
        public async Task Run_DoesNotFlag_SoftDeletedTask_AndWritesNoHistory()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-5), isDeleted: true);

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(0, result.Examined);
            Assert.Equal(0, result.Flagged);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == task.Id);
            Assert.False(reloaded.IsOverdue);
            Assert.False(await context.Set<TaskHistory>().AnyAsync(h => h.TaskId == task.Id));
        }

        // ---- AC6: "today" follows the configured zone, not UTC ----

        [Fact]
        public void ToDateInZone_ResolvesVietnamDate_NotUtcDate()
        {
            var instant = new DateTimeOffset(2026, 10, 20, 17, 30, 0, TimeSpan.Zero);

            var resolved = TaskFlow.Common.Helpers.DateTimeHelper.ToDateInZone(instant, "Asia/Ho_Chi_Minh");

            Assert.Equal(new DateOnly(2026, 10, 21), resolved);
        }

        [Fact]
        public async Task Run_FlagsTask_UsingDateDerivedFromTheConfiguredZone()
        {
            // A task due 20 Oct is overdue once "today" (passed in, already
            // resolved in the zone - T1) is 21 Oct, mirroring the UTC/VN
            // boundary from AC6/the contract example.
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, new DateOnly(2026, 10, 20));

            var result = await RunScanAsync(new DateOnly(2026, 10, 21));

            Assert.Equal(1, result.Flagged);
            await AssertFlaggedAsync(task);
        }

        // ---- AC16/AC22: several tasks, one entry each, actor is System (null) ----

        [Fact]
        public async Task Run_WritesExactlyOneOverdueMarkedEntryPerTask_WithNullActor()
        {
            var tasks = new List<TaskItem>();
            for (var i = 0; i < 3; i++)
            {
                tasks.Add(await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-1 - i)));
            }

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(3, result.Flagged);

            await using var context = _dbFixture.CreateContext();
            foreach (var task in tasks)
            {
                var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked);
                Assert.Null(entry.ActorUserId);
            }
        }

        // ---- AC17: re-run the same day flags nothing new ----

        [Fact]
        public async Task Run_SecondRunSameDay_FlagsNothing_AndWritesNoNewHistory()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-2));
            await RunScanAsync(FixedToday);

            var secondResult = await RunScanAsync(FixedToday);

            Assert.Equal(0, secondResult.Flagged);

            await using var context = _dbFixture.CreateContext();
            var count = await context.Set<TaskHistory>().CountAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked);
            Assert.Equal(1, count);
        }

        // ---- AC18: empty candidate set ----

        [Fact]
        public async Task Run_WithNoCandidates_CompletesSuccessfully_WithAllCountersZero()
        {
            var result = await RunScanAsync(FixedToday);

            Assert.Equal(0, result.Examined);
            Assert.Equal(0, result.Flagged);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(0, result.Failed);
        }

        // ---- AC19/AC26: 1, 2 and 30 days late - one entry each, no per-day duplication ----

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(30)]
        public async Task Run_FlagsTaskOnce_RegardlessOfHowManyDaysLate(int daysLate)
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-daysLate));

            var result = await RunScanAsync(FixedToday);

            Assert.Equal(1, result.Flagged);

            await using var context = _dbFixture.CreateContext();
            var count = await context.Set<TaskHistory>().CountAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked);
            Assert.Equal(1, count);
        }

        // ---- AC20: one task fails, the rest still flag (D3) ----

        [Fact]
        public async Task Run_OneTaskFails_ViaInjectedTrigger_RestStillFlagged_AndFailureLogged()
        {
            var doomed = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-1));
            var healthy = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-2));

            await using var context = _dbFixture.CreateContext();

            // Identifiers here are built from a freshly generated identity id,
            // never external input - string composition is safe (same
            // reasoning as DatabaseFixture.ResetTablesAsync).
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($@"
                CREATE TRIGGER trg_fail_task_{doomed.Id}
                BEFORE INSERT ON TaskHistories
                FOR EACH ROW
                BEGIN
                    IF NEW.TaskId = {doomed.Id} AND NEW.Event = {(int)TaskHistoryEvent.OverdueMarked} THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'injected failure for slice 5.1 AC20';
                    END IF;
                END");
#pragma warning restore EF1002

            try
            {
                var result = await RunScanAsync(FixedToday);

                Assert.Equal(2, result.Examined);
                Assert.Equal(1, result.Flagged);
                Assert.Equal(1, result.Failed);
                Assert.Equal(0, result.Skipped);

                await AssertNotFlaggedAsync(doomed);
                await AssertFlaggedAsync(healthy);
            }
            finally
            {
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync($"DROP TRIGGER IF EXISTS trg_fail_task_{doomed.Id}");
#pragma warning restore EF1002
            }
        }

        // ---- AC21: scan racing an HTTP move to Done ----

        [Fact]
        public async Task Run_RacingHttpMoveToDone_NeverFails_AndEndsConsistent()
        {
            for (var round = 0; round < 10; round++)
            {
                // D12: mover is a Manager (AssignAnyone) because it creates
                // the task assigned to a third party.
                var mover = await CreateAuthenticatedClientAsync("Manager");
                var assignee = await CreateAuthenticatedClientAsync("Member");
                var created = await CreateTaskAsync(mover.Client, assignee.UserId);
                var atTodo = await MoveAsync(mover.Client, created.Id, TaskWorkflowStatus.Todo, created.Version);

                // D11: holder-only - InProgress/Review moves are the assignee's.
                var atInProgress = await MoveAsync(assignee.Client, created.Id, TaskWorkflowStatus.InProgress, atTodo.Version);
                var closer = await CreateAuthenticatedClientAsync("Manager");
                var atReview = await MoveAsync(assignee.Client, created.Id, TaskWorkflowStatus.Review, atInProgress.Version);

                // Backdate the due date directly (Move/Create validate against
                // today; the race needs a task that is already past due).
                await using (var context = _dbFixture.CreateContext())
                {
                    var row = await context.Set<TaskItem>().SingleAsync(t => t.Id == created.Id);
                    row.DueDate = FixedToday.AddDays(-1);
                    await context.SaveChangesAsync();
                }

                var moveToDone = closer.Client.PutAsJsonAsync($"api/v1/tasks/{created.Id}/status", new { status = "Done", version = atReview.Version });
                var scan = RunScanAsync(FixedToday);
                await Task.WhenAll(moveToDone, scan);

                var moveResponse = await moveToDone;
                Assert.NotEqual(HttpStatusCode.InternalServerError, moveResponse.StatusCode);

                await using var assertContext = _dbFixture.CreateContext();
                var finalTask = await assertContext.Set<TaskItem>().SingleAsync(t => t.Id == created.Id);
                var overdueEntries = await assertContext.Set<TaskHistory>()
                    .CountAsync(h => h.TaskId == created.Id && h.Event == TaskHistoryEvent.OverdueMarked);

                if (finalTask.Status == TaskWorkflowStatus.Done)
                {
                    Assert.False(finalTask.IsOverdue);
                    Assert.Equal(0, overdueEntries);
                }
                else
                {
                    Assert.True(finalTask.IsOverdue);
                    Assert.Equal(1, overdueEntries);
                    Assert.Equal(HttpStatusCode.BadRequest, moveResponse.StatusCode);
                    var body = await moveResponse.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
                    Assert.Equal(TaskControllerMS.Move.Conflict, body!.ErrorCode);
                }
            }
        }

        // ---- AC23: flag, clear via due-date extension (AC7), flag again (D7) ----

        [Fact]
        public async Task Run_FlagsASecondTime_AfterDueDateWasExtendedAndHasPassedAgain()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-1));
            await RunScanAsync(FixedToday);
            await AssertFlaggedAsync(task);

            var member = await CreateAuthenticatedClientAsync("Manager");
            Guid currentVersion;
            await using (var context = _dbFixture.CreateContext())
            {
                var row = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
                currentVersion = row.Version;
            }

            var extended = await UpdateDueDateAsync(member.Client, task.Id, FixedToday.AddDays(10), currentVersion);
            Assert.False(extended.IsOverdue);

            var passedAgain = FixedToday.AddDays(15);
            var secondResult = await RunScanAsync(passedAgain);

            Assert.Equal(1, secondResult.Flagged);

            await using var assertContext = _dbFixture.CreateContext();
            var entries = await assertContext.Set<TaskHistory>()
                .CountAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked);
            Assert.Equal(2, entries);
        }

        // ---- AC24 (data side): two Run calls in parallel scopes over shared candidates ----

        [Fact]
        public async Task Run_TwoParallelScans_OverSharedCandidates_EachTaskExactlyOneEntry_NoFailures()
        {
            for (var round = 0; round < 5; round++)
            {
                await _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");

                var tasks = new List<TaskItem>();
                for (var i = 0; i < 20; i++)
                {
                    tasks.Add(await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-1 - (i % 5))));
                }

                var configuration = _factory.Services.GetRequiredService<IConfiguration>();
                await using var contextA = _dbFixture.CreateContext();
                await using var contextB = _dbFixture.CreateContext();
                var serviceA = new TaskOverdueScanService(new UnitOfWork(contextA, new HttpContextAccessor()), NullLogger<TaskOverdueScanService>.Instance);
                var serviceB = new TaskOverdueScanService(new UnitOfWork(contextB, new HttpContextAccessor()), NullLogger<TaskOverdueScanService>.Instance);

                var runA = serviceA.Run(FixedToday, CancellationToken.None);
                var runB = serviceB.Run(FixedToday, CancellationToken.None);
                await Task.WhenAll(runA, runB);

                var resultA = await runA;
                var resultB = await runB;

                Assert.Equal(0, resultA.Failed);
                Assert.Equal(0, resultB.Failed);
                Assert.Equal(20, resultA.Flagged + resultB.Flagged);

                await using var assertContext = _dbFixture.CreateContext();
                foreach (var task in tasks)
                {
                    var count = await assertContext.Set<TaskHistory>().CountAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked);
                    Assert.Equal(1, count);
                    var reloaded = await assertContext.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
                    Assert.True(reloaded.IsOverdue);
                }
            }
        }

        // ---- Matrix: every status x {past, today, future, null} vs TaskOverdueRule ----

        [Theory]
        [MemberData(nameof(StatusDateMatrix))]
        public void IsOverdue_MatchesTheSharedRule_ForEveryStatusAndDateCombination(TaskWorkflowStatus status, DateOnly? dueDate, bool expected)
        {
            var actual = TaskOverdueRule.IsOverdue(dueDate, status, FixedToday);
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> StatusDateMatrix()
        {
            var past = FixedToday.AddDays(-1);
            var future = FixedToday.AddDays(1);

            foreach (var status in Enum.GetValues<TaskWorkflowStatus>())
            {
                var isDone = status == TaskWorkflowStatus.Done;
                yield return new object[] { status, past, !isDone };
                yield return new object[] { status, FixedToday, false };
                yield return new object[] { status, future, false };
                yield return new object[] { status, (DateOnly?)null, false };
            }
        }

        // ---- Regression: AC7-AC9, AC11, AC13-AC15 already hold in TaskService (T0) ----

        [Fact]
        public async Task Regression_ExtendingDueDateIntoTheFuture_ClearsOverdue_WithinTheSameRequest()
        {
            // AC7
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-5), isOverdue: true);
            var member = await CreateAuthenticatedClientAsync("Manager");

            var updated = await UpdateDueDateAsync(member.Client, task.Id, FixedToday.AddDays(5), task.Version);

            Assert.False(updated.IsOverdue);
        }

        [Fact]
        public async Task Regression_ApprovingIntoDone_ClearsOverdue_RegardlessOfHowLate()
        {
            // AC8
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var assignee = await CreateAuthenticatedClientAsync("Member");
            var closer = await CreateAuthenticatedClientAsync("Manager");
            var created = await CreateTaskAsync(mover.Client, assignee.UserId);
            var atTodo = await MoveAsync(mover.Client, created.Id, TaskWorkflowStatus.Todo, created.Version);
            var atInProgress = await MoveAsync(assignee.Client, created.Id, TaskWorkflowStatus.InProgress, atTodo.Version);
            var atReview = await MoveAsync(assignee.Client, created.Id, TaskWorkflowStatus.Review, atInProgress.Version);

            await using (var context = _dbFixture.CreateContext())
            {
                var row = await context.Set<TaskItem>().SingleAsync(t => t.Id == created.Id);
                row.DueDate = FixedToday.AddDays(-30);
                row.IsOverdue = true;
                await context.SaveChangesAsync();
            }

            var reloaded = await GetTaskAsync(created.Id);
            var done = await MoveAsync(closer.Client, created.Id, TaskWorkflowStatus.Done, reloaded.Version);

            Assert.False(done.IsOverdue);
        }

        [Fact]
        public async Task Regression_RemovingDueDateEntirely_ClearsOverdue()
        {
            // AC9
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-5), isOverdue: true);
            var member = await CreateAuthenticatedClientAsync("Manager");

            var updated = await UpdateDueDateAsync(member.Client, task.Id, null, task.Version);

            Assert.False(updated.IsOverdue);
        }

        [Fact]
        public async Task Regression_Reopen_RecomputesOverdue_FromCurrentDueDate_RatherThanCarryingItOver()
        {
            // AC11
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var assignee = await CreateAuthenticatedClientAsync("Member");
            var closer = await CreateAuthenticatedClientAsync("Manager");
            var created = await CreateTaskAsync(mover.Client, assignee.UserId);
            var atTodo = await MoveAsync(mover.Client, created.Id, TaskWorkflowStatus.Todo, created.Version);
            var atInProgress = await MoveAsync(assignee.Client, created.Id, TaskWorkflowStatus.InProgress, atTodo.Version);
            var atReview = await MoveAsync(assignee.Client, created.Id, TaskWorkflowStatus.Review, atInProgress.Version);
            var done = await MoveAsync(closer.Client, created.Id, TaskWorkflowStatus.Done, atReview.Version);

            await using (var context = _dbFixture.CreateContext())
            {
                var row = await context.Set<TaskItem>().SingleAsync(t => t.Id == created.Id);
                row.DueDate = FixedToday.AddDays(5);
                await context.SaveChangesAsync();
            }

            var reloaded = await GetTaskAsync(created.Id);
            var reopenResponse = await closer.Client.PutAsJsonAsync($"api/v1/tasks/{created.Id}/reopen", new { reason = "Needs another pass", version = reloaded.Version });
            var reopenBody = await reopenResponse.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(reopenResponse.IsSuccessStatusCode && reopenBody!.Success);
            Assert.False(reopenBody.Result!.IsOverdue);
        }

        [Fact]
        public async Task Regression_SettingDueDateToToday_IsAccepted_AndNotOverdue()
        {
            // AC13
            var member = await CreateAuthenticatedClientAsync("Manager");
            var task = await CreateTaskAsync(member.Client);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var updated = await UpdateDueDateAsync(member.Client, task.Id, today, task.Version);

            Assert.False(updated.IsOverdue);
        }

        [Fact]
        public async Task Regression_CallerWithoutUpdateTasksPermission_CannotChangeDueDate()
        {
            // AC15
            var creator = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(creator.Client);
            var noPermissionCaller = await CreateAuthenticatedClientAsync(role: null);

            var response = await noPermissionCaller.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/due-date", new { dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), version = task.Version });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Null(reloaded.DueDate);
        }

        // ---- D8 fix (T7, AC14): an unchanged past due date is never re-validated ----

        [Fact]
        public async Task UpdateDueDate_AllowsSameValuePastDate_BecauseItHasNotChanged()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-10));
            var member = await CreateAuthenticatedClientAsync("Manager");

            // Same (already-past) value, re-sent verbatim - D8 says this is not
            // a change, so the past-date guard must not fire.
            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/due-date", new { dueDate = FixedToday.AddDays(-10), version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
        }

        [Fact]
        public async Task Update_OfUnrelatedField_Succeeds_OnATaskWithAnUntouchedPastDueDate()
        {
            var task = await SeedTaskAsync(TaskWorkflowStatus.InProgress, FixedToday.AddDays(-10));
            var member = await CreateAuthenticatedClientAsync("Member");

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}", new { title = "Renamed, due date untouched", version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal("Renamed, due date untouched", body.Result!.Title);
        }

        // ---- Helpers ----

        private async Task<TaskOverdueScanResultDto> RunScanAsync(DateOnly today)
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITaskOverdueScanService>();
            return await service.Run(today, CancellationToken.None);
        }

        private async Task<TaskItem> SeedTaskAsync(TaskWorkflowStatus status, DateOnly? dueDate, bool isOverdue = false, bool isDeleted = false)
        {
            await using var context = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = $"Scan test {Guid.NewGuid():N}",
                Status = status,
                Priority = TaskPriority.Normal,
                DueDate = dueDate,
                IsOverdue = isOverdue,
                IsDeleted = isDeleted,
                Version = Guid.NewGuid(),
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task AssertFlaggedAsync(TaskItem task)
        {
            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.True(reloaded.IsOverdue);
            Assert.True(await context.Set<TaskHistory>().AnyAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked));
        }

        private async Task AssertNotFlaggedAsync(TaskItem task)
        {
            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.False(reloaded.IsOverdue);
            Assert.False(await context.Set<TaskHistory>().AnyAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.OverdueMarked));
        }

        private async Task<TaskDetailRes> GetTaskAsync(long id)
        {
            var admin = await CreateAuthenticatedClientAsync("Manager");
            var response = await admin.Client.GetAsync($"api/v1/tasks/{id}");
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

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, long? assigneeId = null)
        {
            // f01-workflow.md Revision R2, D13: priority is sent only when
            // actually requested - a Member sending it, even the default
            // value, is refused (AC35). None of this file's callers need a
            // specific priority, so it is simply never sent.
            var payload = new Dictionary<string, object?> { ["title"] = $"Scan http test {Guid.NewGuid():N}" };
            if (assigneeId.HasValue)
            {
                payload["assigneeId"] = assigneeId.Value;
            }

            var response = await client.PostAsJsonAsync("api/v1/tasks", payload);
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

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice51-{Guid.NewGuid():N}";
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
