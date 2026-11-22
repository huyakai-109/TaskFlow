using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Users;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Users;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.BusinessLogic.Storage;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/users")]
    public class UsersController(
        ILogger<UsersController> logger,
        IMapper mapper,
        IUserService userService,
        IStorageService storageService) : BaseController(logger, mapper)
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

        [HttpGet]
        [Route("workload")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<List<WorkloadRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWorkload()
        {
            var response = new ResultRes<List<WorkloadRes>>();

            try
            {
                var workload = await userService.GetWorkload();

                response.Success = true;
                response.Result = Mapper.Map<List<WorkloadRes>>(workload);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.Workload.Exception;
                Logger.LogError(ex, "Get user workload failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("{id:long}")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ResultRes<UserAdminRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDetail([FromRoute] long id)
        {
            var response = new ResultRes<UserAdminRes>();

            try
            {
                var (errorCode, user) = await userService.GetDetail(id);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<UserAdminRes>(user);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.Get.Exception;
                Logger.LogError(ex, "Get user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPost]
        [Route("")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ResultRes<UserCreatedRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(UserCreateReq request)
        {
            var response = new ResultRes<UserCreatedRes>();
            Response.Headers.CacheControl = "no-store";

            try
            {
                var requestDto = new BusinessLogic.Dtos.Users.UserCreateReqDto
                {
                    UserName = request.UserName,
                    DisplayName = request.DisplayName,
                    Role = request.Role.ToString(),
                };

                var (errorCode, result) = await userService.Create(requestDto);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<UserCreatedRes>(result);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.Create.Exception;
                Logger.LogError(ex, "Create user failed");
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/role")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ResultRes<UserAdminRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ChangeRole([FromRoute] long id, UserChangeRoleReq request)
        {
            var response = new ResultRes<UserAdminRes>();

            try
            {
                var (errorCode, user) = await userService.ChangeRole(id, request.Role);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<UserAdminRes>(user);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.ChangeRole.Exception;
                Logger.LogError(ex, "Change role of user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/deactivate")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ResultRes<UserAdminRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Deactivate([FromRoute] long id)
        {
            var response = new ResultRes<UserAdminRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();

                var (errorCode, user) = await userService.Deactivate(id, actorUserId);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                // f08-accounts-profile.md "Quick revocation on deactivation" /
                // f07-presence.md AC9: the hub-abort itself now happens
                // inside UserService.Deactivate (review-change: moved out of
                // this controller so the invariant holds for every caller,
                // not just this one HTTP action).
                response.Success = true;
                response.Result = Mapper.Map<UserAdminRes>(user);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.Deactivate.Exception;
                Logger.LogError(ex, "Deactivate user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/activate")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ResultRes<UserAdminRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Activate([FromRoute] long id)
        {
            var response = new ResultRes<UserAdminRes>();

            try
            {
                var (errorCode, user) = await userService.Activate(id);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<UserAdminRes>(user);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.Activate.Exception;
                Logger.LogError(ex, "Activate user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("{id:long}/password-reset")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ResultRes<PasswordResetRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResetPassword([FromRoute] long id)
        {
            var response = new ResultRes<PasswordResetRes>();
            Response.Headers.CacheControl = "no-store";

            try
            {
                var (errorCode, temporaryPassword) = await userService.ResetPassword(id);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = new PasswordResetRes { TemporaryPassword = temporaryPassword! };
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.ResetPassword.Exception;
                Logger.LogError(ex, "Reset password of user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        [HttpDelete]
        [Route("{id:long}/avatar")]
        [Authorize(Policy = Permissions.Users.ManageUsers)]
        [ProducesResponseType(typeof(ExecutionRes), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveAvatar([FromRoute] long id)
        {
            var response = new ExecutionRes();

            try
            {
                var errorCode = await userService.RemoveAvatar(id);
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
                response.ErrorCode = UserControllerMS.RemoveAvatar.Exception;
                Logger.LogError(ex, "Remove avatar of user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        // f08-accounts-profile.md "Avatars on the existing StorageService"
        // "Serving": private bucket, streamed through this authenticated
        // endpoint - never a presigned/public MinIO URL. ETag/If-None-Match
        // support avoids re-downloading an unchanged avatar.
        [HttpGet]
        [Route("{id:long}/avatar")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        public async Task<IActionResult> GetAvatar([FromRoute] long id, CancellationToken cancellationToken)
        {
            var response = new ExecutionRes();

            try
            {
                var (errorCode, objectKey) = await userService.GetAvatarObjectKey(id);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                if (string.IsNullOrEmpty(objectKey))
                {
                    response.ErrorCode = UserControllerMS.Avatar.NotFound;
                    return BadRequest(response);
                }

                var version = AvatarKeyHelper.ExtractVersion(objectKey);
                SetAvatarCacheHeaders(version);

                var ifNoneMatch = Request.Headers[HeaderNames.IfNoneMatch].ToString();
                if (!string.IsNullOrEmpty(version) && ifNoneMatch == $"\"{version}\"")
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }

                var (success, _, content, contentType, _) = await storageService.GetObject(objectKey, cancellationToken);
                if (!success || content == null)
                {
                    response.ErrorCode = UserControllerMS.Avatar.NotFound;
                    return BadRequest(response);
                }

                return File(content, contentType ?? "application/octet-stream");
            }
            catch (Exception ex)
            {
                response.ErrorCode = UserControllerMS.Avatar.Exception;
                Logger.LogError(ex, "Get avatar of user {Id} failed", id);
                return InternalServerError(response);
            }
        }

        private void SetAvatarCacheHeaders(string? version)
        {
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; sandbox";
            Response.Headers[HeaderNames.ContentDisposition] = "inline";

            if (!string.IsNullOrEmpty(version))
            {
                Response.Headers.CacheControl = "private, max-age=31536000, immutable";
                Response.Headers[HeaderNames.ETag] = $"\"{version}\"";
            }
            else
            {
                Response.Headers.CacheControl = "no-cache";
            }
        }
    }
}
