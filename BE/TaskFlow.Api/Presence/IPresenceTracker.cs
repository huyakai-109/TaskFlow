using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Api.Presence
{
    public record PresenceViewer(long UserId, string UserName, string? DisplayName, string? AvatarUrl);

    // f07-presence.md "Server design": in-memory, scope -> userId -> set of
    // connection ids, locked per scope. A user leaves a scope only when
    // their last connection there leaves (multi-tab dedup, AC2). Lives in
    // TaskFlow.Api - no BusinessLogic dependency in v1.
    public interface IPresenceTracker
    {
        // Returns true the first time this (scope, userId) pair becomes
        // present (another connection by the same user already being there
        // means no broadcast-worthy change - AC2).
        bool Join(string scope, long userId, string connectionId, PresenceViewer viewer);

        // Returns true when the user has no more connections left in scope
        // (a broadcast-worthy change).
        bool Leave(string scope, long userId, string connectionId);

        // Removes a disconnected connection from every scope it had joined,
        // returning the distinct scopes that lost their last connection for
        // that user (OnDisconnectedAsync drives the PresenceChanged broadcast
        // from this).
        List<string> RemoveConnection(string connectionId);

        List<PresenceViewer> GetViewers(string scope);

        int ScopeCountForConnection(string connectionId);

        // f08-accounts-profile.md "Quick revocation on deactivation": the
        // hub registers its own HubCallerContext here on connect (the only
        // place SignalR exposes an abortable handle), so a deactivation
        // handler elsewhere can abort every live connection for a user
        // directly, without needing IHubContext (which has no such method) -
        // near-immediate, not bounded by token expiry (f07-presence.md AC9).
        void RegisterContext(string connectionId, HubCallerContext context);

        // Harmless no-op if the user has no live connections.
        void AbortUserConnections(long userId);
    }

    public class PresenceTracker : IPresenceTracker
    {
        private readonly object _lock = new();

        // scope -> userId -> set of connection ids
        private readonly Dictionary<string, Dictionary<long, HashSet<string>>> _scopes = new();

        // connectionId -> (scope -> userId) it joined, so RemoveConnection
        // doesn't need to scan every scope.
        private readonly Dictionary<string, Dictionary<string, long>> _connections = new();

        // connectionId -> viewer, so GetViewers can render a name/avatar
        // without a second lookup.
        private readonly Dictionary<string, PresenceViewer> _viewerByConnection = new();

        // connectionId -> its HubCallerContext, so AbortUserConnections can
        // call .Abort() on a connection it did not itself receive the call
        // on (f08-accounts-profile.md "Quick revocation on deactivation").
        private readonly Dictionary<string, HubCallerContext> _contextByConnection = new();

        public bool Join(string scope, long userId, string connectionId, PresenceViewer viewer)
        {
            lock (_lock)
            {
                var usersInScope = _scopes.TryGetValue(scope, out var existing) ? existing : _scopes[scope] = new();
                var isNewUserInScope = !usersInScope.ContainsKey(userId);

                var connections = usersInScope.TryGetValue(userId, out var existingConnections)
                    ? existingConnections
                    : usersInScope[userId] = new();
                connections.Add(connectionId);

                var connectionScopes = _connections.TryGetValue(connectionId, out var existingScopes) ? existingScopes : _connections[connectionId] = new();
                connectionScopes[scope] = userId;

                _viewerByConnection[connectionId] = viewer;

                return isNewUserInScope;
            }
        }

        public bool Leave(string scope, long userId, string connectionId)
        {
            lock (_lock)
            {
                var changed = RemoveFromScope(scope, userId, connectionId);

                if (_connections.TryGetValue(connectionId, out var connectionScopes))
                {
                    connectionScopes.Remove(scope);
                    if (connectionScopes.Count == 0)
                    {
                        _connections.Remove(connectionId);
                        _viewerByConnection.Remove(connectionId);
                    }
                }

                return changed;
            }
        }

        public List<string> RemoveConnection(string connectionId)
        {
            lock (_lock)
            {
                var changedScopes = new List<string>();

                if (_connections.TryGetValue(connectionId, out var connectionScopes))
                {
                    foreach (var (scope, userId) in connectionScopes)
                    {
                        if (RemoveFromScope(scope, userId, connectionId))
                        {
                            changedScopes.Add(scope);
                        }
                    }

                    _connections.Remove(connectionId);
                }

                _viewerByConnection.Remove(connectionId);
                _contextByConnection.Remove(connectionId);

                return changedScopes;
            }
        }

        public List<PresenceViewer> GetViewers(string scope)
        {
            lock (_lock)
            {
                if (!_scopes.TryGetValue(scope, out var usersInScope))
                {
                    return [];
                }

                var viewers = new List<PresenceViewer>();
                foreach (var (_, connections) in usersInScope)
                {
                    var firstConnection = connections.FirstOrDefault();
                    if (firstConnection != null && _viewerByConnection.TryGetValue(firstConnection, out var viewer))
                    {
                        viewers.Add(viewer);
                    }
                }

                return viewers;
            }
        }

        public int ScopeCountForConnection(string connectionId)
        {
            lock (_lock)
            {
                return _connections.TryGetValue(connectionId, out var scopes) ? scopes.Count : 0;
            }
        }

        public void RegisterContext(string connectionId, HubCallerContext context)
        {
            lock (_lock)
            {
                _contextByConnection[connectionId] = context;
            }
        }

        public void AbortUserConnections(long userId)
        {
            List<HubCallerContext> contexts;

            lock (_lock)
            {
                var connectionIds = new HashSet<string>();
                foreach (var usersInScope in _scopes.Values)
                {
                    if (usersInScope.TryGetValue(userId, out var connections))
                    {
                        foreach (var connectionId in connections)
                        {
                            connectionIds.Add(connectionId);
                        }
                    }
                }

                contexts = [.. connectionIds
                    .Select(id => _contextByConnection.GetValueOrDefault(id))
                    .Where(ctx => ctx != null)!];
            }

            // Abort() outside the lock: it triggers OnDisconnectedAsync,
            // which re-enters this tracker (RemoveConnection) and would
            // deadlock on a non-reentrant lock otherwise.
            foreach (var context in contexts)
            {
                context.Abort();
            }
        }

        // Caller already holds _lock.
        private bool RemoveFromScope(string scope, long userId, string connectionId)
        {
            if (!_scopes.TryGetValue(scope, out var usersInScope) || !usersInScope.TryGetValue(userId, out var connections))
            {
                return false;
            }

            connections.Remove(connectionId);
            if (connections.Count > 0)
            {
                return false;
            }

            usersInScope.Remove(userId);
            if (usersInScope.Count == 0)
            {
                _scopes.Remove(scope);
            }

            return true;
        }
    }
}
