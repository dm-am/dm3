using System.Net;
using System.Text.Json;
using DM.Web.API.Swagger;
using AwesomeAssertions;

namespace DM.Web.API.IntegrationTests.Helpers;

public static class SwaggerDocuments
{
    // Each document is cloned off its parser, so the members outlive the read.
    public static async Task<IReadOnlyList<SwaggerOperation>> OperationsAsync(HttpClient client)
    {
        var operations = new List<SwaggerOperation>();

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var parsed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var document = parsed.RootElement.Clone();
            foreach (var path in document.GetProperty("paths").EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    operations.Add(new SwaggerOperation(document, path.Name, operation.Name, operation.Value));
                }
            }
        }

        return operations;
    }
}

/// <summary>One member of a path item, with the document it was read from.</summary>
/// <param name="Document">The root of the group's document, for the references a member makes.</param>
/// <param name="Path">The path template the item is published under.</param>
/// <param name="Name">The member's key: an HTTP verb for an operation.</param>
/// <param name="Value">The member itself.</param>
public sealed record SwaggerOperation(JsonElement Document, string Path, string Name, JsonElement Value)
{
    public string Address => $"{Name.ToUpperInvariant()} {Path}";
}
