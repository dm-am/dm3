using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The secret of a second factor does not leave the perimeter.
/// </summary>
/// <remarks>
/// INV-15. A QR code is an encoding of the otpauth URI, and that URI contains the
/// shared secret in full: handing it to a public chart service to be drawn hands
/// the second factor of every account that sets one up to whoever runs that
/// service and to whoever reads its logs. The image looks the same either way,
/// which is exactly why the rule has to be mechanical - a reviewer looking at a
/// working QR code has nothing to notice.
///
/// Checked over the sources of every project, the client included: the drawing
/// can be done on either side, and the temptation to reach for a URL is on the
/// client side rather than the server one.
/// </remarks>
public class SecondFactorSecretShould
{
    /// <summary>
    /// Services that draw a QR code from a value in a URL.
    /// </summary>
    /// <remarks>
    /// Named one by one rather than matched by shape. What makes an address
    /// forbidden here is that the value to be drawn travels to somebody else's
    /// machine, and no pattern says that; a list somebody had to type out does.
    /// </remarks>
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

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IEnumerable<string> Sources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var extension = Path.GetExtension(file);
            if (extension is ".cs" or ".ts" or ".tsx" or ".vue" or ".js" or ".json" or ".html")
            {
                yield return file;
            }
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (BuildOutput.Contains(Path.GetFileName(nested), StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var file in Sources(nested))
            {
                yield return file;
            }
        }
    }

    [Fact]
    public void NeverBeDrawnByAService()
    {
        var root = Path.Combine(RepositoryRoot, "src");
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
                    Path.GetRelativePath(RepositoryRoot, source).Replace('\\', '/') + " -> " + host);
            }
        }

        read.Should().BeGreaterThan(500, "a walk that reads nothing passes on anything");

        offenders.Should().BeEmpty(
            "a QR code of an otpauth URI carries the shared secret in full, so drawing it " +
            "elsewhere hands the second factor of every account to whoever runs that service");
    }
}
