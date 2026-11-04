using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The secret of a second factor does not leave the perimeter.
/// </summary>
/// <remarks>
/// The otpauth URI carries the shared secret in full, and a QR image looks the same either way. Over every
/// project, the client included.
/// </remarks>
public class SecondFactorSecretShould
{
    /// <summary>
    /// Services that draw a QR code from a value in a URL.
    /// </summary>
    /// <remarks>Named one by one: no pattern says the value travels to somebody else's machine.</remarks>
    private static readonly string[] DrawnByStrangers =
    [
        "api.qrserver.com",
        "chart.googleapis.com",
        "chart.apis.google.com",
        "quickchart.io",
        "qrcode.tec-it.com",
        "goqr.me",
        "qrickit.com",
        "qrserver.com",
    ];

    private static readonly string[] BuildOutput =
        ["bin", "obj", "node_modules", "coverage", "dist", ".git", "artifacts", "TestResults"];


    private static IEnumerable<string> Sources(string directory) => RepositoryFiles
        .Files(directory, "*", BuildOutput)
        .Where(file => Path.GetExtension(file) is ".cs" or ".ts" or ".tsx" or ".vue" or ".js" or ".json" or ".html");

    [Fact]
    public void NeverBeDrawnByAService()
    {
        var root = Path.Combine(RepositoryLayout.Root, "src");
        var read = 0;
        var offenders = new List<string>();

        foreach (var source in Sources(root))
        {
            read++;
            var text = File.ReadAllText(source);
            foreach (var host in DrawnByStrangers.Where(host =>
                         text.Contains(host, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add(
                    Path.GetRelativePath(RepositoryLayout.Root, source).Replace('\\', '/') + " -> " + host);
            }
        }

        read.Should().BePositive("a walk that reads nothing passes on anything");

        offenders.Should().BeEmpty(
            "a QR code of an otpauth URI carries the shared secret in full, so drawing it " +
            "elsewhere hands the second factor of every account to whoever runs that service");
    }
}
