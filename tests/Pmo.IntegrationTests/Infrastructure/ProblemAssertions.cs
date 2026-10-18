using System.Net;
using System.Text.Json;
using Shouldly;

namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>Reads RFC 7807 ProblemDetails responses: status, business-rule code and field errors.</summary>
public static class ProblemAssertions
{
    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        if (code is not null)
        {
            (await ReadAsync(response)).GetProperty("code").GetString().ShouldBe(code);
        }
    }

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>The ProblemDetails "errors" entry for one field (camelCase), as a list of messages.</summary>
    public static async Task<string[]> FieldErrorsAsync(this HttpResponseMessage response, string field) =>
        (await ReadAsync(response)).GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!).ToArray();
}
