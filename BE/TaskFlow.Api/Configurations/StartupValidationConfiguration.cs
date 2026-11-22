using TaskFlow.Common.Constants;

namespace TaskFlow.Api.Configurations
{
    // Every setting a service reads from configuration once and relies on
    // being sane, checked together at startup rather than failing later,
    // deep in a request, with a null-reference instead of a clear message.
    public static class StartupValidationConfiguration
    {
        public static void ValidateStartupSettings(this IConfiguration configuration)
        {
            // f02-capacity.md Technical contract T3 (D1): the limit is never
            // a literal at the call site - TaskService reads it from
            // configuration once, so it must exist and be sane at startup.
            var maxInProgressPerAssignee = configuration.GetSection(ConfigKeys.Workflow.MaxInProgressPerAssignee).Get<int?>();
            if (maxInProgressPerAssignee is null or < 1)
            {
                throw new InvalidOperationException($"'{ConfigKeys.Workflow.MaxInProgressPerAssignee}' must be configured with a value of at least 1.");
            }

            // f05-overdue.md Technical contract T4 (D4): "today" for the
            // overdue scan and for every synchronous recompute comes from
            // this zone, never the server clock.
            var workflowTimeZone = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>();
            if (string.IsNullOrEmpty(workflowTimeZone))
            {
                throw new InvalidOperationException($"'{ConfigKeys.Workflow.TimeZone}' must be configured.");
            }

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(workflowTimeZone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new InvalidOperationException($"'{ConfigKeys.Workflow.TimeZone}' value '{workflowTimeZone}' is not a valid time zone.", ex);
            }

            // f08-accounts-profile.md "Password policy": fail fast rather
            // than silently falling back to Identity's unconfigured default (6).
            var passwordRequiredLength = configuration.GetSection(ConfigKeys.Security.Password.RequiredLength).Get<int?>();
            if (passwordRequiredLength is null or < 1)
            {
                throw new InvalidOperationException($"'{ConfigKeys.Security.Password.RequiredLength}' must be configured with a value of at least 1.");
            }
        }
    }
}
