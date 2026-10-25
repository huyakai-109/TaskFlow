using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Auth;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.BusinessLogic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController(
        ILogger<AuthController> logger,
        IMapper mapper,
        IAuthService authService) : BaseController(logger, mapper)
    {
        [HttpPost]
        [Route("login")]
        [ProducesResponseType(typeof(ResultRes<LoginRes>), StatusCodes.Status200OK)]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginReq loginRes)
        {
            var result = new ResultRes<LoginRes>();

            try
            {
                if (string.IsNullOrEmpty(loginRes.Username)
                    || string.IsNullOrEmpty(loginRes.Password))
                {
                    result.Error = "Invalid credential";
                    return BadRequest(result);
                }

                var loginResult = await authService.Login(loginRes.DeviceGuid, loginRes.Username, loginRes.Password);
                if (loginResult.Success)
                {
                    result.Success = true;
                    result.Result = Mapper.Map<LoginRes>(loginResult);

                    return Ok(result);
                }

                if (loginResult.InvalidCredential)
                {
                    result.Error = "Invalid credential";
                }

                if (loginResult.WillBeLockedOut)
                {
                    result.Error = "Your account will be locked out";
                }

                if (loginResult.IsLockedOut)
                {
                    result.Error = "Your account has been locked out";
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Logger.LogError("Login failed: {ex}", ex);
                return InternalServerError(result);
            }
        }

        [HttpPut]
        [Route("refresh")]
        [ProducesResponseType(typeof(ResultRes<RefreshTokenRes>), StatusCodes.Status200OK)]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken(RefreshTokenReq refreshTokenReq)
        {
            var result = new ResultRes<RefreshTokenRes>();

            try
            {
                if (string.IsNullOrEmpty(refreshTokenReq.Token) || string.IsNullOrEmpty(refreshTokenReq.RefreshToken))
                {
                    result.Error = "Invalid tokens";
                    return BadRequest(result);
                }

                var refreshResult = await authService.RefreshToken(refreshTokenReq.DeviceGuid, refreshTokenReq.Token, refreshTokenReq.RefreshToken);

                if (!refreshResult.Success)
                {
                    result.Error = "Request failed";
                    return BadRequest(result);
                }

                result.Result = Mapper.Map<RefreshTokenRes>(refreshResult);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                Logger.LogError("Refresh token failed: {ex}", ex);
                return InternalServerError(result);
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("check")]
        public IActionResult Check()
        {
            return Ok();
        }
    }
}
