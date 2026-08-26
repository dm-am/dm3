using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for health endpoints
/// </summary>
public class HealthCheckShould : IntegrationTestBase
{
    public HealthCheckShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Liveness answers whether the process is up, and nothing else.
    /// </summary>
    /// <remarks>
    /// It is mapped with a predicate that selects no check at all, so it answers
    /// 200 whatever the state of the stores behind it. The test that used to
    /// stand here was called "returns healthy" and asserted that 200 - which is
    /// what the endpoint returns when every dependency is gone, and therefore a
    /// claim about nothing. What is worth pinning is the shape: no check runs
    /// here, because a liveness probe that fails on a database restarts a
    /// container that was fine.
    /// </remarks>
    [Fact]
    public async Task Liveness_AnswersWithoutRunningAnyCheck()
    {
        var response = await Client.GetAsync("/_health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("postgresql");
        body.Should().NotContain("rabbitmq");
    }

    /// <summary>
    /// Readiness answers whether this instance can serve a request, which means
    /// it has to run the checks liveness deliberately skips.
    /// </summary>
    /// <remarks>
    /// The status is not asserted: whether the stores this instance is pointed at
    /// answer depends on the machine the suite runs on, and that is exactly the
    /// difference from liveness. What is asserted is that the report names them,
    /// because an endpoint that names no check cannot report one down - the
    /// failure mode /_health has by design and /_ready must not acquire.
    ///
    /// The broker is deliberately absent: a request is served without it, and
    /// tagging it ready pulled the whole site out of rotation over a delayed
    /// notification.
    /// </remarks>
    [Fact]
    public async Task Readiness_ReportsTheStoresARequestNeeds()
    {
        var response = await Client.GetAsync("/_ready");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("postgresql");
        body.Should().NotContain("rabbitmq");
    }

    /// <summary>
    /// The detail endpoint is the one that hides nothing, broker included.
    /// </summary>
    [Fact]
    public async Task Detail_ReportsEveryCheckTheHostRegisters()
    {
        var response = await Client.GetAsync("/_health/detail");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("postgresql");
        body.Should().Contain("rabbitmq");
    }

    /// <summary>
    /// Swagger endpoint should be available (using Forum group as example)
    /// </summary>
    [Fact]
    public async Task Swagger_ReturnsOk()
    {
        // Act - Swagger docs are generated per API group (Forum, Game, etc.)
        var response = await Client.GetAsync("/swagger/Forum/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("openapi");
    }
}
