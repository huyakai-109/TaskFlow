namespace TaskFlow.BusinessLogic.Ports
{
    // First-of-its-kind location in this codebase (coding-standards.md
    // "never invent a convention silently" - stating that explicitly here):
    // every other service interface (ITaskService, IUserService,
    // IStorageService, ...) is declared directly above its implementation,
    // in the same file, within TaskFlow.BusinessLogic.Services - because
    // both the interface and the implementation live in this layer. This
    // one doesn't fit that shape: its implementation
    // (TaskFlow.Api.Presence.PresenceUserNotifier) lives in the Presentation
    // layer, because the capability it wraps (IPresenceTracker, aborting a
    // live SignalR connection) only exists there. BusinessLogic cannot
    // reference TaskFlow.Api directly (backend.md Section 1's one-way
    // layering), so this is a port: an interface this layer declares and
    // depends on, implemented by an adapter one layer up. `Ports` is a new
    // namespace, separate from `Services`, specifically to mark "this
    // interface's implementation is not in this project" at a glance -
    // adopt the same home for any future port, rather than placing another
    // one in `Services` by default.
    //
    // f08-accounts-profile.md "Quick revocation on deactivation" /
    // f07-presence.md AC9: this also fixes a correctness gap found in
    // review - the abort was originally only called from
    // UsersController.Deactivate, so any other caller of
    // UserService.Deactivate (a future bulk action, a background job, a
    // test) would silently skip the hub-abort half of "quick revocation" -
    // the invariant now lives with the data mutation it must always
    // accompany.
    public interface IUserPresenceNotifier
    {
        void NotifyDeactivated(long userId);
    }
}
