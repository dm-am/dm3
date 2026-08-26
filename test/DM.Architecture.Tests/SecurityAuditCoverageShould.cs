using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Account.Features.Security;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every security event type the journal offers to read is written by somebody.
/// </summary>
/// <remarks>
/// The journal is read through filters built out of this enum, so a type nobody
/// writes is not a gap in a log file. It is a screen answering "nothing
/// happened" to "was my password changed while I was away", which is the single
/// question the journal exists for. Five of the eleven were in that state and
/// nothing said so: the enum compiled, the filter compiled, the page rendered,
/// empty.
///
/// Source text is the surface because the writes are call sites and not types.
/// A reflection rule would have to run the application to see them.
/// </remarks>
public class SecurityAuditCoverageShould
{
    /// <summary>
    /// A call naming the type at the argument the journal stores. Deliberately
    /// not "the name appears in the file": the read filters list the very same
    /// three password types, and matching those would report the exact defect
    /// this exists to catch as covered.
    /// </summary>
    private static readonly Regex WriteSite = new(
        @"LogAsync\(\s*[^,;()]+,\s*SecurityEventType\.(\w+)",
        RegexOptions.Compiled);

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    [Fact]
    public void WriteEverySecurityEventTypeItOffersToRead()
    {
        var separator = Path.DirectorySeparatorChar;
        var written = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}")
                           && !path.Contains($"{separator}bin{separator}"))
            .SelectMany(path => WriteSite.Matches(File.ReadAllText(path))
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        written.Should().Contain(Enum.GetNames<SecurityEventType>(),
            "a security event type the journal filters by has to have a writer");
    }
}
