using TaskFlow.BusinessLogic.Dtos.Workflow;
using TaskFlow.BusinessLogic.Schedulers;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IWorkflowService
    {
        Task<WorkflowResDto> GetWorkflow();
    }

    // f10-read-models-and-sort.md Section 4. IScheduleService is resolved
    // through IServiceProvider rather than taken as a constructor
    // dependency: it (and the Quartz machinery behind it) is registered
    // only outside the "Testing" environment (CoreDependencyConfiguration's
    // skipQuartz), by design (TaskFlow.Tests never touches the real
    // scheduler) - "null when not scheduled (Testing)" is the documented
    // behavior, not a missing registration to fix.
    public class WorkflowService(IConfiguration configuration, IServiceProvider serviceProvider) : IWorkflowService
    {
        private readonly string _timeZone = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;
        private readonly int _maxInProgressPerAssignee = configuration.GetSection(ConfigKeys.Workflow.MaxInProgressPerAssignee).Get<int>();

        public async Task<WorkflowResDto> GetWorkflow()
        {
            DateTimeOffset? lastFiredAt = null;
            DateTimeOffset? nextFireAt = null;

            var scheduleService = serviceProvider.GetService<IScheduleService>();
            if (scheduleService != null)
            {
                (lastFiredAt, nextFireAt) = await scheduleService.GetTriggerTimes(GlobalConstants.Jobs.OverdueScanJobId);
            }

            return new WorkflowResDto
            {
                MaxInProgressPerAssignee = _maxInProgressPerAssignee,
                TimeZone = _timeZone,
                Today = DateTimeHelper.GetDtOffset().ToDateInZone(_timeZone),
                LastFiredAt = lastFiredAt,
                NextFireAt = nextFireAt,
            };
        }
    }
}
