using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.TaskHistories;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.TaskHistories;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/tasks/{taskId:long}/history")]
    public class TaskHistoryController(
        ILogger<TaskHistoryController> logger,
        IMapper mapper,
        ITaskHistoryService taskHistoryService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(PaginationResultRes<List<TaskHistoryRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromRoute] long taskId, [FromQuery] TaskHistoryGetListReq request)
        {
            var response = new PaginationResultRes<List<TaskHistoryRes>>();

            try
            {
                var (errorCode, page) = await taskHistoryService.GetList(taskId, request.Start, request.Length);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<List<TaskHistoryRes>>(page!.Items);
                response.Pagination = new PaginationRes(page.TotalItems, page.Items.Count, (page.PageNumber - 1) * page.PageSize, page.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = TaskHistoryControllerMS.GetList.Exception;
                Logger.LogError(ex, "Get history for task {TaskId} failed", taskId);
                return InternalServerError(response);
            }
        }
    }
}
