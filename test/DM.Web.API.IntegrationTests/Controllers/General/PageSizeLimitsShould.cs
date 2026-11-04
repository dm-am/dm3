using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A page size outside the declared range is a caller mistake, not a clipped page.
/// </summary>
/// <remarks>
/// API_DESIGN states it: take runs from 1 to PagingPolicy.MaxPageSize. The security journal declares a take of
/// its own, and the warning log is the shared PagingQuery on a public endpoint.
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
    [InlineData("/v1/warnings?take=0")]
    [InlineData("/v1/warnings?take=201")]
    [InlineData("/v1/warnings?skip=-1")]
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
