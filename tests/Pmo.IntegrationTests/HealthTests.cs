using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pmo.Api.Controllers;
using Pmo.IntegrationTests.Infrastructure;
using Shouldly;

namespace Pmo.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthTests
{
    private readonly PmoApiFactory _factory;

    public HealthTests(PmoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_WithDatabaseUp_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.ShouldBe(new HealthResponse("healthy", "up"));
    }

    [Fact]
    public async Task GetHealth_WithCorrelationIdHeader_EchoesSameId()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-abc-123");

        var response = await client.GetAsync("/api/health");

        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("test-abc-123");
    }

    [Fact]
    public async Task UnknownRoute_WhenSignedIn_Returns404ProblemDetailsWithCorrelationId()
    {
        var client = await _factory.CreateClientForAsync(DemoUsers.Admin1);   // anonymous callers get 401 first (fallback policy)

        var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        var correlationId = json.RootElement.GetProperty("correlationId").GetString();
        correlationId.ShouldNotBeNullOrWhiteSpace();
        correlationId.ShouldBe(response.Headers.GetValues("X-Correlation-Id").Single());
    }
}
