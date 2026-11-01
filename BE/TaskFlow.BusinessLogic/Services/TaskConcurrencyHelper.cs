using MySqlConnector;

namespace TaskFlow.BusinessLogic.Services
{
    // Pure lift of TaskService.Move's original private IsDeadlock helper
    // (f01-workflow.md T2, ROADMAP R13): shared, unchanged, so Reopen (1.3)
    // and Assign (1.4) catch the same MySQL 1213 lock-upgrade deadlock the
    // same way Move does, without re-deriving the unwrap logic.
    public static class TaskConcurrencyHelper
    {
        public static bool IsDeadlock(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is MySqlException { ErrorCode: MySqlErrorCode.LockDeadlock })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
