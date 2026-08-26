using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
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
        // W1.1 made raw SQL a normal write path (ON CONFLICT upserts, the
        // retention sweeper); a method pairing one of these with SaveChanges
        // owes the same wrapper as any other double write.
        "ExecuteSqlRawAsync(",
        "ExecuteSqlInterpolatedAsync(",
    ];

    /// <summary>
    /// The two spellings of "this block runs inside an execution strategy".
    /// </summary>
    /// <remarks>
    /// The strategy taken by hand, and <c>RetryableWrite.Run</c> - which is that
    /// same preamble plus the change-tracker clear, written once instead of the
    /// nineteen times it used to be. Both put the transaction below INSIDE the
    /// strategy, which is the property this rule is about, and the helper does it
    /// by construction rather than by the caller remembering to.
    ///
    /// This is not an exemption: a method still has to open a transaction and
    /// commit it, and it still has to do so under one of these two. What changed
    /// is that the strategy has a name; the rule reads text, so it has to be told
    /// the name.
    /// </remarks>
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
        ["TryRecordDigest"] =
            "compensation on a lost race, asserted by PublishAfterCommitShould",
        ["AddAsync"] =
            "two branches of one if/else, so a call writes once",
        ["UpdateUser"] =
            "retry on a lost settings-insert race: the second write replays the first " +
            "after the whole first transaction rolled back, it does not follow it",
        ["FlushAsync"] =
            "two single-statement writes where the first either ends the method " +
            "or touched zero rows - there is no half-done state to protect",
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
        UnderStrategy(member) &&
        Wrapper.All(call => member.Body.Contains(call, StringComparison.Ordinal));

    private static bool UnderStrategy(SourceText.Member member) =>
        Strategy.Any(call => member.Body.Contains(call, StringComparison.Ordinal));

    /// <summary>
    /// Whether every method of the file that calls this one is wrapped.
    /// </summary>
    /// <remarks>
    /// A helper extracted out of a transaction is not a second transaction: its
    /// writes land inside the one its caller opened, and they roll back with it.
    /// Asked of the callers rather than answered by an entry in the register
    /// above, because the property that makes such a helper safe is exactly this
    /// one, and it stops being true the moment somebody calls it from outside a
    /// transaction - at which point that caller is flagged in its own right and
    /// this helper is flagged with it.
    ///
    /// "At least one caller" is half the answer: a helper nothing in the file
    /// calls is either dead or reached by a route this parser cannot see, and
    /// neither is a reason to trust it.
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

        foreach (var path in RepositoryFiles())
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
