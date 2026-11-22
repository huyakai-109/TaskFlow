using System.Text.RegularExpressions;
using TaskFlow.Api.Presence;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Api.Hubs
{
    // f07-presence.md: a `board` room and a per-`task:{id}` room, each
    // carrying only { userId, userName, displayName, avatarUrl } - no
    // join/leave history persisted anywhere. Task existence is not checked
    // on Join: every task is visible to every ViewTasks holder, so there is
    // no data leak in letting a bad id join a room nobody else can reach.
    [Authorize(Policy = Permissions.Tasks.ViewTasks)]
    public class PresenceHub(IPresenceTracker tracker, IConnectionRateLimiter rateLimiter, IUnitOfWork unitOfWork) : Hub
    {
        private const int MaxScopesPerConnection = 4;
        private static readonly Regex TaskScopePattern = new(@"^task:[1-9][0-9]*$", RegexOptions.Compiled);

        public override Task OnConnectedAsync()
        {
            // f08-accounts-profile.md "Quick revocation on deactivation":
            // registered up front, not only on Join, so a connection that
            // never joins any scope can still be aborted (defensive; in
            // practice the client joins immediately after connecting).
            tracker.RegisterContext(Context.ConnectionId, Context);
            return base.OnConnectedAsync();
        }

        public async Task<List<PresenceViewer>> Join(string scope)
        {
            if (!rateLimiter.TryConsume(Context.ConnectionId))
            {
                return tracker.GetViewers(scope);
            }

            if (!IsValidScope(scope))
            {
                throw new HubException("Presence.Join.InvalidScope");
            }

            if (tracker.ScopeCountForConnection(Context.ConnectionId) >= MaxScopesPerConnection)
            {
                throw new HubException("Presence.Join.TooManyScopes");
            }

            var userId = Context.User!.Claims.GetUserId();
            var viewer = await BuildViewer(userId);

            var changed = tracker.Join(scope, userId, Context.ConnectionId, viewer);
            await Groups.AddToGroupAsync(Context.ConnectionId, scope);

            var viewers = tracker.GetViewers(scope);
            if (changed)
            {
                await Clients.Group(scope).SendAsync("PresenceChanged", new { scope, viewers });
            }

            return viewers;
        }

        public async Task Leave(string scope)
        {
            if (!rateLimiter.TryConsume(Context.ConnectionId))
            {
                return;
            }

            var userId = Context.User!.Claims.GetUserId();
            var changed = tracker.Leave(scope, userId, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, scope);

            if (changed)
            {
                var viewers = tracker.GetViewers(scope);
                await Clients.Group(scope).SendAsync("PresenceChanged", new { scope, viewers });
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            rateLimiter.Remove(Context.ConnectionId);
            var changedScopes = tracker.RemoveConnection(Context.ConnectionId);

            foreach (var scope in changedScopes)
            {
                var viewers = tracker.GetViewers(scope);
                await Clients.Group(scope).SendAsync("PresenceChanged", new { scope, viewers });
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task<PresenceViewer> BuildViewer(long userId)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(userId);
            return new PresenceViewer(
                userId,
                user?.UserName ?? string.Empty,
                user?.DisplayName,
                AvatarKeyHelper.BuildUrl(userId, user?.AvatarObjectKey));
        }

        private static bool IsValidScope(string scope) => scope == "board" || TaskScopePattern.IsMatch(scope);
    }
}
