using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The document store gets the connection ceiling written on the line above it.
/// </summary>
/// <remarks>
/// Both connection strings are written on neighbouring lines in four files - the
/// compose anchor five services share, and the Development overlay of every host
/// that opens both stores - and the two disagreed by a factor of ten:
/// maxPoolSize=1000 against MaxPoolSize=100, with nothing anywhere saying why.
/// The numbers cap the same thing, the connections one process holds at once, and
/// the document driver returns a connection after each operation where Npgsql
/// holds one for a whole unit of work, so the wider ceiling was never the one that
/// could bite. What it did instead was hide which of the two is the limit.
///
/// Making them agree is one edit per file; keeping them in agreement is what needs
/// a rule, because the number is written out four times, no compiler reads any of
/// them, and the copy that falls behind fails nothing. So the rule is not "the
/// ceiling is a hundred" - that would be a fifth copy of the policy, and the first
/// change of the relational line would leave it stale - but "the document ceiling
/// of a file equals the relational ceiling of the same file".
///
/// Asserted on the text: a connection string is read by the driver it is handed
/// to, and three of the four files are never loaded by anything under test.
/// </remarks>
public class ConnectionPoolCeilingShould
{
    /// <summary>A document-store connection string, up to the first space or quote.</summary>
    private static readonly Regex DocumentConnection = new(
        @"mongodb(?:\+srv)?://[^\s""']+", RegexOptions.Compiled);

    /// <summary>
    /// The pool ceiling of a connection string. Case-insensitive on purpose: the
    /// two drivers spell the option differently and mean the same policy.
    /// </summary>
    private static readonly Regex PoolCeiling = new(
        @"maxpoolsize\s*=\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    [Fact]
    public void FindTheFilesItReads() =>
        FilesThatOpenTheDocumentStore().Should().HaveCountGreaterThan(3,
            "the compose anchor and the Development overlay of every host that opens both " +
            "stores carry the pair, and a walk that finds none passes on anything");

    [Fact]
    public void SpellTheCeilingOutInEveryStringThatOpensTheDocumentStore() =>
        FilesThatOpenTheDocumentStore()
            .SelectMany(file => DocumentConnection.Matches(File.ReadAllText(file))
                .Where(connection => !PoolCeiling.IsMatch(connection.Value))
                .Select(_ => Relative(file)))
            .Should().BeEmpty(
                "a string that leaves the option out runs on whatever the driver defaults to " +
                "and stops following the line above it, without failing anything");

    [Fact]
    public void HoldEveryDocumentCeilingToTheRelationalCeilingBesideIt()
    {
        var offenders = new List<string>();

        foreach (var file in FilesThatOpenTheDocumentStore())
        {
            var text = File.ReadAllText(file);
            var relational = Ceilings(DocumentConnection.Replace(text, " ")).Distinct().ToArray();

            if (relational.Length != 1)
            {
                offenders.Add(
                    $"{Relative(file)}: {relational.Length} relational ceilings, and the rule " +
                    "holds the document one to the single number written beside it");
                continue;
            }

            offenders.AddRange(DocumentConnection.Matches(text)
                .SelectMany(connection => Ceilings(connection.Value))
                .Where(ceiling => ceiling != relational[0])
                .Select(ceiling =>
                    $"{Relative(file)}: maxPoolSize={ceiling} beside MaxPoolSize={relational[0]}"));
        }

        offenders.Should().BeEmpty(
            "both numbers cap the connections one process holds at once, and a document " +
            "ceiling that walks away from the relational one beside it is a limit nobody " +
            "chose, copied into four files that nothing compiles");
    }

    /// <summary>
    /// The rule is two expressions over text, and an extraction that stops matching
    /// turns it green by reading nothing.
    /// </summary>
    [Fact]
    public void ReadBothSpellingsAndKeepThemApart()
    {
        // The two shapes the tree has: a compose value carrying substitutions and no
        // quotes, and a JSON pair. The numbers are deliberately not the ones in the
        // tree, so this stays a test of the extraction and not a copy of the policy.
        const string compose =
            "  DM_ConnectionStrings__Rdb: Host=${DB_HOST:-dm-pg};Pooling=true;MinPoolSize=0;MaxPoolSize=9;\n" +
            "  DM_ConnectionStrings__Mongo: mongodb://${MONGO_USER:-dm}:${MONGO_PASSWORD}@dm-mongo:27017/dm3?authSource=dm3&maxPoolSize=7${MONGO_TLS:-}\n" +
            "  DM_ConnectionStrings__Logs: http://dm-loki:3100";

        var connection = DocumentConnection.Match(compose);

        connection.Success.Should().BeTrue("the compose value is one unquoted word");
        Ceilings(connection.Value).Single().Should().Be(7,
            "the ceiling inside the URI is the document one, substitutions and all");
        Ceilings(DocumentConnection.Replace(compose, " ")).Single().Should().Be(9,
            "the relational ceiling is what is left once the URI is taken out; reading both " +
            "into one bag compares a number with itself and passes on any disagreement");

        const string overlay =
            "    \"Rdb\": \"Pooling=true;MinPoolSize=0;MaxPoolSize=9;\",\n" +
            "    \"Mongo\": \"mongodb://localhost:27017/dm3?maxPoolSize=7\"";

        DocumentConnection.Match(overlay).Value.Should().EndWith("maxPoolSize=7",
            "the JSON string ends at the quote, and a match that swallows it takes the " +
            "relational ceiling of the next line with it");
    }

    /// <summary>Deployment files that hand a connection string to the document driver.</summary>
    private static IReadOnlyList<string> FilesThatOpenTheDocumentStore() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "docker"), "docker-compose*.yml")
        .Concat(Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(IsAuthored))
        .Where(path => DocumentConnection.IsMatch(File.ReadAllText(path)))
        .ToArray();

    /// <summary>The build output carries stale copies of every settings file.</summary>
    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin");

    private static IEnumerable<int> Ceilings(string text) => PoolCeiling
        .Matches(text)
        .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path);
}
