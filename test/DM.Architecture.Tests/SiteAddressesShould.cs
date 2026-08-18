using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The addresses the site answers on are configuration, and configuration is
/// the only place they are written down.
/// </summary>
/// <remarks>
/// A hostname compiled into the source is a link that leads nowhere for
/// whoever cannot reach that host, and the reader who cannot is exactly the
/// reader a second address exists for. It had happened in the places where it
/// costs most: the footer of every notification letter, the approval link of a
/// username change, and six seeded links to contest results.
///
/// The rule is about ABSOLUTE addresses. A relative path is not a defect and is
/// usually the fix: it resolves against whatever host the reader is already on.
/// </remarks>
public class SiteAddressesShould
{
    /// <summary>
    /// The two declarations name the same addresses.
    /// </summary>
    /// <remarks>
    /// Two copies exist for a reason the client states: the moment somebody needs
    /// the other address is the moment the site stopped answering, and a request
    /// cannot be served then. What the reason does not buy is the right to
    /// disagree - one copy carrying an address the other does not is a footer
    /// sending a reader to a host nobody serves, or a letter that forgets the door
    /// that still works.
    /// </remarks>
    [Fact]
    public void DeclareTheSameAddressesOnBothSides()
    {
        var client = Regex
            .Matches(
                File.ReadAllText(Path.Combine(RepositoryRoot,
                    "src", "DM.Web.Client", "src", "shared", "config", "site.ts")),
                @"host:\s*""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .ToList();

        client.Should().HaveCountGreaterOrEqualTo(2,
            "the walk has to find the declaration, and a list of one is a site with a single door");
        client.Should().BeEquivalentTo(DM.Domain.Core.Site.SiteAddresses.Hosts,
            "the server names these addresses in every letter and the client draws them in " +
            "the footer, and the two lists have no way of noticing they disagree");
    }

    /// <summary>
    /// Where an address of the site is allowed to be written down.
    /// </summary>
    /// <remarks>
    /// The server keeps one declaration in code and the client keeps its own, so
    /// that the footer can name the other address with the network down; each is a
    /// declaration rather than a use. Tests name addresses because they assert
    /// about them.
    /// </remarks>
    private static readonly string[] DeclarationFiles =
    {
        Path.Combine("src", "DM.Domain.Core", "Site", "SiteAddresses.cs"),
        Path.Combine("src", "DM.Web.Client", "src", "shared", "config", "site.ts"),
        Path.Combine("src", "DM.Web.Client", "src", "shared", "config", "site.spec.ts")
    };

    private static readonly string[] SearchedExtensions =
    {
        ".cs", ".razor", ".vue", ".ts", ".json", ".yml", ".yaml"
    };

    /// <summary>
    /// An absolute address under the domain the site lives on. Deliberately
    /// broad: the point is that no host of this site is named in code, not that
    /// one particular host is not.
    /// </summary>
    private static readonly Regex SiteAddress =
        new(@"https?://[A-Za-z0-9.-]*\bdm\.am\b", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IEnumerable<string> SourceFiles()
    {
        var source = Path.Combine(RepositoryRoot, "src");
        return Directory
            .EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(path => SearchedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void BeWrittenDownOnlyWhereTheyAreDeclared()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            var relative = Path.GetRelativePath(RepositoryRoot, file);
            if (DeclarationFiles.Any(declaration =>
                    relative.Equals(declaration, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var found = SiteAddress.Match(lines[i]);
                if (found.Success)
                {
                    offenders.Add($"{relative}:{i + 1}: {found.Value}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "an address in the source is a link that fails for everyone who cannot reach that host, " +
            "and a relative path costs nothing and follows the reader");
    }

    [Fact]
    public void HaveASearchThatWouldFindOne()
    {
        // The assertion above is a search over a file tree, and a search that
        // stops matching passes silently. This is the fixture that says it
        // still matches what it is looking for.
        SiteAddress.IsMatch("https://dm.am/forum/topic/1").Should().BeTrue();
        SiteAddress.IsMatch("https://ru.l.dm.am").Should().BeTrue();
        SiteAddress.IsMatch("http://api.dm.am/v1").Should().BeTrue();
        SiteAddress.IsMatch("/forum/topic/1").Should().BeFalse("a relative path is the fix, not the defect");
        SiteAddress.IsMatch("dm.amsterdam").Should().BeFalse();
    }

    [Fact]
    public void FindTheFilesToSearch()
    {
        SourceFiles().Should().HaveCountGreaterThan(100,
            "a file walk that stops matching turns the rule above green by checking nothing");
    }

    /// <summary>
    /// The session cookie is written twice, once to hand it out and once to
    /// clear it, and a browser replaces a cookie only when the incoming
    /// attributes match the stored ones. Domain is one of those attributes, so
    /// the two halves diverging leaves a session that logging out cannot clear.
    /// </summary>
    [Fact]
    public void WriteTheSessionCookieAttributesInOnePlace()
    {
        var storage = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "DM.Web.API", "Shared", "Authentication", "ApiCredentialsStorage.cs"));

        Regex.Matches(storage, @"new CookieOptions|CookieOptions\s*\(\s*\)|CookieOptions\s*\{")
            .Count.Should().BeLessOrEqualTo(1,
                "every write of the cookie takes its attributes from the one helper");

        Regex.Matches(storage, @"SessionCookieOptions\(").Count.Should().BeGreaterOrEqualTo(3,
            "the helper is declared once and used by both the handing out and the clearing");

        storage.Should().Contain("SessionCookieConfiguration",
            "the scope of the cookie is deployment configuration, not a constant in the source");
    }
}
