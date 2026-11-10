using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// The body of every refusal is built through ProblemDetailsFactory, which puts the correlation
// token in.
public class CsrfRefusalShould : IntegrationTestBase
{
    public CsrfRefusalShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task AnswerWithTheSameProblemDocumentAsEveryOtherRefusal()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/users/me/notepad");
        request.Headers.Add("Origin", "https://not-this-site.example");
        request.Content = JsonContent.Create(new { title = "чужой источник", content = "тело" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.TryGetProperty("traceId", out _).Should()
            .BeTrue("a refusal without a correlation token cannot be found in the log");
        document.RootElement.TryGetProperty("type", out _).Should().BeTrue();
        document.RootElement.GetProperty("status").GetInt32().Should().Be(403);
    }
}
