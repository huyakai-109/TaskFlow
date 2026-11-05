using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Users;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/users")]
    public class UsersController(
        ILogger<UsersController> logger,
        IMapper mapper,
        IUserService userService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<List<UserRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList()
        {
            var response = new ResultRes<List<UserRes>>();

            try
            {
                var users = await userService.GetList();

                response.Success = true;
                response.Result = Mapper.Map<List<UserRes>>(users);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.GetList.Exception;
                Logger.LogError(ex, "Get user list failed");
                return InternalServerError(response);
            }
        }
    }
}
