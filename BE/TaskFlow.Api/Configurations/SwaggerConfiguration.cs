using Asp.Versioning.ApiExplorer;
using TaskFlow.Api.Filters;
using TaskFlow.Common.Constants;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TaskFlow.Api.Configurations
{
    public static class SwaggerConfiguration
    {
        public static void AddSwaggers(this IServiceCollection collection)
        {
            collection.AddEndpointsApiExplorer();
            collection.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition(RestfulConstants.RequestHeaders.AuthScheme, new OpenApiSecurityScheme
                {
                    Scheme = "Bearer",
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = RestfulConstants.RequestHeaders.Authorization,
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http
                });
                c.OperationFilter<SwaggerAuthorizationHeaderFilter>();
            });
            collection.ConfigureOptions<ConfigureSwaggerOptions>();
        }

        public static void UseSwaggers(this WebApplication app)
        {
            var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                        description.GroupName.ToUpperInvariant());
                }
            });
        }
    }

    public class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        : IConfigureNamedOptions<SwaggerGenOptions>
    {
        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, CreateVersionInfo(description));
            }
        }

        public void Configure(string? name, SwaggerGenOptions options)
        {
            Configure(options);
        }

        private static OpenApiInfo CreateVersionInfo(ApiVersionDescription desc)
        {
            var info = new OpenApiInfo
            {
                Title = "TaskFlow.Api",
                Version = desc.ApiVersion.ToString()
            };

            if (desc.IsDeprecated)
            {
                info.Description = "This Api version has been deprecated";
            }

            return info;
        }
    }
}
