using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The addresses the site answers on are configuration, and configuration is
/// the only place they are written down.
/// </summary>
/// <remarks>A compiled hostname fails exactly the reader a second address exists for. Relative paths are fine.</remarks>
public class SiteAddressesShould
{
    /// <summary>
    /// The two declarations name the same addresses.
    /// </summary>
    /// <remarks>The client keeps its copy for when the site stops answering; the two may not disagree.</remarks>
    [Fact]
    public void DeclareTheSameAddressesOnBothSides()
    {
        var client = Regex
            .Matches(
                File.ReadAllText(Path.Combine(RepositoryLayout.Root,
                    "src", "DM.Web.Client", "src", "shared", "config", "site.ts")),
                @"host:\s*""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .ToList();

        client.Should().HaveCountGreaterThanOrEqualTo(2,
            "the walk has to find the declaration, and a list of one is a site with a single door");
        client.Should().BeEquivalentTo(DM.Domain.Core.Site.SiteAddresses.Hosts,
            "the server names these addresses in every letter and the client draws them in " +
            "the footer, and the two lists have no way of noticing they disagree");

        ClientField("name").Should().Equal(
            DM.Domain.Core.Site.SiteAddresses.All.Select(address => address.Name),
            "the letterhead calls each address the way the sidebar calls it, and a reader " +
            "who meets one name in one place and another in the other has no way of " +
            "telling they are the same door");

        ClientField("title").Should().Equal(
            DM.Domain.Core.Site.SiteAddresses.All.Select(address => address.Title),
            "the letter and the failure toast name the address in a sentence by the same " +
            "full name, \"Основной сервер\" and not \"Основной\"");
    }

    /// <summary>The values of one field of every address of the client's list, in its order.</summary>
    private static List<string> ClientField(string field) => Regex
        .Matches(
            File.ReadAllText(Path.Combine(RepositoryLayout.Root,
                "src", "DM.Web.Client", "src", "shared", "config", "site.ts")),
            @"\b" + field + @":\s*""([^""]+)""")
        .Select(match => match.Groups[1].Value)
        .ToList();

    /// <summary>
    /// The rows of the sidebar block are named by the address itself.
    /// </summary>
    /// <remarks>
    /// The block is headed "Серверы сайта", so the canonical row says "Основной" and does not repeat the word.
    /// A sentence has no heading over it and says the word.
    /// </remarks>
    [Fact]
    public void NameTheAddressesWithoutRepeatingTheWordOfTheBlock()
    {
        DM.Domain.Core.Site.SiteAddresses.All.Select(address => address.Name)
            .Should().Equal("Основной", "RU-прокси");
        DM.Domain.Core.Site.SiteAddresses.All.Select(address => address.Title)
            .Should().Equal("Основной сервер", "RU-прокси");
    }

    /// <summary>
    /// Where an address of the site is allowed to be written down.
    /// </summary>
    /// <remarks>Each declaration is not a use; tests name addresses because they assert about them.</remarks>
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
    /// An absolute address under the site's domain, deliberately broad.
    /// </summary>
    private static readonly Regex SiteAddress =
        new(@"https?://[A-Za-z0-9.-]*\bdm\.am\b", RegexOptions.Compiled);


    private static IEnumerable<string> SourceFiles()
    {
        var source = Path.Combine(RepositoryLayout.Root, "src");
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
            var relative = Path.GetRelativePath(RepositoryLayout.Root, file);
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
        // The rule above is a search over a file tree, and a search that stops matching passes in silence.
        SiteAddress.IsMatch("https://dm.am/forum/topic/1").Should().BeTrue();
        SiteAddress.IsMatch("https://ru.l.dm.am").Should().BeTrue();
        SiteAddress.IsMatch("http://api.dm.am/v1").Should().BeTrue();
        SiteAddress.IsMatch("/forum/topic/1").Should().BeFalse("a relative path is the fix, not the defect");
        SiteAddress.IsMatch("dm.amsterdam").Should().BeFalse();
    }

    [Fact]
    public void FindTheFilesToSearch() =>
        SourceFiles().Should().HaveCountGreaterThan(100,
            "a file walk that stops matching turns the rule above green by checking nothing");

    /// <summary>
    /// The session cookie is written to hand out and to clear, and a browser replaces it only when the
    /// attributes, Domain included, match.
    /// </summary>
    [Fact]
    public void WriteTheSessionCookieAttributesInOnePlace()
    {
        var storage = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", "DM.Web.API", "Shared", "Authentication", "ApiCredentialsStorage.cs"));

        Regex.Matches(storage, @"new CookieOptions|CookieOptions\s*\(\s*\)|CookieOptions\s*\{")
            .Count.Should().BeLessThanOrEqualTo(1,
                "every write of the cookie takes its attributes from the one helper");

        Regex.Matches(storage, @"SessionCookieOptions\(").Count.Should().BeGreaterThanOrEqualTo(3,
            "the helper is declared once and used by both the handing out and the clearing");

        storage.Should().Contain("SessionCookieConfiguration",
            "the scope of the cookie is deployment configuration, not a constant in the source");
    }
}
