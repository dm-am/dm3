using System;
using System.IO;
using DM.Infrastructure.Core.Configuration;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Configuration;

/// <summary>
/// The sources of a host, weakest first: appsettings.json, its environment overlay,
/// the secrets files, the DM_ environment variables.
/// </summary>
/// <remarks>
/// Each row puts one key into two sources and expects the stronger; adjacent rows chain into the whole
/// order. Built over files on disk: the order exists only inside the builder.
/// </remarks>
public sealed class ConfigurationSourceOrderShould : IDisposable
{
    private const string EnvironmentName = "Probe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dm3-configuration-{Guid.NewGuid():N}");

    /// <summary>Unique per test, so the environment variable of one never reaches another.</summary>
    private readonly string _section = $"OrderProbe{Guid.NewGuid():N}";

    [Theory]
    [InlineData("appsettings.json", "appsettings.Probe.json")]
    [InlineData("appsettings.Probe.json", "secrets/appsettings.json")]
    [InlineData("secrets/appsettings.json", "secrets/appsettings.Probe.json")]
    [InlineData("appsettings.json", "secrets/appsettings.json")]
    public void LetTheLaterFileWin(string weaker, string stronger)
    {
        Write(weaker, "weaker");
        Write(stronger, "stronger");

        Resolve().Should().Be("stronger",
            $"{stronger} is read after {weaker}; a secret that loses to the tracked default " +
            "is a password the operator wrote down and the host never used");
    }

    [Fact]
    public void LetAnEnvironmentVariableWinOverEveryFile()
    {
        Write("appsettings.json", "tracked");
        Write("secrets/appsettings.Probe.json", "secret");

        var variable = $"DM_{_section}__Value";
        Environment.SetEnvironmentVariable(variable, "variable");
        try
        {
            Resolve().Should().Be("variable",
                "compose supplies the deployment through DM_ variables, and they have the last word");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Write(string relativePath, string value)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{ "{{_section}}": { "Value": "{{value}}" } }""");
    }

    private string? Resolve()
    {
        using var host = Host.CreateDefaultBuilder()
            .UseContentRoot(_root)
            .UseEnvironment(EnvironmentName)
            .WithDmConfiguration()
            .Build();

        return host.Services.GetRequiredService<IConfiguration>()[$"{_section}:Value"];
    }
}
