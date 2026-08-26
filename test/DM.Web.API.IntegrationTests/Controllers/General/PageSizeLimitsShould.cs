using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A page size outside the declared range is a caller mistake, not a clipped page.
/// </summary>
/// <remarks>
/// API_DESIGN states it: take is 1 to 100, and a value outside that is a
/// validation error, because a caller who asked for a thousand has to learn that
/// a thousand is not on offer instead of taking a hundred for the whole set.
///
/// The security journal clipped only the top. Below the range nothing looked,
/// and its limit used to go straight into a driver where zero means "no
/// limit" — so ?take=0 answered with the whole login and password history of an
/// account, on a free request, from an endpoint whose own documentation promises
/// a maximum of 100.
/// </remarks>
public class PageSizeLimitsShould : IntegrationTestBase
{
    public PageSizeLimitsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("/v1/account/logs?take=0")]
    [InlineData("/v1/account/logs?take=-1")]
    [InlineData("/v1/account/logs?take=1000")]
    public async Task RefuseALimitOutsideTheDeclaredRange(string url)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcceptALimitInsideTheDeclaredRange()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, "/v1/account/logs?take=10"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
