using System.ComponentModel.DataAnnotations;
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

        [HttpPut]
        [Route("{id:long}/status")]
        [Authorize(Policy = Permissions.Tasks.MoveTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Move([FromRoute] long id, TaskMoveReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.Move(id, Mapper.Map<TaskMoveReqDto>(request), actorUserId, callerPermissions);

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
                response.ErrorCode = TaskControllerMS.Move.Exception;
                Logger.LogError(ex, "Move task {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/reopen")]
        [Authorize(Policy = Permissions.Tasks.ReopenTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reopen([FromRoute] long id, TaskReopenReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.Reopen(id, Mapper.Map<TaskReopenReqDto>(request), actorUserId, callerPermissions);

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
                response.ErrorCode = TaskControllerMS.Reopen.Exception;
                Logger.LogError(ex, "Reopen task {Id} failed", id);
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

        [HttpGet]
        [Route("board")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<BoardRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBoard()
        {
            var response = new ResultRes<BoardRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var board = await taskService.GetBoard(actorUserId, callerPermissions);

                response.Success = true;
                response.Result = Mapper.Map<BoardRes>(board);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = TaskControllerMS.GetBoard.Exception;
                Logger.LogError(ex, "Get board failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(PaginationResultRes<List<TaskSummaryRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] TaskGetListReq request)
        {
            var response = new PaginationResultRes<List<TaskSummaryRes>>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var page = await taskService.GetList(request.Status, request.Start, request.Length, actorUserId, callerPermissions);

                response.Success = true;
                response.Result = Mapper.Map<List<TaskSummaryRes>>(page.Items);
                response.Pagination = new PaginationRes(page.TotalItems, page.Items.Count, (page.PageNumber - 1) * page.PageSize, page.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = TaskControllerMS.GetList.Exception;
                Logger.LogError(ex, "Get task list failed");
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}")]
        [Authorize(Policy = Permissions.Tasks.UpdateTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update([FromRoute] long id, TaskUpdateReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.Update(id, Mapper.Map<TaskUpdateReqDto>(request), actorUserId, callerPermissions);

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
                response.ErrorCode = TaskControllerMS.Update.Exception;
                Logger.LogError(ex, "Update task {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/priority")]
        [Authorize(Policy = Permissions.Tasks.UpdateTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdatePriority([FromRoute] long id, TaskUpdatePriorityReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.UpdatePriority(id, Mapper.Map<TaskUpdatePriorityReqDto>(request), actorUserId, callerPermissions);

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
                response.ErrorCode = TaskControllerMS.UpdatePriority.Exception;
                Logger.LogError(ex, "Update priority of task {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/due-date")]
        [Authorize(Policy = Permissions.Tasks.UpdateTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateDueDate([FromRoute] long id, TaskUpdateDueDateReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.UpdateDueDate(id, Mapper.Map<TaskUpdateDueDateReqDto>(request), actorUserId, callerPermissions);

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
                response.ErrorCode = TaskControllerMS.UpdateDueDate.Exception;
                Logger.LogError(ex, "Update due date of task {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/assignee")]
        [Authorize(Policy = Permissions.Tasks.AssignTasks)]
        [ProducesResponseType(typeof(ResultRes<TaskDetailRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Assign([FromRoute] long id, TaskAssignReq request)
        {
            var response = new ResultRes<TaskDetailRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, task) = await taskService.Assign(id, Mapper.Map<TaskAssignReqDto>(request), actorUserId, callerPermissions);

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
                response.ErrorCode = TaskControllerMS.Assign.Exception;
                Logger.LogError(ex, "Assign task {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpDelete]
        [Route("{id:long}")]
        [Authorize(Policy = Permissions.Tasks.DeleteTasks)]
        [ProducesResponseType(typeof(ExecutionRes), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete([FromRoute] long id, [FromQuery] [Required] Guid version)
        {
            var response = new ExecutionRes();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();

                var errorCode = await taskService.Delete(id, version, actorUserId);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = TaskControllerMS.Delete.Exception;
                Logger.LogError(ex, "Delete task {Id} failed", id);
                return InternalServerError(response);
            }
        }
    }
}
