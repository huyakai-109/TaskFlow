using TaskFlow.Api.Presence;

namespace TaskFlow.Tests.Presence
{
    // f07-presence.md: pure in-memory tracker, no database/host needed - not
    // in the Database collection on purpose (nothing here touches MySQL or
    // boots TestWebApplicationFactory).
    public class PresenceTrackerTests
    {
        private static PresenceViewer Viewer(long userId) => new(userId, $"user{userId}", null, null);

        [Fact]
        public void Join_ReturnsTrue_OnTheFirstConnectionForThatUserInScope()
        {
            var tracker = new PresenceTracker();

            var changed = tracker.Join("board", userId: 1, connectionId: "c1", Viewer(1));

            Assert.True(changed);
            Assert.Single(tracker.GetViewers("board"));
        }

        [Fact]
        public void Join_SameUser_TwoTabs_AppearsOnce_AndSecondJoinIsNotABroadcastChange()
        {
            var tracker = new PresenceTracker();

            var firstChanged = tracker.Join("board", userId: 1, connectionId: "tab-a", Viewer(1));
            var secondChanged = tracker.Join("board", userId: 1, connectionId: "tab-b", Viewer(1));

            Assert.True(firstChanged);
            Assert.False(secondChanged);
            Assert.Single(tracker.GetViewers("board"));
        }

        [Fact]
        public void RemoveConnection_ClosingOneOfTwoTabs_KeepsTheUserPresent()
        {
            var tracker = new PresenceTracker();
            tracker.Join("board", userId: 1, connectionId: "tab-a", Viewer(1));
            tracker.Join("board", userId: 1, connectionId: "tab-b", Viewer(1));

            var changedScopes = tracker.RemoveConnection("tab-a");

            Assert.Empty(changedScopes);
            Assert.Single(tracker.GetViewers("board"));
        }

        [Fact]
        public void RemoveConnection_ClosingTheLastTab_RemovesTheUser_AndReportsTheScopeChanged()
        {
            var tracker = new PresenceTracker();
            tracker.Join("board", userId: 1, connectionId: "tab-a", Viewer(1));
            tracker.Join("board", userId: 1, connectionId: "tab-b", Viewer(1));

            tracker.RemoveConnection("tab-a");
            var changedScopes = tracker.RemoveConnection("tab-b");

            Assert.Equal(["board"], changedScopes);
            Assert.Empty(tracker.GetViewers("board"));
        }

        [Fact]
        public void RemoveConnection_RemovesFromEveryScopeItJoined()
        {
            var tracker = new PresenceTracker();
            tracker.Join("board", userId: 1, connectionId: "c1", Viewer(1));
            tracker.Join("task:7", userId: 1, connectionId: "c1", Viewer(1));

            var changedScopes = tracker.RemoveConnection("c1");

            Assert.Equal(2, changedScopes.Count);
            Assert.Empty(tracker.GetViewers("board"));
            Assert.Empty(tracker.GetViewers("task:7"));
        }

        [Fact]
        public void ScopeCountForConnection_TracksHowManyScopesAConnectionHasJoined()
        {
            var tracker = new PresenceTracker();
            tracker.Join("board", userId: 1, connectionId: "c1", Viewer(1));
            tracker.Join("task:7", userId: 1, connectionId: "c1", Viewer(1));

            Assert.Equal(2, tracker.ScopeCountForConnection("c1"));
        }

        [Fact]
        public void AbortUserConnections_IsHarmless_ForAUserWithNoConnections()
        {
            var tracker = new PresenceTracker();

            var exception = Record.Exception(() => tracker.AbortUserConnections(999));

            Assert.Null(exception);
        }

        [Fact]
        public void Leave_IsIdempotent_CallingTwiceDoesNotThrow()
        {
            var tracker = new PresenceTracker();
            tracker.Join("board", userId: 1, connectionId: "c1", Viewer(1));

            tracker.Leave("board", userId: 1, connectionId: "c1");
            var secondCallChanged = tracker.Leave("board", userId: 1, connectionId: "c1");

            Assert.False(secondCallChanged);
        }
    }
}
