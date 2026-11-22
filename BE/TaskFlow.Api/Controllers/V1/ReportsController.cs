using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Reports;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Reports;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    // f09-reports.md "Technical contract": every action reads the caller's
    // claims and passes them inward (TasksController precedent); the
    // scorecard/drill-down endpoints use ViewTasks, with R1's self-only
    // check enforced in the service, never only by the client.
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/reports")]
    public class ReportsController(
        ILogger<ReportsController> logger,
        IMapper mapper,
        IReportService reportService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [Route("scorecard")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(ResultRes<ReportScorecardRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetScorecard([FromQuery] ReportScorecardReq request)
        {
            var response = new ResultRes<ReportScorecardRes>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, result) = await reportService.GetScorecard(request.UserId, request.From!.Value, request.To!.Value, request.Granularity, actorUserId, callerPermissions);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<ReportScorecardRes>(result);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ReportControllerMS.GetScorecard.Exception;
                Logger.LogError(ex, "Get report scorecard failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("team")]
        [Authorize(Policy = Permissions.Reports.ViewReports)]
        [ProducesResponseType(typeof(ResultRes<ReportTeamRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTeam([FromQuery] ReportTeamReq request)
        {
            var response = new ResultRes<ReportTeamRes>();

            try
            {
                var (errorCode, result) = await reportService.GetTeam(request.From!.Value, request.To!.Value);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<ReportTeamRes>(result);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ReportControllerMS.GetTeam.Exception;
                Logger.LogError(ex, "Get report team failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("tasks")]
        [Authorize(Policy = Permissions.Tasks.ViewTasks)]
        [ProducesResponseType(typeof(PaginationResultRes<List<ReportTaskRes>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTasks([FromQuery] ReportTasksReq request)
        {
            var response = new PaginationResultRes<List<ReportTaskRes>>();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, page) = await reportService.GetTasks(request.UserId, request.From!.Value, request.To!.Value, request.Metric!.Value, request.Start, request.Length, actorUserId, callerPermissions);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                response.Success = true;
                response.Result = Mapper.Map<List<ReportTaskRes>>(page!.Items);
                response.Pagination = new PaginationRes(page.TotalItems, page.Items.Count, (page.PageNumber - 1) * page.PageSize, page.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ReportControllerMS.GetTasks.Exception;
                Logger.LogError(ex, "Get report drill-down tasks failed");
                return InternalServerError(response);
            }
        }

        [HttpGet]
        [Route("export")]
        [Authorize(Policy = Permissions.Reports.ExportReports)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Export([FromQuery] ReportExportReq request)
        {
            var response = new ExecutionRes();

            try
            {
                var actorUserId = this.User.Claims.GetUserId();
                var callerPermissions = this.User.Claims.GetUserRoleClaims();

                var (errorCode, content, fileName) = await reportService.Export(request.UserId, request.From!.Value, request.To!.Value, request.Granularity, request.Dataset!.Value, actorUserId, callerPermissions);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    response.ErrorCode = errorCode;
                    return BadRequest(response);
                }

                Response.Headers.CacheControl = "no-store";
                return File(content!, $"{GlobalConstants.Files.CsvContentType}; charset=utf-8", fileName);
            }
            catch (Exception ex)
            {
                response.ErrorCode = ReportControllerMS.Export.Exception;
                Logger.LogError(ex, "Export report failed");
                return InternalServerError(response);
            }
        }
    }
}
