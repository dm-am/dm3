using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A repository method that writes twice writes both times or neither.
/// </summary>
/// <remarks>
/// Two writes with nothing around them are two transactions, and the gap between
/// them is a state the code has no name for: a password hash changed with the
/// reset token that authorised it still live, a rename recorded in history against
/// a user still carrying the old name, an invitation spent with nothing granted for
/// it. None of these is reachable by an ordinary action — it takes the database
/// refusing, or the process dying, in the gap — and none of them is repaired by
/// anything afterwards.
///
/// The wrapper is not optional and not a style choice. The API host configures
/// EnableRetryOnFailure, and a retrying execution strategy refuses a transaction
/// opened by hand: it has no way to replay one. So the two halves come together
/// or not at all, which is why this rule asks for all three spellings rather than
/// for the transaction alone.
///
/// Asserted on the text, because the defect is the absence of a call. A test that
/// exercised these methods would pass either way — both writes land when nothing
/// goes wrong, and what goes wrong here is the database, in a window of
/// microseconds, on a machine nobody is watching.
/// </remarks>
public class DurableWritesShould
{
    /// <summary>Calls that reach the database and cannot be taken back afterwards.</summary>
    private static readonly string[] Writes =
    [
        "SaveChangesAsync(",
        "ExecuteUpdateAsync(",
        "ExecuteDeleteAsync(",
    ];

    private static readonly string[] Wrapper =
    [
        "CreateExecutionStrategy(",
        "BeginTransactionAsync(",
        "CommitAsync(",
    ];

    /// <summary>
    /// Methods that write twice on purpose, with the reason stated where they are
    /// written. Listed rather than pattern-matched, because what makes them safe is
    /// what they mean.
    /// </summary>
    private static readonly Dictionary<string, string> Deliberate = new(StringComparer.Ordinal)
    {
        ["TryRecordDigest"] =
            "compensation on a lost race, asserted by PublishAfterCommitShould",
        ["AddAsync"] =
            "two branches of one if/else, so a call writes once",
    };

    /// <summary>
    /// The whole persistence project, and not the Repositories folder of it.
    /// </summary>
    /// <remarks>
    /// Four repositories live outside that folder — likes, notepads,
    /// subscriptions and the unread counters — and a rule about repositories that
    /// stops at a directory name is one a new folder walks out of. None of them
    /// writes twice in a method today, so the reach costs nothing and holds the
    /// day one of them does.
    /// </remarks>
    private static string PersistenceRoot => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence");

    /// <summary>Directories with nothing anybody wrote by hand in them.</summary>
    private static readonly string[] NotAuthored = ["obj", "bin", "Migrations"];

    private static IEnumerable<string> RepositoryFiles() => Directory
        .EnumerateFiles(PersistenceRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => NotAuthored.Contains(segment, StringComparer.Ordinal)));

    /// <summary>
    /// How many separate trips to the database a body makes, counting the bodies of
    /// its own file's methods that it calls unqualified. A helper that is itself
    /// wrapped counts as one: it is already all-or-nothing.
    /// </summary>
    private static int Trips(SourceText.Member member, IReadOnlyList<SourceText.Member> file, int depth = 0)
    {
        var own = Writes.Sum(write => Occurrences(member.Body, write));
        if (depth > 2)
        {
            return own;
        }

        foreach (var callee in file.Where(other => other.Name != member.Name))
        {
            if (!Regex.IsMatch(member.Body, $@"(?<![.\w]){Regex.Escape(callee.Name)}\s*\(")) continue;

            own += Wrapped(callee) ? 1 : Trips(callee, file, depth + 1);
        }

        return own;
    }

    private static bool Wrapped(SourceText.Member member) =>
        Wrapper.All(call => member.Body.Contains(call, StringComparison.Ordinal));

    private static int Occurrences(string body, string call)
    {
        var count = 0;
        for (var at = body.IndexOf(call, StringComparison.Ordinal); at >= 0;
             at = body.IndexOf(call, at + call.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// A rule over an empty set passes, and both the walk and the parser are the
    /// kind of thing that stops matching quietly.
    /// </summary>
    [Fact]
    public void FindTheRepositoriesAndTheirMethods()
    {
        var files = RepositoryFiles().ToList();
        files.Should().HaveCountGreaterThan(30, "the persistence project has a repository per aggregate");

        var members = files.Sum(file => SourceText.Members(SourceText.ReadCode(file)).Count);
        members.Should().BeGreaterThan(300, "each of them declares a good many methods");

        var writing = files
            .SelectMany(file => SourceText.Members(SourceText.ReadCode(file)))
            .Count(member => Writes.Any(write => member.Body.Contains(write, StringComparison.Ordinal)));
        writing.Should().BeGreaterThan(50, "and a good many of those write");
    }

    [Fact]
    public void WrapEveryMethodThatWritesMoreThanOnce()
    {
        var offenders = new List<string>();

        foreach (var path in RepositoryFiles())
        {
            var members = SourceText.Members(SourceText.ReadCode(path));
            foreach (var member in members)
            {
                if (Deliberate.ContainsKey(member.Name)) continue;
                if (Trips(member, members) < 2) continue;
                if (Wrapped(member)) continue;

                offenders.Add($"{Path.GetFileName(path)}.{member.Name}");
            }
        }

        offenders.Should().BeEmpty(
            "each of these writes twice without a transaction, so a refusal between the two " +
            "leaves a state nothing else repairs");
    }

    /// <summary>
    /// The other half of the same rule: a transaction outside an execution strategy
    /// is refused at run time by the retrying provider the host configures.
    /// </summary>
    [Fact]
    public void OpenNoTransactionOutsideAnExecutionStrategy()
    {
        var offenders = new List<string>();

        foreach (var path in RepositoryFiles())
        {
            foreach (var member in SourceText.Members(SourceText.ReadCode(path)))
            {
                if (!member.Body.Contains("BeginTransactionAsync(", StringComparison.Ordinal)) continue;
                if (member.Body.Contains("CreateExecutionStrategy(", StringComparison.Ordinal)) continue;

                offenders.Add($"{Path.GetFileName(path)}.{member.Name}");
            }
        }

        offenders.Should().BeEmpty(
            "the retrying strategy the host configures refuses a transaction it did not open, " +
            "so this throws on the first attempt rather than on the first retry");
    }

    /// <summary>
    /// An exemption that outlived its reason is a hole with a comment over it.
    /// </summary>
    [Fact]
    public void ExemptOnlyWhatStillExists()
    {
        var names = RepositoryFiles()
            .SelectMany(path => SourceText.Members(SourceText.ReadCode(path)))
            .Select(member => member.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (name, reason) in Deliberate)
        {
            names.Should().Contain(name, $"the exemption for {name} ({reason}) names a method that is gone");
        }
    }
}
