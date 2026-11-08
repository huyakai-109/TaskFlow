using TaskFlow.BusinessLogic.Dtos.Tasks;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Enums;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TaskFlow.BusinessLogic.Services
{
    public interface ITaskOverdueScanService
    {
        Task<TaskOverdueScanResultDto> Run(DateOnly todayInZone, CancellationToken cancellationToken);
    }

    // f05-overdue.md Technical contract T1/T2: the nightly scan, called by the
    // future Quartz job (slice 5.2) and directly by tests today - no HTTP
    // surface. D3: per-task isolated, one failing row never stops the rest.
    public class TaskOverdueScanService(
        IUnitOfWork unitOfWork,
        ILogger<TaskOverdueScanService> logger) : ITaskOverdueScanService
    {
        private const int CandidateBatchSize = 100;

        public async Task<TaskOverdueScanResultDto> Run(DateOnly todayInZone, CancellationToken cancellationToken)
        {
            var result = new TaskOverdueScanResultDto();
            var lastId = 0L;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidateIds = await LoadCandidateBatch(lastId, todayInZone);
                if (candidateIds.Count == 0)
                {
                    break;
                }

                foreach (var id in candidateIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    result.Examined++;
                    await ProcessCandidate(id, todayInZone, result);
                }

                lastId = candidateIds[^1];
            }

            logger.LogInformation(
                "Overdue scan for {Today} examined {Examined}, flagged {Flagged}, skipped {Skipped}, failed {Failed}",
                todayInZone, result.Examined, result.Flagged, result.Skipped, result.Failed);

            return result;
        }

        // Keyset, not offset (T1): a flagged row leaves the predicate and a
        // failed row stays in it, so offset paging would skip or repeat rows.
        // No tracking, ids only - the SQL is a pre-filter, the rule itself is
        // re-checked per row against the tracked entity below.
        private async Task<List<long>> LoadCandidateBatch(long lastId, DateOnly todayInZone)
        {
            var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(
                i => !i.IsOverdue && i.Status != TaskWorkflowStatus.Done && i.DueDate != null && i.DueDate < todayInZone && i.Id > lastId);

            return await query
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .Take(CandidateBatchSize)
                .ToListAsync();
        }

        private async Task ProcessCandidate(long id, DateOnly todayInZone, TaskOverdueScanResultDto result)
        {
            try
            {
                var query = await unitOfWork.GetRepository<TaskItem>().QueryCondition(i => i.Id == id, disableTracking: false);
                var task = query.FirstOrDefault();

                // Already gone (hard to reach - soft delete only - or raced
                // away between the candidate read and here): nothing to flag.
                if (task == null)
                {
                    result.Skipped++;
                    return;
                }

                // Re-check against the one shared predicate: the candidate SQL
                // is only a pre-filter, and the row may have changed since it
                // ran (AC21).
                if (!TaskOverdueRule.IsOverdue(task.DueDate, task.Status, todayInZone) || task.IsOverdue)
                {
                    result.Skipped++;
                    return;
                }

                task.IsOverdue = true;
                task.Version = Guid.NewGuid();

                var history = new TaskHistory
                {
                    TaskId = task.Id,
                    Event = TaskHistoryEvent.OverdueMarked,
                    ActorUserId = null,
                };
                await unitOfWork.GetRepository<TaskHistory>().Add(history);

                await unitOfWork.SaveChanges();
                result.Flagged++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // A concurrent user write (move, due-date change, ...) already
                // changed this row (T2, AC21): the scan loses, no retry.
                logger.LogWarning(ex, "Overdue scan hit a concurrency conflict flagging task {TaskId}; skipped", id);
                result.Skipped++;
            }
            catch (Exception ex) when (TaskConcurrencyHelper.IsDeadlock(ex))
            {
                // Same MySQL 1213 lock-upgrade deadlock Move/Reopen/Assign hit
                // under concurrent writers (ROADMAP R13) - the scan's victim
                // transaction is already rolled back, so this is "someone else
                // won", not a defect.
                logger.LogWarning(ex, "Overdue scan hit a MySQL deadlock (1213) flagging task {TaskId}; skipped", id);
                result.Skipped++;
            }
            catch (Exception ex)
            {
                // D3: one bad row is logged and the scan continues.
                logger.LogError(ex, "Overdue scan failed to flag task {TaskId}", id);
                result.Failed++;
            }
            finally
            {
                // One failure (or success) must not poison the next
                // candidate's load - each row gets a clean tracked state.
                unitOfWork.DbContext.ChangeTracker.Clear();
            }
        }
    }
}
