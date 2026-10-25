using TaskFlow.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TaskFlow.Api.Filters
{
    public class SwaggerAuthorizationHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var allowAnonymous = context.ApiDescription.ActionDescriptor
                .EndpointMetadata.Any(item => item is AllowAnonymousAttribute);

            if (allowAnonymous) return;

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            // Swashbuckle 10 / Microsoft.OpenApi 2: a scheme is referenced by id against the document.
            var scheme = new OpenApiSecuritySchemeReference(RestfulConstants.RequestHeaders.AuthScheme, context.Document);

            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [scheme] = new List<string>(),
            });
        }
    }
}
