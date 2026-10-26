using TaskFlow.Api.Models.Responses.Base;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Configurations
{
    public static class ApiBehaviorConfiguration
    {
        // Cross-cutting fix (R1): model-binding/model-validation failures must
        // carry the project's envelope, not ASP.NET's default ProblemDetails.
        // Feature error codes follow "<Feature>.<Operation>.<Reason>" via a
        // "<Feature>ControllerMS" class (backend.md, section 5); this one code
        // covers every action, ahead of the first feature establishing that
        // pattern.
        public const string InvalidModelStateErrorCode = "Request.Validation.Failed";

        public static void AddInvalidModelStateHandling(this IMvcBuilder builder)
        {
            builder.ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var validates = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .Select(entry => new ValidateRes
                        {
                            Key = entry.Key,
                            Errors = entry.Value!.Errors
                                .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                                    ? error.Exception?.Message ?? "Invalid value"
                                    : error.ErrorMessage)
                                .ToArray(),
                        })
                        .ToArray();

                    var response = new ExecutionRes
                    {
                        Success = false,
                        ErrorCode = InvalidModelStateErrorCode,
                        Error = "Request validation failed",
                        Validates = validates,
                    };

                    return new BadRequestObjectResult(response);
                };
            });
        }
    }
}
