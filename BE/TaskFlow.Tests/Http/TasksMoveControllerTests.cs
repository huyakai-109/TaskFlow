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
    // Slice 1.2: PUT /tasks/{id}/status. Exercises F01 AC1-5, AC7-16, AC20,
    // AC25, AC27-31 and F04 AC2, AC7, AC10-14, AC18 (f01-workflow.md,
    // f04-audit.md Technical contract). AC27 (invalid transition beats
    // capacity) and the capacity half of AC2 are NOT VERIFIED here - F02's
    // capacity guard does not exist yet.
    [Collection(DatabaseCollection.Name)]
    public class TasksMoveControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksMoveControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- Happy path (AC1-5, F04 AC2, AC7) ----

        [Fact]
        public async Task Move_AdvancesBacklogToTodo_AndRecordsStatusChangedEntry()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            var moved = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);

            Assert.Equal(TaskWorkflowStatus.Todo, moved.Status);
            Assert.NotEqual(task.Version, moved.Version);

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.StatusChanged);
            Assert.Equal(mover.UserId, entry.ActorUserId);
            Assert.Equal(nameof(TaskWorkflowStatus.Backlog), entry.FromValue);
            Assert.Equal(nameof(TaskWorkflowStatus.Todo), entry.ToValue);
        }

        [Fact]
        public async Task Move_AdvancesTodoToInProgress_WhenAssigneeIsSet()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var assignee = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client, assignee.UserId);
            var atTodo = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);

            var moved = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.InProgress, atTodo.Version);

            Assert.Equal(TaskWorkflowStatus.InProgress, moved.Status);

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.StatusChanged
                && h.ToValue == nameof(TaskWorkflowStatus.InProgress));
            Assert.Equal(mover.UserId, entry.ActorUserId);
            Assert.Equal(nameof(TaskWorkflowStatus.Todo), entry.FromValue);
        }

        [Fact]
        public async Task Move_AdvancesInProgressToReview()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.InProgress);

            var moved = await MoveAsync(mover, task.Id, TaskWorkflowStatus.Review, task.Version);

            Assert.Equal(TaskWorkflowStatus.Review, moved.Status);
        }

        [Fact]
        public async Task Move_ReviewToDone_ByNonAssignee_SetsCompletedAtAndClearsOverdue()
        {
            var (task, _, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            var nonAssigneeMover = await CreateAuthenticatedClientAsync("Manager");

            var moved = await MoveAsync(nonAssigneeMover.Client, task.Id, TaskWorkflowStatus.Done, task.Version);

            Assert.Equal(TaskWorkflowStatus.Done, moved.Status);
            Assert.NotNull(moved.CompletedAt);
            Assert.False(moved.IsOverdue);
        }

        [Fact]
        public async Task Move_ReviewToInProgress_ByNonAssignee_NotifiesTheAssignee()
        {
            var (task, _, assigneeId, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            var nonAssigneeMover = await CreateAuthenticatedClientAsync("Manager");

            var moved = await MoveAsync(nonAssigneeMover.Client, task.Id, TaskWorkflowStatus.InProgress, task.Version);

            Assert.Equal(TaskWorkflowStatus.InProgress, moved.Status);

            await using var context = _dbFixture.CreateContext();
            var notification = await context.Set<TaskNotification>().SingleAsync(n => n.TaskId == task.Id);
            Assert.Equal(assigneeId, notification.RecipientUserId);
            Assert.Equal(TaskNotificationEvent.ReturnedForRework, notification.Event);
            Assert.False(notification.IsRead);
        }

        [Fact]
        public async Task Move_ThreeStagesInOneDay_WriteThreeSeparateHistoryEntries_InOrder()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            var atTodo = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);
            var atInProgress = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.InProgress, atTodo.Version);
            await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Review, atInProgress.Version);

            await using var context = _dbFixture.CreateContext();
            var entries = await context.Set<TaskHistory>()
                .Where(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.StatusChanged)
                .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                .ToListAsync();

            Assert.Equal(3, entries.Count);
            Assert.Equal((nameof(TaskWorkflowStatus.Backlog), nameof(TaskWorkflowStatus.Todo)), (entries[0].FromValue, entries[0].ToValue));
            Assert.Equal((nameof(TaskWorkflowStatus.Todo), nameof(TaskWorkflowStatus.InProgress)), (entries[1].FromValue, entries[1].ToValue));
            Assert.Equal((nameof(TaskWorkflowStatus.InProgress), nameof(TaskWorkflowStatus.Review)), (entries[2].FromValue, entries[2].ToValue));
        }

        // ---- Rejected moves (AC7-14, AC20); each asserts F04 AC11 - no entry, no notification, task untouched ----

        [Fact]
        public async Task Move_Refuses_BacklogToInProgress()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Backlog);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.InProgress, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_BacklogToReview()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Backlog);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Review, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_BacklogToDone()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Backlog);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Done, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_TodoToReview()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Todo);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Review, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_TodoToDone()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Todo);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Done, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_InProgressToTodo()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.InProgress);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Todo, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_InProgressToBacklog()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.InProgress);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Backlog, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_InProgressToDone_CannotSkipReview()
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.InProgress);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, TaskWorkflowStatus.Done, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_ReviewToTodo()
        {
            var (task, _, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            var nonAssigneeMover = await CreateAuthenticatedClientAsync("Manager");
            await AssertRejectedWithNoSideEffectsAsync(nonAssigneeMover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_ReviewToBacklog()
        {
            var (task, _, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            var nonAssigneeMover = await CreateAuthenticatedClientAsync("Manager");
            await AssertRejectedWithNoSideEffectsAsync(nonAssigneeMover.Client, task.Id, TaskWorkflowStatus.Backlog, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Fact]
        public async Task Move_Refuses_DoneToAnyStatus_ThroughTheMoveOperation()
        {
            var (task, _, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Done);
            var someCaller = await CreateAuthenticatedClientAsync("Manager");

            // Reopen (Done -> Review) exists only on the reopen operation's
            // table (slice 1.3); through /status it must be InvalidTransition.
            await AssertRejectedWithNoSideEffectsAsync(someCaller.Client, task.Id, TaskWorkflowStatus.Review, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        [Theory]
        [InlineData(TaskWorkflowStatus.Backlog)]
        [InlineData(TaskWorkflowStatus.Todo)]
        [InlineData(TaskWorkflowStatus.InProgress)]
        public async Task Move_Refuses_SameStatus(TaskWorkflowStatus status)
        {
            var (task, mover, _, _) = await AdvanceToStatusAsync(status);
            await AssertRejectedWithNoSideEffectsAsync(mover, task.Id, status, task.Version, TaskControllerMS.Move.SameStatus);
        }

        [Fact]
        public async Task Move_Refuses_SameStatus_WhenAlreadyDone()
        {
            var (task, _, _, _) = await AdvanceToStatusAsync(TaskWorkflowStatus.Done);
            var someCaller = await CreateAuthenticatedClientAsync("Manager");
            await AssertRejectedWithNoSideEffectsAsync(someCaller.Client, task.Id, TaskWorkflowStatus.Done, task.Version, TaskControllerMS.Move.SameStatus);
        }

        // ---- The actor rule, D5 (AC16, AC28) ----

        [Fact]
        public async Task Move_Refuses_ReviewToDone_WhenActorIsTheAssignee()
        {
            var (task, _, _, assigneeClient) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            await AssertRejectedWithNoSideEffectsAsync(assigneeClient, task.Id, TaskWorkflowStatus.Done, task.Version, TaskControllerMS.Move.ActorIsAssignee);
        }

        [Fact]
        public async Task Move_Refuses_ReviewToInProgress_WhenActorIsTheAssignee()
        {
            var (task, _, _, assigneeClient) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            await AssertRejectedWithNoSideEffectsAsync(assigneeClient, task.Id, TaskWorkflowStatus.InProgress, task.Version, TaskControllerMS.Move.ActorIsAssignee);
        }

        [Fact]
        public async Task Move_Refuses_ReviewToBacklog_WithInvalidTransitionCode_EvenWhenActorIsTheAssignee()
        {
            // AC28 (D3): transition validity is checked before the actor rule,
            // so an invalid move by the assignee still returns InvalidTransition.
            var (task, _, _, assigneeClient) = await AdvanceToStatusAsync(TaskWorkflowStatus.Review);
            await AssertRejectedWithNoSideEffectsAsync(assigneeClient, task.Id, TaskWorkflowStatus.Backlog, task.Version, TaskControllerMS.Move.InvalidTransition);
        }

        // ---- Shape (AC15) ----

        [Fact]
        public async Task Move_ReturnsShapeError_WhenStatusIsAnUnknownString()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/tasks/{task.Id}/status")
            {
                Content = JsonContent.Create(new { status = "Bogus", version = task.Version }),
            };
            var response = await mover.Client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
        }

        [Fact]
        public async Task Move_ReturnsShapeError_WhenStatusIsOutOfRange()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/tasks/{task.Id}/status")
            {
                Content = JsonContent.Create(new { status = 99, version = task.Version }),
            };
            var response = await mover.Client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.NotEmpty(body.Validates!);
        }

        // ---- Existence, permission, authentication (AC24-equivalent, AC25, AC26) ----

        [Fact]
        public async Task Move_Refuses_WhenTaskDoesNotExist()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");

            var response = await mover.Client.PutAsJsonAsync("api/v1/tasks/999999/status", new { status = "Todo", version = Guid.NewGuid() });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Move.NotFound, body!.ErrorCode);
        }

        [Fact]
        public async Task Move_Refuses_WhenCallerLacksMoveTasksPermission()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);
            var noPermissionCaller = await CreateAuthenticatedClientAsync(role: null);

            var response = await noPermissionCaller.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/status", new { status = "Todo", version = task.Version });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            // F04 AC14: no entry written, no actor recorded anywhere.
            await using var context = _dbFixture.CreateContext();
            Assert.False(await context.Set<TaskHistory>().AnyAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.StatusChanged));
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Equal(TaskWorkflowStatus.Backlog, reloaded.Status);
        }

        [Fact]
        public async Task Move_Refuses_WhenCallerIsUnauthenticated()
        {
            var client = _factory.CreateClient();

            var response = await client.PutAsJsonAsync("api/v1/tasks/1/status", new { status = "Todo", version = Guid.NewGuid() });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---- Concurrency, the gate (AC29, AC30, AC31) ----

        [Fact]
        public async Task Move_StaleVersion_IsRefusedWithConflict()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);
            var staleVersion = task.Version;

            // Bump the version with a real move; staleVersion no longer matches.
            await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);

            await AssertRejectedWithNoSideEffectsAsync(mover.Client, task.Id, TaskWorkflowStatus.InProgress, staleVersion, TaskControllerMS.Move.Conflict);
        }

        [Fact]
        public async Task Move_TwoRacingSaveChanges_OnSeparateConnections_ThrowsConcurrencyException_ServiceTranslatesToConflict()
        {
            // AC29's actual mechanism, independent of the explicit version
            // check in TaskService.Move (which never fires here: both scopes
            // read the same original Version before either writes). Two real
            // MySQL connections (two MyDbContext instances), not mocked.
            var mover = await CreateAuthenticatedClientAsync("Member");
            var assignee = await CreateAuthenticatedClientAsync("Member");
            var created = await CreateTaskAsync(mover.Client, assignee.UserId);
            var atTodo = await MoveAsync(mover.Client, created.Id, TaskWorkflowStatus.Todo, created.Version);

            var configuration = _factory.Services.GetRequiredService<IConfiguration>();

            await using var contextA = _dbFixture.CreateContext();
            await using var contextB = _dbFixture.CreateContext();
            var serviceA = new TaskService(configuration, new UnitOfWork(contextA, new HttpContextAccessor()));
            var serviceB = new TaskService(configuration, new UnitOfWork(contextB, new HttpContextAccessor()));

            var moveDto = new TaskMoveReqDto { Status = TaskWorkflowStatus.InProgress, Version = atTodo.Version };
            var permissions = new[] { Permissions.Tasks.MoveTasks };

            var taskA = serviceA.Move(created.Id, moveDto, mover.UserId, permissions);
            var taskB = serviceB.Move(created.Id, moveDto, mover.UserId, permissions);
            await Task.WhenAll(taskA, taskB);

            var outcomes = new[] { await taskA, await taskB };
            Assert.Single(outcomes, o => o.ErrorCode == string.Empty);
            Assert.Single(outcomes, o => o.ErrorCode == TaskControllerMS.Move.Conflict);

            await using var assertContext = _dbFixture.CreateContext();
            var finalTask = await assertContext.Set<TaskItem>().SingleAsync(t => t.Id == created.Id);
            Assert.Equal(TaskWorkflowStatus.InProgress, finalTask.Status);

            // AC31/F04 AC10: exactly one StatusChanged entry for the winner,
            // none for the loser.
            var statusChangedCount = await assertContext.Set<TaskHistory>()
                .CountAsync(h => h.TaskId == created.Id && h.Event == TaskHistoryEvent.StatusChanged && h.ToValue == nameof(TaskWorkflowStatus.InProgress));
            Assert.Equal(1, statusChangedCount);

            // This move is Todo -> InProgress, not Review -> InProgress, so no
            // notification is expected from either racer.
            Assert.Equal(0, await assertContext.Set<TaskNotification>().CountAsync(n => n.TaskId == created.Id));
        }

        [Fact]
        public async Task Move_TwoConcurrentHttpRequests_ExactlyOneSucceeds_TaskAdvancesExactlyOneStep()
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var otherCaller = await CreateAuthenticatedClientAsync("Manager");
            var assignee = await CreateAuthenticatedClientAsync("Member");
            var created = await CreateTaskAsync(mover.Client, assignee.UserId);
            var atTodo = await MoveAsync(mover.Client, created.Id, TaskWorkflowStatus.Todo, created.Version);

            var payload = new { status = "InProgress", version = atTodo.Version };

            var task1 = mover.Client.PutAsJsonAsync($"api/v1/tasks/{created.Id}/status", payload);
            var task2 = otherCaller.Client.PutAsJsonAsync($"api/v1/tasks/{created.Id}/status", payload);
            await Task.WhenAll(task1, task2);

            var responses = new[] { await task1, await task2 };
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

            var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.BadRequest);
            var conflictBody = await conflictResponse.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.Equal(TaskControllerMS.Move.Conflict, conflictBody!.ErrorCode);

            await using var context = _dbFixture.CreateContext();
            var finalTask = await context.Set<TaskItem>().SingleAsync(t => t.Id == created.Id);
            Assert.Equal(TaskWorkflowStatus.InProgress, finalTask.Status);
        }

        // ---- F04 AC18, DB level only (the read endpoint lands in slice 1.4) ----

        [Fact]
        public async Task TaskHistories_OrderByCreatedAtThenId_IsStable_WhenTimestampsTie()
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

            async Task<long[]> ReadOrderAsync()
            {
                await using var context = _dbFixture.CreateContext();
                return await context.Set<TaskHistory>()
                    .Where(h => h.TaskId == task.Id)
                    .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                    .Select(h => h.Id)
                    .ToArrayAsync();
            }

            var firstRead = await ReadOrderAsync();
            var secondRead = await ReadOrderAsync();

            Assert.Equal(new[] { first.Id, second.Id }, firstRead);
            Assert.Equal(firstRead, secondRead);
        }

        // ---- Helpers ----

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, long? assigneeId = null, string priority = "Normal")
        {
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = $"Move test {Guid.NewGuid():N}",
                priority,
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

        private async Task AssertRejectedWithNoSideEffectsAsync(HttpClient client, long taskId, TaskWorkflowStatus targetStatus, Guid version, string expectedErrorCode)
        {
            TaskWorkflowStatus statusBefore;
            Guid versionBefore;
            int historyCountBefore;
            int notificationCountBefore;

            await using (var before = _dbFixture.CreateContext())
            {
                var task = await before.Set<TaskItem>().SingleAsync(t => t.Id == taskId);
                statusBefore = task.Status;
                versionBefore = task.Version;
                historyCountBefore = await before.Set<TaskHistory>().CountAsync(h => h.TaskId == taskId);
                notificationCountBefore = await before.Set<TaskNotification>().CountAsync(n => n.TaskId == taskId);
            }

            var response = await client.PutAsJsonAsync($"api/v1/tasks/{taskId}/status", new { status = targetStatus.ToString(), version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.Equal(expectedErrorCode, body.ErrorCode);

            await using (var after = _dbFixture.CreateContext())
            {
                var task = await after.Set<TaskItem>().SingleAsync(t => t.Id == taskId);
                Assert.Equal(statusBefore, task.Status);
                Assert.Equal(versionBefore, task.Version);
                Assert.Equal(historyCountBefore, await after.Set<TaskHistory>().CountAsync(h => h.TaskId == taskId));
                Assert.Equal(notificationCountBefore, await after.Set<TaskNotification>().CountAsync(n => n.TaskId == taskId));
            }
        }

        // Walks a freshly created task along the legal path to the given
        // status. Backlog->Todo->InProgress->Review is driven by `mover`
        // (not the assignee); Review->Done is driven by a third caller so D5
        // holds for every status this helper can reach.
        private async Task<(TaskDetailRes Task, HttpClient Mover, long AssigneeId, HttpClient AssigneeClient)> AdvanceToStatusAsync(TaskWorkflowStatus target)
        {
            var mover = await CreateAuthenticatedClientAsync("Member");
            var assignee = await CreateAuthenticatedClientAsync("Member");

            var current = await CreateTaskAsync(mover.Client, assignee.UserId);
            if (target == TaskWorkflowStatus.Backlog)
            {
                return (current, mover.Client, assignee.UserId, assignee.Client);
            }

            current = await MoveAsync(mover.Client, current.Id, TaskWorkflowStatus.Todo, current.Version);
            if (target == TaskWorkflowStatus.Todo)
            {
                return (current, mover.Client, assignee.UserId, assignee.Client);
            }

            current = await MoveAsync(mover.Client, current.Id, TaskWorkflowStatus.InProgress, current.Version);
            if (target == TaskWorkflowStatus.InProgress)
            {
                return (current, mover.Client, assignee.UserId, assignee.Client);
            }

            current = await MoveAsync(mover.Client, current.Id, TaskWorkflowStatus.Review, current.Version);
            if (target == TaskWorkflowStatus.Review)
            {
                return (current, mover.Client, assignee.UserId, assignee.Client);
            }

            var closer = await CreateAuthenticatedClientAsync("Manager");
            current = await MoveAsync(closer.Client, current.Id, TaskWorkflowStatus.Done, current.Version);
            return (current, mover.Client, assignee.UserId, assignee.Client);
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice12-{Guid.NewGuid():N}";
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
