using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Workflow;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    // f10-read-models-and-sort.md Section 4: replaces the client literal
    // IN_PROGRESS_LIMIT and browser-date "today" approximations.
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/workflow")]
    public class WorkflowController(
        ILogger<WorkflowController> logger,
        IMapper mapper,
        IWorkflowService workflowService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<WorkflowRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var response = new ResultRes<WorkflowRes>();

            try
            {
                var workflow = await workflowService.GetWorkflow();

                response.Success = true;
                response.Result = Mapper.Map<WorkflowRes>(workflow);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = WorkflowControllerMS.Get.Exception;
                Logger.LogError(ex, "Get workflow failed");
                return InternalServerError(response);
            }
        }
    }
}
