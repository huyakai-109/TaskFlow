using System.Net;
using System.Net.Http.Json;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // f08-accounts-profile.md "Anonymous MinIO upload endpoint": both
    // AllowAnonymous scaffold routes are removed outright (R2-3/R2-6) -
    // this guards against either one silently coming back.
    [Collection(DatabaseCollection.Name)]
    public class ExampleControllerAnonymousRoutesRemovedTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
    {
        [Fact]
        public async Task MinioUpload_IsNotReachable()
        {
            var client = factory.CreateClient();
            using var content = new MultipartFormDataContent();

            var response = await client.PostAsync("api/v1/example/minio/upload", content);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CacheTest_IsNotReachable()
        {
            var client = factory.CreateClient();

            var response = await client.GetAsync("api/v1/example/cache/test");

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
