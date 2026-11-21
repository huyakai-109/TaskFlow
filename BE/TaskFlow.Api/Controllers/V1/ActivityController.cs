using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Configurations;
using TaskFlow.Api.Models.Requests.Activity;
using TaskFlow.Api.Models.Responses.Activity;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.BusinessLogic.Dtos.Activity;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    // f04-activity-feed.md: a read-only, keyset-paged feed of F04 history
    // across the whole board (ADR 0007). Policy ViewTasks - every seeded
    // role holds it (R1), no new permission. The per-task history endpoint
    // (TaskHistoryController) is untouched (AC23).
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/activity")]
    public class ActivityController(
        ILogger<ActivityController> logger,
        IMapper mapper,
        ITaskHistoryService taskHistoryService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(CursorResultRes<List<ActivityRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] ActivityGetListReq request)
        {
            var response = new CursorResultRes<List<ActivityRes>>();

            try
            {
                // AC15: an unknown numeric event value binds successfully
                // (the query binder doesn't range-check enums) but must
                // still fail validation - [EnumDataType] only validates a
                // single property, not array elements.
                if (request.Events != null)
                {
                    foreach (var requestEvent in request.Events)
                    {
                        if (!Enum.IsDefined(typeof(TaskHistoryEvent), requestEvent))
                        {
                            response.ErrorCode = ApiBehaviorConfiguration.InvalidModelStateErrorCode;
                            return BadRequest(response);
                        }
                    }
                }

                var requestDto = new ActivityGetListReqDto
                {
                    Cursor = request.Cursor,
                    Length = request.Length,
                    Actor = request.Actor,
                    From = request.From,
                    To = request.To,
                    Events = request.Events,
                };

                var (errorCode, feed) = await taskHistoryService.GetFeed(requestDto);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<List<ActivityRes>>(feed!.Items);
                response.Page = new CursorPageRes(feed.NextCursor, feed.HasMore, feed.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ActivityControllerMS.GetList.Exception;
                Logger.LogError(ex, "Get activity feed failed");
                return InternalServerError(response);
            }
        }
    }
}
