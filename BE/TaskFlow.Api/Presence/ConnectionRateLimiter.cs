using System.Collections.Concurrent;

namespace TaskFlow.Api.Presence
{
    // f07-presence.md B10: Join/Leave beyond 5/s per connection are ignored,
    // not errors. A simple sliding window per connection id; swept lazily
    // (an abandoned connection's entry is removed by RemoveConnection
    // rather than a background timer, since connections are already
    // bounded in number for this app's team size).
    public interface IConnectionRateLimiter
    {
        bool TryConsume(string connectionId);

        void Remove(string connectionId);
    }

    public class ConnectionRateLimiter : IConnectionRateLimiter
    {
        private const int MaxPerSecond = 5;
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _timestamps = new();
        private readonly object _lock = new();

        public bool TryConsume(string connectionId)
        {
            lock (_lock)
            {
                var queue = _timestamps.GetOrAdd(connectionId, _ => new Queue<DateTime>());
                var now = DateTime.UtcNow;

                while (queue.Count > 0 && (now - queue.Peek()).TotalSeconds >= 1)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= MaxPerSecond)
                {
                    return false;
                }

                queue.Enqueue(now);
                return true;
            }
        }

        public void Remove(string connectionId)
        {
            _timestamps.TryRemove(connectionId, out _);
        }
    }
}
