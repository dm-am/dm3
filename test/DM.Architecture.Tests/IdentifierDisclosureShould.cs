using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every answer that tells a caller whether an identifier is registered is both
/// written down as a decision and recorded when it is given.
/// </summary>
/// <remarks>
/// Disclosing existence is a recorded trade, not an oversight: a registration
/// form has to say an address is taken, and a recovery form has to distinguish a
/// typo from a letter that was sent. What the trade costs is that a bulk scan of
/// those surfaces is possible, and what makes it acceptable is that a scan leaves
/// a trace — one entry per disclosing answer, countable per client address.
///
/// Both halves are asserted against each other, in both directions, because
/// either one alone is worthless. A surface that discloses and does not record
/// makes the whole recorded exception a paragraph describing something that is
/// not true. A table entry with no surface behind it says the site discloses
/// something it no longer does, and the reader budgets a risk that is gone.
///
/// Nothing fails when either drifts: the endpoint answers, the document reads
/// well, and the difference shows up only to somebody reading both at once with
/// the question already in mind.
/// </remarks>
public class IdentifierDisclosureShould
{
    /// <summary>The convention document that records the exception.</summary>
    private static readonly string Convention =
        Path.Combine("docs", "conventions", "SECURITY.md");

    /// <summary>A surface of the recorded table, by the route it answers on.</summary>
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
            .Matches(File.ReadAllText(Path.Combine(RepositoryRoot, Convention)))
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

    /// <summary>
    /// The identifier that was probed stays out of the entry.
    /// </summary>
    /// <remarks>
    /// Entries live for a month and are read through a dashboard, so writing the
    /// value would leave a month of exactly the addresses and names every scan
    /// went looking for — the record of the abuse becoming a better list than the
    /// abuse produced. Scale and source are recoverable from the event and the
    /// caller's address without it.
    /// </remarks>
    [Fact]
    public void KeepTheProbedIdentifierOutOfTheEntry()
    {
        var helper = File.ReadAllText(Path.Combine(RepositoryRoot,
            "src", "DM.Web.API", "Features", "Account", "IdentifierProbeLog.cs"));

        helper.Should().Contain("{ClientAddress}",
            "the caller's address is what turns single entries into a countable scan");
        helper.Should().NotContain("{Identifier}",
            "a month of the values people probed for is a worse leak than the answers that " +
            "disclosed them one at a time");
    }

    private static HashSet<string> RecordedSurfaces() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "DM.Web.API"), "*.cs",
            SearchOption.AllDirectories)
        .Where(IsAuthored)
        .SelectMany(path => Recorded.Matches(File.ReadAllText(path)))
        .Select(match => match.Groups[1].Value)
        .ToHashSet(StringComparer.Ordinal);

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin");

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
