using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Two bare writes are two transactions with a nameless state between them. The API host retries on
/// failure, and the retrying strategy refuses a hand-opened transaction. Asserted on the text: the
/// defect is the absence of a call.
/// </remarks>
public class DurableWritesShould
{
    private static readonly string[] Writes =
    [
        "SaveChangesAsync(",
        "ExecuteUpdateAsync(",
        "ExecuteDeleteAsync(",
        // Raw SQL is a write path; paired with SaveChanges it owes the same wrapper.
        "ExecuteSqlRawAsync(",
        "ExecuteSqlInterpolatedAsync(",
        // AttachmentRows.RetireAsync, TokenRows.Retire and SoftDelete.MarkWhere are each one
        // ExecuteUpdateAsync under a name.
        "RetireAsync(",
        "Retire(",
        "MarkWhere(",
    ];

    /// <remarks>The strategy taken by hand, and <c>RetryableWrite.Run</c>, which takes it and clears the tracker.</remarks>
    private static readonly string[] Strategy =
    [
        "CreateExecutionStrategy(",
        "RetryableWrite.Run(",
    ];

    private static readonly string[] Wrapper =
    [
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
        ["AddAsync"] =
            "two branches of one if/else, so a call writes once",
        ["UpdateUser"] =
            "retry on a lost settings-insert race: the second write replays the first " +
            "after the whole first transaction rolled back, it does not follow it",
        ["FlushAsync"] =
            "two single-statement writes where the first either ends the method " +
            "or touched zero rows - there is no half-done state to protect",
    };

    // Four repositories live outside the Repositories folder.
    private static string PersistenceRoot => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence");

    private static readonly string[] NotAuthored = ["obj", "bin", "Migrations"];

    private static IEnumerable<string> PersistenceSources() => Directory
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
        UnderStrategy(member) &&
        Wrapper.All(call => member.Body.Contains(call, StringComparison.Ordinal));

    private static bool UnderStrategy(SourceText.Member member) =>
        Strategy.Any(call => member.Body.Contains(call, StringComparison.Ordinal));

    /// <remarks>
    /// A helper's writes land inside its caller's transaction. A helper nothing in the file calls is
    /// not trusted.
    /// </remarks>
    private static bool CalledOnlyFromWrappedMembers(
        SourceText.Member member, IReadOnlyList<SourceText.Member> file)
    {
        var callers = file
            .Where(other => other.Name != member.Name)
            .Where(other => Regex.IsMatch(other.Body, $@"(?<![.\w]){Regex.Escape(member.Name)}\s*\("))
            .ToList();

        return callers.Count > 0 && callers.TrueForAll(Wrapped);
    }

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
        var files = PersistenceSources().ToList();
        files.Should().NotBeEmpty("the persistence project has a repository per aggregate");

        var members = files.Sum(file => SourceText.Members(SourceText.ReadCode(file)).Count);
        members.Should().BePositive("each of them declares methods");

        var writing = files
            .SelectMany(file => SourceText.Members(SourceText.ReadCode(file)))
            .Count(member => Writes.Any(write => member.Body.Contains(write, StringComparison.Ordinal)));
        writing.Should().BePositive("and some of those write");
    }

    [Fact]
    public void WrapEveryMethodThatWritesMoreThanOnce()
    {
        var offenders = new List<string>();

        foreach (var path in PersistenceSources())
        {
            var members = SourceText.Members(SourceText.ReadCode(path));
            foreach (var member in members)
            {
                if (Deliberate.ContainsKey(member.Name)) continue;
                if (Trips(member, members) < 2) continue;
                if (Wrapped(member)) continue;
                if (CalledOnlyFromWrappedMembers(member, members)) continue;

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

        foreach (var path in PersistenceSources())
        {
            foreach (var member in SourceText.Members(SourceText.ReadCode(path)))
            {
                if (!member.Body.Contains("BeginTransactionAsync(", StringComparison.Ordinal)) continue;
                if (UnderStrategy(member)) continue;

                offenders.Add($"{Path.GetFileName(path)}.{member.Name}");
            }
        }

        offenders.Should().BeEmpty(
            "the retrying strategy the host configures refuses a transaction it did not open, " +
            "so this throws on the first attempt rather than on the first retry");
    }

    // An exemption that outlived its reason is a hole with a comment over it.
    [Fact]
    public void ExemptOnlyWhatStillExists()
    {
        var names = PersistenceSources()
            .SelectMany(path => SourceText.Members(SourceText.ReadCode(path)))
            .Select(member => member.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (name, reason) in Deliberate)
        {
            names.Should().Contain(name, $"the exemption for {name} ({reason}) names a method that is gone");
        }
    }
}
