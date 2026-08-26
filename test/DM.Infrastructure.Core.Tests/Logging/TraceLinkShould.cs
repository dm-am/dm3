using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Logging;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// A log line names the trace it was written in, under the name the dashboard
/// looks for.
/// </summary>
/// <remarks>
/// It did not, and nothing said so. An enricher put the ids on as properties
/// called TraceId and SpanId; both names are reserved by the sink, which renamed
/// them to _TraceId and _SpanId on the way out. The derived field of the log
/// datasource matches a quote immediately before the name, so it matched nothing:
/// every line carried the ids, and not one of them was ever a link into a trace.
///
/// Which is why this test speaks to a real sink over a real socket and reads the
/// bytes that would have gone to the store. The three parts that have to agree -
/// what the sink writes, what it calls it, and what the datasource looks for -
/// live in three files, two languages and one package, and no compiler sees any
/// pair of them.
/// </remarks>
[Collection(StaticLoggerCollection.Name)]
public class TraceLinkShould
{
    [Fact]
    public async Task ReachTheDashboardUnderTheNameItSearchesFor()
    {
        var matcher = DerivedFieldPattern("TraceId");

        using var listener = new HttpListener();
        var port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var pushed = ReadOnePush(listener);

        using var activity = new Activity("probe").Start();
        var traceId = activity.TraceId.ToString();

        var services = new ServiceCollection().AddDmLogging("DM.Probe", Configuration(port), Environment());
        using (var provider = services.BuildServiceProvider())
        {
            provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("probe")
                .LogWarning("a line written inside a trace");
        }

        // The sink ships on a timer and on dispose; the logger above is the static
        // one AddDmLogging installs, so closing it is what flushes the batch.
        await Log.CloseAndFlushAsync();

        var body = await pushed;
        listener.Stop();

        body.Should().NotBeNullOrEmpty("the sink has to have pushed the line somewhere");

        // The line, not the envelope it travelled in. The dashboard applies the
        // expression to what it shows, and what it shows is one value out of the
        // stream - reading the envelope instead would test the escaping of the
        // push protocol.
        var line = LineOf(body);

        var found = Regex.Match(line, matcher);
        found.Success.Should().BeTrue(
            $"the datasource looks for {matcher}, and the line it reads is what the sink wrote");
        found.Groups[1].Value.Should().Be(traceId,
            "a link that opens the wrong trace is worse than no link");

        // Not a derived field of its own: the span is what says which part of the
        // request a line belongs to, and it is read beside the trace it opens.
        Regex.IsMatch(line, @"""SpanId"":""\w+""").Should().BeTrue(
            "a trace without the span the line came from is a haystack");
    }

    /// <summary>The one log line inside a push, as the dashboard would show it.</summary>
    private static string LineOf(string push)
    {
        using var document = JsonDocument.Parse(push);

        return document.RootElement
            .GetProperty("streams")[0]
            .GetProperty("values")[0][1]
            .GetString()!;
    }

    /// <summary>The expression the log datasource extracts a field with.</summary>
    private static string DerivedFieldPattern(string field)
    {
        var datasources = File.ReadAllText(Path.Combine(DM.Testing.RepositoryLayout.Root,
            "docker", "grafana", "provisioning", "datasources", "datasources.yml"));

        var derived = Regex.Match(datasources,
            $@"-\s*name:\s*{Regex.Escape(field)}\s*\n\s*matcherRegex:\s*'([^']+)'");

        derived.Success.Should().BeTrue($"the datasource declares a derived field for {field}");
        return derived.Groups[1].Value;
    }

    private static IConfiguration Configuration(int port) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Logs"] = $"http://127.0.0.1:{port}",
                ["ConnectionStrings:TracingEndpoint"] = "http://127.0.0.1:4317",
            })
            .Build();

    private static IHostEnvironment Environment() =>
        new HostingEnvironment { EnvironmentName = Environments.Production };

    private static async Task<string> ReadOnePush(HttpListener listener)
    {
        var context = await listener.GetContextAsync();
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        context.Response.StatusCode = 204;
        context.Response.Close();

        return body;
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
