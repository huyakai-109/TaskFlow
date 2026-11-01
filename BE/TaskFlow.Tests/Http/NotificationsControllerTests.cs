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
using TaskFlow.Api.Models.Responses.Notifications;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // Slice 1.3: GET /notifications and PUT /notifications/{id}/read, the read
    // side of the store 1.2's Review->InProgress move already writes to
    // (f01-workflow.md T5 endpoints 13-14).
    [Collection(DatabaseCollection.Name)]
    public class NotificationsControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
    {
        private const string TestPassword = "Slice1Test@12345";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly TestWebApplicationFactory _factory;
        private readonly DatabaseFixture _dbFixture;

        public NotificationsControllerTests(TestWebApplicationFactory factory, DatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;
        }

        public Task InitializeAsync()
        {
            return _dbFixture.ResetTablesAsync("TaskNotifications", "TaskHistories", "Tasks");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task GetList_ReturnsOnlyTheCallersOwnNotifications()
        {
            var caller = await CreateAuthenticatedClientAsync("Member");
            var otherUser = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync();
            await SeedNotificationAsync(task.Id, caller.UserId);
            await SeedNotificationAsync(task.Id, otherUser.UserId);

            var response = await caller.Client.GetAsync("api/v1/notifications");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<NotificationRes>>>(JsonOptions);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body!.Success);
            var item = Assert.Single(body.Result!);
            Assert.Equal(task.Id, item.TaskId);
        }

        [Fact]
        public async Task GetList_FiltersByIsRead()
        {
            var caller = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync();
            var unread = await SeedNotificationAsync(task.Id, caller.UserId);
            var read = await SeedNotificationAsync(task.Id, caller.UserId, isRead: true);

            var response = await caller.Client.GetAsync("api/v1/notifications?isRead=false");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<NotificationRes>>>(JsonOptions);

            var item = Assert.Single(body!.Result!);
            Assert.Equal(unread, item.Id);
            Assert.NotEqual(read, item.Id);
        }

        [Fact]
        public async Task GetList_OrdersNewestFirst()
        {
            var caller = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync();
            var older = await SeedNotificationAsync(task.Id, caller.UserId, createdAt: DateTimeOffset.UtcNow.AddMinutes(-10));
            var newer = await SeedNotificationAsync(task.Id, caller.UserId, createdAt: DateTimeOffset.UtcNow);

            var response = await caller.Client.GetAsync("api/v1/notifications");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<NotificationRes>>>(JsonOptions);

            Assert.Equal(new[] { newer, older }, body!.Result!.Select(n => n.Id));
        }

        [Fact]
        public async Task GetList_ExcludesNotification_WhenItsTaskIsSoftDeleted()
        {
            var caller = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync();
            await SeedNotificationAsync(task.Id, caller.UserId);

            await using (var context = _dbFixture.CreateContext())
            {
                var row = await context.Set<TaskItem>().IgnoreQueryFilters().SingleAsync(t => t.Id == task.Id);
                row.IsDeleted = true;
                await context.SaveChangesAsync();
            }

            var response = await caller.Client.GetAsync("api/v1/notifications");
            var body = await response.Content.ReadFromJsonAsync<PaginationResultRes<List<NotificationRes>>>(JsonOptions);

            Assert.True(body!.Success);
            Assert.Empty(body.Result!);
        }

        [Fact]
        public async Task MarkRead_IsIdempotent_WhenCalledTwice()
        {
            var caller = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync();
            var notificationId = await SeedNotificationAsync(task.Id, caller.UserId);

            var firstResponse = await caller.Client.PutAsync($"api/v1/notifications/{notificationId}/read", null);
            var secondResponse = await caller.Client.PutAsync($"api/v1/notifications/{notificationId}/read", null);

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

            await using var context = _dbFixture.CreateContext();
            var notification = await context.Set<TaskNotification>().SingleAsync(n => n.Id == notificationId);
            Assert.True(notification.IsRead);
            Assert.NotNull(notification.ReadAt);
        }

        [Fact]
        public async Task MarkRead_Returns404_ForAnotherUsersNotification_NotForbidden()
        {
            var owner = await CreateAuthenticatedClientAsync("Member");
            var otherCaller = await CreateAuthenticatedClientAsync("Member");
            var task = await SeedTaskAsync();
            var notificationId = await SeedNotificationAsync(task.Id, owner.UserId);

            var response = await otherCaller.Client.PutAsync($"api/v1/notifications/{notificationId}/read", null);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(NotificationControllerMS.MarkRead.NotFound, body!.ErrorCode);

            await using var context = _dbFixture.CreateContext();
            var notification = await context.Set<TaskNotification>().SingleAsync(n => n.Id == notificationId);
            Assert.False(notification.IsRead);
        }

        // ---- Helpers ----

        private async Task<TaskItem> SeedTaskAsync()
        {
            await using var context = _dbFixture.CreateContext();
            var task = new TaskItem
            {
                Title = $"Notification fixture {Guid.NewGuid():N}",
                Status = TaskWorkflowStatus.InProgress,
                Priority = TaskPriority.Normal,
                Version = Guid.NewGuid(),
            };
            context.Add(task);
            await context.SaveChangesAsync();
            return task;
        }

        private async Task<long> SeedNotificationAsync(long taskId, long recipientUserId, bool isRead = false, DateTimeOffset? createdAt = null)
        {
            await using var context = _dbFixture.CreateContext();
            var notification = new TaskNotification
            {
                RecipientUserId = recipientUserId,
                TaskId = taskId,
                Event = TaskNotificationEvent.ReturnedForRework,
                IsRead = isRead,
                ReadAt = isRead ? DateTimeOffset.UtcNow : null,
            };
            context.Add(notification);
            await context.SaveChangesAsync();

            if (createdAt.HasValue)
            {
                notification.CreatedAt = createdAt.Value;
                await context.SaveChangesAsync();
            }

            return notification.Id;
        }

        private async Task<(HttpClient Client, long UserId)> CreateAuthenticatedClientAsync(string? role)
        {
            var userName = $"slice13-notif-{Guid.NewGuid():N}";
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
