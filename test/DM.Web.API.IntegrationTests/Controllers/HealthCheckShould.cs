using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

public class HealthCheckShould : IntegrationTestBase
{
    public HealthCheckShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // A liveness probe that fails on a database restarts a container that was fine, so no check
    // runs.
    [Fact]
    public async Task LivenessAnswersWithoutRunningAnyCheck()
    {
        var response = await Client.GetAsync("/_health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("postgresql");
        body.Should().NotContain("rabbitmq");
        body.Should().NotContain("realtime-push");
    }

    /// <remarks>
    /// The status depends on the machine; the report names the checks. The broker and the realtime subscription
    /// are not readiness: a request is served without them.
    /// </remarks>
    [Fact]
    public async Task ReadinessReportsTheStoresARequestNeeds()
    {
        var response = await Client.GetAsync("/_ready");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("postgresql");
        body.Should().NotContain("rabbitmq");
        body.Should().NotContain("realtime-push");
    }

    /// <summary>
    /// The detail endpoint is the one that hides nothing: broker and realtime
    /// subscription included.
    /// </summary>
    /// <remarks>This report is the only place inside the host that says push is not reaching the instance.</remarks>
    [Fact]
    public async Task DetailReportsEveryCheckTheHostRegisters()
    {
        var response = await Client.GetAsync("/_health/detail");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("postgresql");
        body.Should().Contain("rabbitmq");
        body.Should().Contain("realtime-push");
    }
}
