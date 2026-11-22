using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Profile;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Profile;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    // R2-3 (f08-accounts-profile.md "Self API"): every action acts on the
    // caller's own row only - the user id always comes from claims, never a
    // route parameter, so one user can never read or edit another's profile.
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/me")]
    public class ProfileController(
        ILogger<ProfileController> logger,
        IMapper mapper,
        IProfileService profileService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<MeRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMe()
        {
            var response = new ResultRes<MeRes>();

            try
            {
                var userId = this.User.Claims.GetUserId();

                var (errorCode, me) = await profileService.GetMe(userId);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<MeRes>(me);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ProfileControllerMS.Get.Exception;
                Logger.LogError(ex, "Get own profile failed");
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<MeRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateMe(ProfileUpdateReq request)
        {
            var response = new ResultRes<MeRes>();

            try
            {
                var userId = this.User.Claims.GetUserId();

                var (errorCode, me) = await profileService.UpdateDisplayName(userId, request.DisplayName);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<MeRes>(me);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ProfileControllerMS.Update.Exception;
                Logger.LogError(ex, "Update own profile failed");
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("password")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ExecutionRes), StatusCodes.Status200OK)]
        public async Task<IActionResult> ChangePassword(ChangePasswordReq request)
        {
            var response = new ExecutionRes();

            try
            {
                var userId = this.User.Claims.GetUserId();

                var errorCode = await profileService.ChangePassword(userId, request.CurrentPassword, request.NewPassword);
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
                response.ErrorCode = ProfileControllerMS.ChangePassword.Exception;
                Logger.LogError(ex, "Change own password failed");
                return InternalServerError(response);
            }
        }

        [HttpPut]
        [Route("avatar")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [RequestSizeLimit(2_200_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
        [ProducesResponseType(typeof(ResultRes<AvatarRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> PutAvatar(IFormFile file, CancellationToken cancellationToken)
        {
            var response = new ResultRes<AvatarRes>();

            try
            {
                var userId = this.User.Claims.GetUserId();

                await using var stream = file.OpenReadStream();
                var (errorCode, avatarUrl) = await profileService.PutAvatar(userId, stream, file.Length, cancellationToken);
                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = new AvatarRes { AvatarUrl = avatarUrl! };
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ProfileControllerMS.Avatar.Exception;
                Logger.LogError(ex, "Upload own avatar failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("summary")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<MeSummaryRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSummary()
        {
            var response = new ResultRes<MeSummaryRes>();

            try
            {
                var userId = this.User.Claims.GetUserId();

                var summary = await profileService.GetSummary(userId);

                response.Success = true;
                response.Result = Mapper.Map<MeSummaryRes>(summary);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ProfileControllerMS.Summary.Exception;
                Logger.LogError(ex, "Get own summary failed");
                return InternalServerError(response);
            }
        }

        [HttpDelete]
        [Route("avatar")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ExecutionRes), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteAvatar()
        {
            var response = new ExecutionRes();

            try
            {
                var userId = this.User.Claims.GetUserId();

                var errorCode = await profileService.DeleteAvatar(userId);
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
                response.ErrorCode = ProfileControllerMS.Avatar.Exception;
                Logger.LogError(ex, "Delete own avatar failed");
                return InternalServerError(response);
            }
        }
    }
}
