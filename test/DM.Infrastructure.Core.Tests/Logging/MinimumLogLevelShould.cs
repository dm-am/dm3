using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Logging;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Serilog;
using Serilog.Events;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// The level of the log is a deployment decision, and a deployment can make it.
/// </summary>
/// <remarks>
/// It was compiled in: Debug in Development and Information everywhere else,
/// with nothing reading configuration. So the only way to see the Debug lines an
/// incident needs - refused authentications among them - was to set the
/// environment to Development, which is not a logging switch: the same flag
/// mounts Swagger, allows inline script and drops HSTS.
///
/// Asserted through AddDmLogging rather than on the private reader, because the
/// defect was never in parsing a level - it was that nothing read one.
/// </remarks>
[Collection(StaticLoggerCollection.Name)]
public class MinimumLogLevelShould
{
    [Fact]
    public async Task StayAtInformationOnAServerWithNothingConfigured() =>
        (await LowestEnabled(Environments.Production, configured: null))
            .Should().Be(LogEventLevel.Information,
                "Debug on a server writes every framework trace into a store that keeps a " +
                "month of them, and buries the events worth reading");

    [Fact]
    public async Task ReachDebugOnAServerWhenTheDeploymentAsksFor() =>
        (await LowestEnabled(Environments.Production, "Debug"))
            .Should().Be(LogEventLevel.Debug,
                "an incident that needs the Debug lines must not require setting the " +
                "environment to Development, which also publishes Swagger and drops HSTS");

    [Fact]
    public async Task KeepDebugOnADeveloperMachineWithNothingConfigured() =>
        (await LowestEnabled(Environments.Development, configured: null))
            .Should().Be(LogEventLevel.Debug,
                "the environment picks the default, and the override is an override");

    [Fact]
    public void RefuseAValueThatIsNotALevel()
    {
        var install = () => Install(Environments.Production, "chatty");

        install.Should().Throw<InvalidOperationException>(
                "a silent fallback to the default restores the very defect this replaces - " +
                "a knob that turns and does nothing - at the moment somebody is turning it")
            .WithMessage("*chatty*");
    }

    /// <summary>Lowest level the installed logger lets through.</summary>
    private static async Task<LogEventLevel> LowestEnabled(string environmentName, string? configured)
    {
        var previous = Log.Logger;
        Install(environmentName, configured);

        try
        {
            return Enum.GetValues<LogEventLevel>().First(Log.Logger.IsEnabled);
        }
        finally
        {
            // Closes the logger installed above - which owns a push sink with a
            // timer - and puts back whatever the process had before it.
            await Log.CloseAndFlushAsync();
            Log.Logger = previous;
        }
    }

    private static void Install(string environmentName, string? configured)
    {
        var settings = new Dictionary<string, string?>
        {
            // Nothing is written in this test, so the address only has to parse.
            ["ConnectionStrings:Logs"] = "http://127.0.0.1:3100",
            ["ConnectionStrings:TracingEndpoint"] = "http://127.0.0.1:4317",
        };

        if (configured is not null)
        {
            settings["Observability:MinimumLevel"] = configured;
        }

        new ServiceCollection().AddDmLogging("DM.Probe",
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new HostingEnvironment { EnvironmentName = environmentName });
    }
}
