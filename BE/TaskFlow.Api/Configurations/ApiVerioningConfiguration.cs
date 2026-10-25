using Asp.Versioning;

namespace TaskFlow.Api.Configurations
{
    public static class ApiVerioningConfiguration
    {
        public static void AddApiVersionings(this IServiceCollection collection)
        {
            var apiVersioningBuilder = collection.AddApiVersioning(opt =>
            {
                opt.DefaultApiVersion = new ApiVersion(1, 0);
                opt.AssumeDefaultVersionWhenUnspecified = true;
                opt.ReportApiVersions = true;
                opt.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("x-api-version"),
                    new MediaTypeApiVersionReader("x-api-version"));
            });

            apiVersioningBuilder.AddApiExplorer(
                options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });
        }
    }
}
