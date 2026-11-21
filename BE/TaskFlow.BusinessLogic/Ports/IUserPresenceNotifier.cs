namespace TaskFlow.BusinessLogic.Services
{
    // review-change (R2 batch), f08-accounts-profile.md "Quick revocation on
    // deactivation" / f07-presence.md AC9: a port, not a direct reference to
    // TaskFlow.Api.Presence - BusinessLogic must not depend on the
    // Presentation layer (backend.md Section 1's one-way layering). The
    // real implementation (TaskFlow.Api.Presence.PresenceUserNotifier) wraps
    // IPresenceTracker.AbortUserConnections and is registered in Program.cs.
    //
    // This also fixes a correctness gap found in review: the abort was
    // originally only called from UsersController.Deactivate, so any other
    // caller of UserService.Deactivate (a future bulk action, a background
    // job, a test) would silently skip the hub-abort half of "quick
    // revocation" - the invariant now lives with the data mutation it must
    // always accompany.
    public interface IUserPresenceNotifier
    {
        void NotifyDeactivated(long userId);
    }
}
