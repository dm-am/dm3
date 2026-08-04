using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The origin check refuses in the same shape as everything else refuses.
/// </summary>
/// <remarks>
/// One middleware owns the body of every refusal in this host, and it builds it
/// through ProblemDetailsFactory, which is what puts the correlation token in.
/// The CSRF middleware assembled its own ProblemDetails instead: right status,
/// right media type, no traceId and no type. The token exists precisely so a
/// refusal can be matched to a line in the log, and this was the one refusal
/// that could not be.
/// </remarks>
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
