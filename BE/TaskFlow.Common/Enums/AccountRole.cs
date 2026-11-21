namespace TaskFlow.Common.Enums
{
    // f08-accounts-profile.md "Admin API": the three seeded roles, named for
    // request binding/validation only - RolePolicies.*.Id/.Name remain the
    // source of truth for the stored Role row.
    public enum AccountRole
    {
        Admin = 1,
        Manager = 2,
        Member = 3,
    }
}
