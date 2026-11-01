using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Notifications;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Notifications;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/notifications")]
    public class NotificationsController(
        ILogger<NotificationsController> logger,
        IMapper mapper,
        INotificationService notificationService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(PaginationResultRes<List<NotificationRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] NotificationGetListReq request)
        {
            var response = new PaginationResultRes<List<NotificationRes>>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();

                var page = await notificationService.GetList(actorUserId, request.IsRead, request.Start, request.Length);

                response.Success = true;
                response.Result = Mapper.Map<List<NotificationRes>>(page.Items);
                response.Pagination = new PaginationRes(page.TotalItems, page.Items.Count, (page.PageNumber - 1) * page.PageSize, page.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = NotificationControllerMS.GetList.Exception;
                Logger.LogError(ex, "Get notification list failed");
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/read")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ExecutionRes), StatusCodes.Status200OK)]
        public async Task<IActionResult> MarkRead([FromRoute] long id)
        {
            var response = new ExecutionRes();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();

                var errorCode = await notificationService.MarkRead(id, actorUserId);

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
                response.ErrorCode = NotificationControllerMS.MarkRead.Exception;
                Logger.LogError(ex, "Mark notification {Id} as read failed", id);
                return InternalServerError(response);
            }
        }
    }
}
