using TaskFlow.BusinessLogic.Services;

namespace TaskFlow.Api.Presence
{
    // The Api-layer implementation of the BusinessLogic-defined port -
    // registered in Program.cs. See IUserPresenceNotifier for why this
    // indirection exists.
    public class PresenceUserNotifier(IPresenceTracker tracker) : IUserPresenceNotifier
    {
        public void NotifyDeactivated(long userId) => tracker.AbortUserConnections(userId);
    }
}
