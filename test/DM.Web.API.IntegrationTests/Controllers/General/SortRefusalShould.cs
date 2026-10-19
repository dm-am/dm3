using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A sort field the endpoint does not have comes back as 400, on the wire.
/// </summary>
/// <remarks>
/// The unit test around SortVocabularyFilter proves the rule, this that it is switched on. The endpoints come
/// from the published document.
/// </remarks>
public class SortRefusalShould : IntegrationTestBase
{
    public SortRefusalShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Stands in for any path parameter: the filter runs before the action, ahead of any lookup.
    /// </summary>
    private const string PathParameterStandIn = "00000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task AnswerFourHundredToASortFieldTheEndpointDoesNotHave()
    {
        var wrong = new List<string>();
        var asked = 0;

        foreach (var (path, hasSortBy) in await SortableGetPaths())
        {
            var url = Regex.Replace(path, "{[^}]+}", PathParameterStandIn);
            var parameter = hasSortBy ? "sortBy=nonsense" : "sortOrder=ascending";

            var response = await Client.GetAsync($"{url}?{parameter}");
            asked++;

            if (response.StatusCode != HttpStatusCode.BadRequest)
            {
                wrong.Add($"GET {path}?{parameter} -> {(int)response.StatusCode}");
            }
        }

        asked.Should().BeGreaterThan(10, "the API publishes sorted list endpoints");
        wrong.Should().BeEmpty(
            "an unknown sort value is refused, not answered with 200 in the order the repository falls back to");
    }

    /// <summary>
    /// Every published GET that takes a sort, and whether it takes a sortBy
    /// (the ones that do not still take a direction).
    /// </summary>
    private async Task<IReadOnlyList<(string Path, bool HasSortBy)>> SortableGetPaths()
    {
        var found = new List<(string, bool)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                if (!path.Value.TryGetProperty("get", out var operation) ||
                    !operation.TryGetProperty("parameters", out var parameters))
                {
                    continue;
                }

                var hasSortBy = false;
                var hasSortOrder = false;
                foreach (var parameter in parameters.EnumerateArray())
                {
                    var name = parameter.TryGetProperty("name", out var value) ? value.GetString() : null;
                    hasSortBy |= name == "sortBy";
                    hasSortOrder |= name == "sortOrder";
                }

                if ((hasSortBy || hasSortOrder) && seen.Add(path.Name))
                {
                    found.Add((path.Name, hasSortBy));
                }
            }
        }

        return found;
    }
}
