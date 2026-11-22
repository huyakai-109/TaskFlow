using TaskFlow.Api.Presence;

namespace TaskFlow.Tests.Presence
{
    public class ConnectionRateLimiterTests
    {
        [Fact]
        public void TryConsume_AllowsUpToFivePerSecond_ThenRefuses()
        {
            var limiter = new ConnectionRateLimiter();

            for (var i = 0; i < 5; i++)
            {
                Assert.True(limiter.TryConsume("c1"));
            }

            Assert.False(limiter.TryConsume("c1"));
        }

        [Fact]
        public void TryConsume_TracksEachConnectionIndependently()
        {
            var limiter = new ConnectionRateLimiter();

            for (var i = 0; i < 5; i++)
            {
                limiter.TryConsume("c1");
            }

            Assert.True(limiter.TryConsume("c2"));
        }

        [Fact]
        public void Remove_ClearsState_SoTheConnectionIdCanBeReused()
        {
            var limiter = new ConnectionRateLimiter();
            for (var i = 0; i < 5; i++)
            {
                limiter.TryConsume("c1");
            }

            limiter.Remove("c1");

            Assert.True(limiter.TryConsume("c1"));
        }
    }
}
