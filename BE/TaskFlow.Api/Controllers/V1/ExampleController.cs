using Asp.Versioning;
using AutoMapper;
using TaskFlow.Api.Models.Requests.Examples;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Examples;
using TaskFlow.BusinessLogic.Cache;
using TaskFlow.BusinessLogic.Dtos.Examples;
using TaskFlow.BusinessLogic.Dtos.Storage;
using TaskFlow.BusinessLogic.Services;
using TaskFlow.BusinessLogic.Storage;
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
        IExampleService exampleService,
        ICacheManager cacheManager,
        IStorageService storageService) : BaseController(logger, mapper)
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

        [HttpGet]
        [Route("cache/test")]
        [AllowAnonymous]
        public async Task<IActionResult> TestCache()
        {
            await cacheManager.SetCachedData("Test", "test", TimeSpan.FromHours(1));
            return Ok(await cacheManager.GetCachedData<string>("Test"));
        }

        [HttpPost]
        [Route("minio/upload")]
        [AllowAnonymous]
        public async Task<IActionResult> UploadFile([FromForm] List<IFormFile> files)
        {
            var uploadDto = new UploadFileReqDto
            {
                File = files[0]
            };
            await storageService.Upload(uploadDto);

            return Ok();
        }
    }
}
