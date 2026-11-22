using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Examples;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Examples;
using TaskFlow.BusinessLogic.Dtos.Examples;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.V1
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/example")]
    public class ExampleController(
        ILogger<ExampleController> logger,
        IMapper mapper,
        IExampleService exampleService) : BaseController(logger, mapper)
    {
        [HttpGet]
        [ProducesResponseType(typeof(ResultRes<ExampleNewRes?>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetExample(long id)
        {
            var result = new ResultRes<ExampleNewRes?>();
            try
            {
                if (id <= 0)
                {
                    result.Error = $"Invalid Id value: {id}";
                    return BadRequest(result);
                }

                result.Result = Mapper.Map<ExampleNewRes>(await exampleService.Get(id));

                if (result.Result == null)
                {
                    result.Error = $"Unable to find entity with Id: {id}";
                    return NotFound(result);
                }

                result.Success = true;

                return Ok(result);
            }
            catch (Exception ex)
            {
                Logger.LogError("Get example {id} failed: {ex}", id, ex);
                result.Success = false;
                return InternalServerError(result);
            }
        }

        [HttpPost]
        [Route("")]
        [ProducesResponseType(typeof(ResultRes<ExampleNewRes>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SaveExample(ExampleNewReq request)
        {
            var response = new ResultRes<ExampleNewRes>();

            if (string.IsNullOrEmpty(request.Name))
            {
                response.Error = "Name property is null or empty";
                return BadRequest(response);
            }

            var result = await exampleService.Insert(Mapper.Map<ExampleDto>(request));

            response.Result = Mapper.Map<ExampleNewRes>(result);
            response.Success = true;

            return Ok(response); // 200
        }

        [HttpGet]
        [Route("weather-forecast")]
        [Authorize(Policy = Permissions.Reports.ManageReports)]
        public IEnumerable<object> Get()
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            return Enumerable.Range(1, 5).Select(index =>
                {
                    var temperatureC = Random.Shared.Next(-20, 55);

                    return new
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = temperatureC,
                        TemperatureF = 32 + (int)(temperatureC / 0.5556),
                        Summary = summaries[Random.Shared.Next(summaries.Length)]
                    };
                })
                .ToArray();
        }

        // f08-accounts-profile.md "Anonymous MinIO upload endpoint" (R2-3/
        // R2-6): /example/minio/upload and /example/cache/test were
        // AllowAnonymous scaffold routes writing into the public bucket /
        // the distributed cache with no authentication. Neither BE nor FE
        // product code called either route (grepped before removal) - both
        // are removed outright rather than put behind [Authorize], per the
        // spec's "if nothing calls it, delete the action" instruction.
        // ExampleControllerAnonymousRoutesRemovedTests asserts both routes
        // now 404.
    }
}
