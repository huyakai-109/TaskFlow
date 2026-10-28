using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.Configurations;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Tests.Fixtures;

namespace TaskFlow.Tests.Http
{
    // R1: a model-binding failure (not an attribute failure - this project has
    // no [Required] attributes yet) must carry the project's envelope, not
    // ASP.NET's default ProblemDetails shape.
    //
    // In the "Database" collection (not just its own IClassFixture<TestWebApplicationFactory>)
    // so xUnit never runs it in parallel with another DB-touching test class -
    // see DatabaseFixture and the comment on DatabaseCollection.
    [Collection(DatabaseCollection.Name)]
    public class LoginValidationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public LoginValidationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_ReturnsEnvelopeWithValidationErrors_WhenDeviceGuidIsNotAGuid()
        {
            // Arrange
            var client = _factory.CreateClient();
            var payload = new
            {
                deviceGuid = "not-a-guid",
                username = "someone",
                password = "whatever",
            };

            // Act
            var response = await client.PostAsJsonAsync("api/v1/auth/login", payload);
            var body = await response.Content.ReadFromJsonAsync<ExecutionRes>();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(body);
            Assert.False(body!.Success);
            Assert.Equal(ApiBehaviorConfiguration.InvalidModelStateErrorCode, body.ErrorCode);
            Assert.NotNull(body.Validates);
            Assert.NotEmpty(body.Validates!);
            Assert.All(body.Validates!, validate => Assert.NotEmpty(validate.Errors));
        }
    }
}
