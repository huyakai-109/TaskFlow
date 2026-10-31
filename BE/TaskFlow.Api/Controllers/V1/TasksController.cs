using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Tasks;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.BusinessLogic.Dtos.Tasks;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/tasks")]
    public class TasksController(
        ILogger<TasksController> logger,
        IMapper mapper,
        ITaskService taskService) : BaseController(logger, mapper)
    {
        [HttpPost]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.CreateTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(TaskCreateReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.Create(Mapper.Map<TaskCreateReqDto>(request), actorUserId, callerPermissions);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<TaskDetailRes>(task);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = TaskControllerMS.Create.Exception;
                Logger.LogError(ex, "Create task failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("{id:long}")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromRoute] long id)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.Get(id, actorUserId, callerPermissions);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<TaskDetailRes>(task);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = TaskControllerMS.Get.Exception;
                Logger.LogError(ex, "Get task {Id} failed", id);
                return InternalServerError(response);
            }
        }
    }
}
