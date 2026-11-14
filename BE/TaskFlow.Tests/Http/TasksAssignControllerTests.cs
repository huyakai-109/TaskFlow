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
    // Slice 3.2: PUT /tasks/{id}/assignee capacity guard
    // (f02-capacity.md Technical contract T3/T4). Exercises F02 AC7-AC10,
    // AC16 (assign), AC17 (nonexistent + soft-deleted id only), AC22
    // (assign), and the AC18 assign variants. AC12-14 (clearing the
    // assignee) are covered here too since D5's clarified scope
    // (InProgress/Review/Done) is the same guard this slice adds.
    // Reassignment happy-path/AssigneeNotFound for a non-active task was
    // already covered pre-F02 in TasksReadsEditsControllerTests and is not
    // duplicated here.
    [Collection(DatabaseCollection.Name)]
    public class TasksAssignControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public TasksAssignControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ---- Reassignment and capacity (AC7-10) ----

        [Fact]
        public async Task Assign_Refuses_WhenTargetAlreadyAtCapacity()
        {
            // AC7: the check applies to the new assignee, not the current one.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);
            for (var i = 0; i < 3; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            string bUserName;
            await using (var context = _dbFixture.CreateContext())
            {
                bUserName = (await context.Set<User>().SingleAsync(u => u.Id == b.UserId)).UserName!;
            }

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = b.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.CapacityReached, body!.ErrorCode);
            Assert.Equal($"{bUserName} already has 3 tasks in progress, which is the limit.", body.Error);

            await using var assertContext = _dbFixture.CreateContext();
            var reloaded = await assertContext.Set<TaskItem>().SingleAsync(t => t.Id == task.Id);
            Assert.Equal(a.UserId, reloaded.AssigneeId);
            Assert.Equal(task.Version, reloaded.Version);
            Assert.Equal(1, await CountActiveInProgressAsync(a.UserId));
            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        [Fact]
        public async Task Assign_Allows_WhenTargetHasTwoActive_AndOldAssigneeDropsByOne()
        {
            // AC8
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);
            for (var i = 0; i < 2; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = b.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
            Assert.Equal(0, await CountActiveInProgressAsync(a.UserId));
        }

        [Fact]
        public async Task Assign_Allows_ReassignmentToSameAssignee_AtExactlyThree()
        {
            // AC9/D3: a no-op reassignment is never refused for capacity.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            TaskDetailRes? task = null;
            for (var i = 0; i < 3; i++)
            {
                task = await CreateInProgressTaskAsync(mover.Client, a);
            }

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task!.Id}/assignee", new { assigneeId = a.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(3, await CountActiveInProgressAsync(a.UserId));
        }

        [Fact]
        public async Task Assign_Allows_WhenTaskIsNotInProgress_EvenIfTargetIsAtCapacity()
        {
            // AC10: the limit constrains active work only.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 3; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var task = await CreateTaskAsync(mover.Client);

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = b.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        // ---- Mandatory assignee while active, clarified D5 (AC12-14) ----

        [Fact]
        public async Task Assign_Refuses_ClearingAssignee_WhenTaskIsInProgress()
        {
            // AC12
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);

            await AssertClearRefusedWithNoSideEffectsAsync(mover.Client, task.Id, task.Version, TaskControllerMS.Assign.AssigneeRequired);
        }

        [Fact]
        public async Task Assign_Refuses_ClearingAssignee_WhenTaskIsInReview_AsLocked()
        {
            // AC13, superseded by f01-workflow.md Revision R2 D12/A3: Review
            // is now locked outright, not merely "assignee required" - any
            // assignee change on a Review/Done task is refused, for every
            // role including Admin (checked before the clearing rule below).
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var closer = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);
            var atReview = await MoveAsync(a.Client, task.Id, TaskWorkflowStatus.Review, task.Version);

            await AssertClearRefusedWithNoSideEffectsAsync(mover.Client, atReview.Id, atReview.Version, TaskControllerMS.Assign.Locked);
        }

        [Fact]
        public async Task Assign_Refuses_ClearingAssignee_WhenTaskIsDone_AsLocked()
        {
            // D5 clarified 2026-11-05, superseded by Revision R2 D12/A3 (see
            // the Review case above): Done is locked outright too, so
            // reopening Done -> Review never produces an unassigned Review
            // task, and nobody - including Admin - can change it while Done.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var closer = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);
            var atReview = await MoveAsync(a.Client, task.Id, TaskWorkflowStatus.Review, task.Version);
            var atDone = await MoveAsync(closer.Client, atReview.Id, TaskWorkflowStatus.Done, atReview.Version);

            await AssertClearRefusedWithNoSideEffectsAsync(mover.Client, atDone.Id, atDone.Version, TaskControllerMS.Assign.Locked);
        }

        [Fact]
        public async Task Assign_Refuses_WhenAdminChangesAssignee_WhileTaskIsInReview()
        {
            // AC33: locked for every role, including Admin - not merely
            // clearing, any assignee change at all.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var closer = await CreateAuthenticatedClientAsync("Manager");
            var admin = await CreateAuthenticatedClientAsync("Admin");
            var a = await CreateAuthenticatedClientAsync("Member");
            var c = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);
            var atReview = await MoveAsync(a.Client, task.Id, TaskWorkflowStatus.Review, task.Version);

            var response = await admin.Client.PutAsJsonAsync($"api/v1/tasks/{atReview.Id}/assignee", new { assigneeId = c.UserId, version = atReview.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.Locked, body!.ErrorCode);

            await using var context = _dbFixture.CreateContext();
            var reloaded = await context.Set<TaskItem>().SingleAsync(t => t.Id == atReview.Id);
            Assert.Equal(a.UserId, reloaded.AssigneeId);
        }

        [Fact]
        public async Task Assign_Refuses_WhenMemberAssignsAnotherUser_WithSelfOnly()
        {
            // D12/AC34: AssignTasks (every role) is self-assign/release only.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var member = await CreateAuthenticatedClientAsync("Member");
            var other = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = other.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.SelfOnly, body!.ErrorCode);
        }

        [Fact]
        public async Task Assign_Refuses_WhenMemberClearsAnotherUsersAssignment_WithSelfOnly()
        {
            // D12/AC34: clearing someone else's assignment also needs AssignAnyone.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var member = await CreateAuthenticatedClientAsync("Member");
            var a = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client, a.UserId);

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = (long?)null, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.SelfOnly, body!.ErrorCode);
        }

        [Fact]
        public async Task Assign_Allows_WhenMemberTakesOverATask_AssigningToSelf()
        {
            // D12: self-assign (take-over) is always allowed for AssignTasks.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = member.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(member.UserId, body.Result!.Assignee!.Id);
        }

        [Fact]
        public async Task Assign_Allows_WhenMemberReleasesTheirOwnAssignment()
        {
            // D12: releasing one's own assignment is always allowed.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var member = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client, member.UserId);

            var response = await member.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = (long?)null, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Null(body.Result!.Assignee);
        }

        [Fact]
        public async Task Assign_Refuses_WhenBTakesOverATask_AndBIsAtCapacity()
        {
            // AC23/AC37: B at 3 InProgress takes over A's InProgress task.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 3; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var task = await CreateInProgressTaskAsync(mover.Client, a);

            var response = await b.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = b.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.CapacityReached, body!.ErrorCode);
            Assert.Equal(1, await CountActiveInProgressAsync(a.UserId));
            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        [Fact]
        public async Task Assign_Allows_WhenBTakesOverATask_AndBHasRoom()
        {
            // AC24: B at 2 takes over A's InProgress task - B reaches 3, A
            // drops by one, one AssigneeChanged history entry with actor B.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 2; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var task = await CreateInProgressTaskAsync(mover.Client, a);

            var response = await b.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = b.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Equal(0, await CountActiveInProgressAsync(a.UserId));
            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));

            await using var context = _dbFixture.CreateContext();
            var entry = await context.Set<TaskHistory>().SingleAsync(h => h.TaskId == task.Id && h.Event == TaskHistoryEvent.AssigneeChanged);
            Assert.Equal(b.UserId, entry.ActorUserId);
        }

        [Fact]
        public async Task Assign_Allows_WhenBTakesOverATodoTask_AndBIsAtCapacity()
        {
            // AC25: capacity is never checked on a Backlog/Todo takeover.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 3; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var task = await CreateTaskAsync(mover.Client, a.UserId);
            var atTodo = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);

            var response = await b.Client.PutAsJsonAsync($"api/v1/tasks/{atTodo.Id}/assignee", new { assigneeId = b.UserId, version = atTodo.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
        }

        [Fact]
        public async Task Assign_Allows_ClearingAssignee_WhenTaskIsInBacklog()
        {
            // AC14
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client, a.UserId);

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = (long?)null, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Null(body.Result!.Assignee);
        }

        [Fact]
        public async Task Assign_Allows_ClearingAssignee_WhenTaskIsInTodo()
        {
            // AC14
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client, a.UserId);
            var atTodo = await MoveAsync(mover.Client, task.Id, TaskWorkflowStatus.Todo, task.Version);

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{atTodo.Id}/assignee", new { assigneeId = (long?)null, version = atTodo.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.True(response.IsSuccessStatusCode && body!.Success, body?.ErrorCode);
            Assert.Null(body.Result!.Assignee);
        }

        // ---- AC16 (assign) + AC17 + AC22 ----

        [Fact]
        public async Task Assign_Refuses_WhenCallerLacksAssignTasksPermission_EvenWhenTargetIsAtCapacity_CountUnchanged()
        {
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateInProgressTaskAsync(mover.Client, a);
            for (var i = 0; i < 3; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var noPermissionCaller = await CreateAuthenticatedClientAsync(role: null);
            var response = await noPermissionCaller.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = b.UserId, version = task.Version });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(1, await CountActiveInProgressAsync(a.UserId));
            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        [Fact]
        public async Task Assign_Refuses_WhenTargetIdDoesNotExist_WithCodeDistinctFromCapacity()
        {
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var task = await CreateTaskAsync(mover.Client);

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = 999_999_999L, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.AssigneeNotFound, body!.ErrorCode);
        }

        [Fact]
        public async Task Assign_Refuses_WhenTargetIdIsSoftDeleted_WithCodeDistinctFromCapacity()
        {
            // AC17: a soft-deleted target resolves to null through the same
            // query-filtered FindById mechanism UserRepositorySoftDeleteTests
            // pins directly.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var deleted = await CreateAuthenticatedClientAsync("Member");
            var task = await CreateTaskAsync(mover.Client);

            // Soft-deleted the same way the application does it - a Delete()
            // through UnitOfWork, which SaveChangesInternal converts into
            // IsDeleted = true (CLAUDE.md "Soft delete") - never the field set
            // by hand.
            await using (var context = _dbFixture.CreateContext())
            {
                var unitOfWork = new UnitOfWork(context, new HttpContextAccessor());
                var user = await unitOfWork.GetRepository<User>().FindById(deleted.UserId);
                await unitOfWork.GetRepository<User>().Delete(user!);
                await unitOfWork.SaveChanges();
            }

            var response = await mover.Client.PutAsJsonAsync($"api/v1/tasks/{task.Id}/assignee", new { assigneeId = deleted.UserId, version = task.Version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TaskControllerMS.Assign.AssigneeNotFound, body!.ErrorCode);
        }

        // ---- AC18 variants for Assign ----

        [Fact]
        public async Task Assign_TwoConcurrentAssigns_OfTwoInProgressTasks_ToSameTargetAtTwo_ExactlyOneSucceeds()
        {
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 2; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var taskX = await CreateInProgressTaskAsync(mover.Client, a);
            var taskY = await CreateInProgressTaskAsync(mover.Client, a);

            var payload1 = new { assigneeId = b.UserId, version = taskX.Version };
            var payload2 = new { assigneeId = b.UserId, version = taskY.Version };

            var request1 = mover.Client.PutAsJsonAsync($"api/v1/tasks/{taskX.Id}/assignee", payload1);
            var request2 = mover.Client.PutAsJsonAsync($"api/v1/tasks/{taskY.Id}/assignee", payload2);
            await Task.WhenAll(request1, request2);

            var responses = new[] { await request1, await request2 };
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

            var refused = responses.Single(r => r.StatusCode == HttpStatusCode.BadRequest);
            var refusedBody = await refused.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(refusedBody!.ErrorCode is TaskControllerMS.Assign.CapacityReached or TaskControllerMS.Assign.Conflict);

            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        [Fact]
        public async Task Assign_OneAssignAndOneMove_RacingForSameAssignee_ExactlyOneWins_CountNeverExceedsLimit()
        {
            // Mixed-operation AC18 variant called out explicitly for 3.2:
            // one assign and one move competing for the same target's last slot.
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 2; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var assignTarget = await CreateInProgressTaskAsync(mover.Client, a);
            var moveTarget = await CreateTaskAsync(mover.Client, b.UserId);
            var moveAtTodo = await MoveAsync(mover.Client, moveTarget.Id, TaskWorkflowStatus.Todo, moveTarget.Version);

            var assignRequest = mover.Client.PutAsJsonAsync(
                $"api/v1/tasks/{assignTarget.Id}/assignee", new { assigneeId = b.UserId, version = assignTarget.Version });

            // D11: holder-only - the move itself must be performed by b, the
            // moveTarget's own assignee.
            var moveRequest = b.Client.PutAsJsonAsync(
                $"api/v1/tasks/{moveTarget.Id}/status", new { status = "InProgress", version = moveAtTodo.Version });
            await Task.WhenAll(assignRequest, moveRequest);

            var responses = new[] { await assignRequest, await moveRequest };
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        [Fact]
        public async Task Assign_TwoRacingSaveChanges_OnSeparateConnections_SameTarget_ExactlyOneSucceeds()
        {
            // Deterministic two-MyDbContext interleave, the Assign analogue of
            // Move's AC18(a) test (ADR 0002 / f02-capacity.md T1).
            var mover = await CreateAuthenticatedClientAsync("Manager");
            var a = await CreateAuthenticatedClientAsync("Member");
            var b = await CreateAuthenticatedClientAsync("Member");
            for (var i = 0; i < 2; i++)
            {
                await CreateInProgressTaskAsync(mover.Client, b);
            }

            var taskX = await CreateInProgressTaskAsync(mover.Client, a);
            var taskY = await CreateInProgressTaskAsync(mover.Client, a);

            var configuration = _factory.Services.GetRequiredService<IConfiguration>();
            await using var contextA = _dbFixture.CreateContext();
            await using var contextB = _dbFixture.CreateContext();
            var serviceA = new TaskService(configuration, new UnitOfWork(contextA, new HttpContextAccessor()), NullLogger<TaskService>.Instance);
            var serviceB = new TaskService(configuration, new UnitOfWork(contextB, new HttpContextAccessor()), NullLogger<TaskService>.Instance);

            // D12: cross-user reassignment needs AssignAnyone (Manager/Admin).
            var permissions = new[] { Permissions.Tasks.AssignTasks, Permissions.Tasks.AssignAnyone };
            var assignX = new TaskAssignReqDto { AssigneeId = b.UserId, Version = taskX.Version };
            var assignY = new TaskAssignReqDto { AssigneeId = b.UserId, Version = taskY.Version };

            var resultA = serviceA.Assign(taskX.Id, assignX, mover.UserId, permissions);
            var resultB = serviceB.Assign(taskY.Id, assignY, mover.UserId, permissions);
            await Task.WhenAll(resultA, resultB);

            var outcomes = new[] { await resultA, await resultB };
            Assert.Single(outcomes, o => o.ErrorCode == string.Empty);
            Assert.Single(outcomes, o => o.ErrorCode == TaskControllerMS.Assign.CapacityReached || o.ErrorCode == TaskControllerMS.Assign.Conflict);

            Assert.Equal(3, await CountActiveInProgressAsync(b.UserId));
        }

        // ---- Helpers ----

        private async Task<TaskDetailRes> CreateTaskAsync(HttpClient client, long? assigneeId = null, string priority = "Normal")
        {
            var response = await client.PostAsJsonAsync("api/v1/tasks", new
            {
                title = $"Assign test {Guid.NewGuid():N}",
                priority,
                assigneeId,
            });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(body!.Success, body.Error);
            return body.Result!;
        }

        // D11: holder-only - the Todo -> InProgress move must be performed
        // by the assignee itself, not by whoever created/moved it to Todo.
        private async Task<TaskDetailRes> CreateInProgressTaskAsync(HttpClient mover, (HttpClient Client, long UserId) assignee)
        {
            var task = await CreateTaskAsync(mover, assignee.UserId);
            var atTodo = await MoveAsync(mover, task.Id, TaskWorkflowStatus.Todo, task.Version);
            return await MoveAsync(assignee.Client, task.Id, TaskWorkflowStatus.InProgress, atTodo.Version);
        }

        private async Task<TaskDetailRes> MoveAsync(HttpClient client, long id, TaskWorkflowStatus status, Guid version)
        {
            var response = await client.PutAsJsonAsync($"api/v1/tasks/{id}/status", new { status = status.ToString(), version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);
            Assert.True(response.IsSuccessStatusCode && body!.Success, $"move to {status} failed: {body?.ErrorCode}");
            return body!.Result!;
        }

        private async Task<int> CountActiveInProgressAsync(long assigneeId)
        {
            await using var context = _dbFixture.CreateContext();
            return await context.Set<TaskItem>().CountAsync(t => t.AssigneeId == assigneeId && t.Status == TaskWorkflowStatus.InProgress);
        }

        private async Task AssertClearRefusedWithNoSideEffectsAsync(HttpClient client, long taskId, Guid version, string expectedErrorCode)
        {
            TaskWorkflowStatus statusBefore;
            long? assigneeIdBefore;
            Guid versionBefore;
            int historyCountBefore;

            await using (var before = _dbFixture.CreateContext())
            {
                var task = await before.Set<TaskItem>().SingleAsync(t => t.Id == taskId);
                statusBefore = task.Status;
                assigneeIdBefore = task.AssigneeId;
                versionBefore = task.Version;
                historyCountBefore = await before.Set<TaskHistory>().CountAsync(h => h.TaskId == taskId);
            }

            var response = await client.PutAsJsonAsync($"api/v1/tasks/{taskId}/assignee", new { assigneeId = (long?)null, version });
            var body = await response.Content.ReadFromJsonAsync<ResultRes<TaskDetailRes>>(JsonOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(body!.Success);
            Assert.Equal(expectedErrorCode, body.ErrorCode);

            await using (var after = _dbFixture.CreateContext())
            {
                var task = await after.Set<TaskItem>().SingleAsync(t => t.Id == taskId);
                Assert.Equal(statusBefore, task.Status);
                Assert.Equal(assigneeIdBefore, task.AssigneeId);
                Assert.Equal(versionBefore, task.Version);
                Assert.Equal(historyCountBefore, await after.Set<TaskHistory>().CountAsync(h => h.TaskId == taskId));
            }
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice32-{Guid.NewGuid():N}";
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
