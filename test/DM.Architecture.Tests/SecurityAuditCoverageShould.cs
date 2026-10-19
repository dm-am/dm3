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
/// <remarks>A type nobody writes answers "nothing happened" on the journal screen. Source text: the writes are call sites.</remarks>
public class SecurityAuditCoverageShould
{
    /// <summary>
    /// A call naming the type at the argument the journal stores: the read filters name the same types.
    /// </summary>
    private static readonly Regex WriteSite = new(
        @"LogAsync\(\s*[^,;()]+,\s*SecurityEventType\.(\w+)",
        RegexOptions.Compiled);


    [Fact]
    public void WriteEverySecurityEventTypeItOffersToRead()
    {
        var written = RepositoryFiles.ProductionSources()
            .SelectMany(path => WriteSite.Matches(File.ReadAllText(path))
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        written.Should().Contain(Enum.GetNames<SecurityEventType>(),
            "a security event type the journal filters by has to have a writer");
    }
}
