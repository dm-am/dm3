using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Disclosing existence is a recorded trade made acceptable by one entry per disclosing answer, so
/// the document and the surfaces are held to each other both ways.
/// </remarks>
public class IdentifierDisclosureShould
{
    private static readonly string Convention =
        Path.Combine("docs", "conventions", "SECURITY.md");

    private static readonly Regex Documented = new(
        @"^\|\s*`(?:GET|POST|PUT|DELETE)\s+/v1/account/([\w-]+)`\s*\|",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>A surface as the recording helper is called with it.</summary>
    private static readonly Regex Recorded = new(
        @"IdentifierDisclosed\(\s*\w+\s*,\s*""([\w-]+)""", RegexOptions.Compiled);

    [Fact]
    public void RecordEverySurfaceTheDocumentDeclares()
    {
        var documented = Documented
            .Matches(File.ReadAllText(Path.Combine(RepositoryLayout.Root, Convention)))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        documented.Should().NotBeEmpty(
            $"{Convention} lists the surfaces that disclose, and a walk that reads none of " +
            "them passes whatever the code does");

        var recorded = RecordedSurfaces();
        recorded.Should().NotBeEmpty(
            "the surfaces record their disclosing answers, and finding no call passes " +
            "whatever the document says");

        documented.Except(recorded).Should().BeEmpty(
            "a surface the document says discloses and that records nothing leaves a scan of " +
            "it with no trace at all, which is the one thing the recorded exception is paid " +
            "for");
        recorded.Except(documented).Should().BeEmpty(
            "a surface that discloses without being in the table is a risk nobody weighed, " +
            "and the reader of the document is counting on the list being complete");
    }

    // Entries live for a month: the value would make the record a better list than the scan.
    [Fact]
    public void KeepTheProbedIdentifierOutOfTheEntry()
    {
        var helper = File.ReadAllText(Path.Combine(RepositoryLayout.Root,
            "src", "DM.Web.API", "Features", "Account", "IdentifierProbeLog.cs"));

        helper.Should().Contain("{ClientAddress}",
            "the caller's address is what turns single entries into a countable scan");
        helper.Should().NotContain("{Identifier}",
            "a month of the values people probed for is a worse leak than the answers that " +
            "disclosed them one at a time");
    }

    private static HashSet<string> RecordedSurfaces() => Directory
        .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src", "DM.Web.API"), "*.cs",
            SearchOption.AllDirectories)
        .Where(RepositoryFiles.IsAuthored)
        .SelectMany(path => Recorded.Matches(File.ReadAllText(path)))
        .Select(match => match.Groups[1].Value)
        .ToHashSet(StringComparer.Ordinal);
}
