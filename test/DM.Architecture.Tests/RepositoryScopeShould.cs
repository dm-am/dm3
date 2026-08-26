using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A repository method that asks who is reading has to use the answer.
/// </summary>
/// <remarks>
/// PostRepository.Get(Guid postId, Guid userId) took the reader's identifier and
/// never mentioned it again: the query was Posts.Where(p => p.PostId == postId),
/// so the text of any post whose identifier a caller happened to know came back,
/// private room or not. The method two lines above it does the same read through
/// the rooms and applies GameAccessibilityFilters.RoomAvailable(userId).
///
/// Nothing said a word. The compiler has no reason to: an unused parameter is
/// legal. The signature promised a scoped read, the call sites believed it, and
/// the only place the promise could be checked was the body nobody re-read.
///
/// So the rule is narrow and mechanical: a parameter that names the reader must
/// appear in the body that declares it. It does not say which filter to use, and
/// it cannot: whether the scope is correct is a question for a test over data.
/// What it does say is that the argument was not silently dropped, which is the
/// shape this defect had both times it happened.
/// </remarks>
public class RepositoryScopeShould
{
    /// <summary>Parameter names that mean "the person this read is for".</summary>
    private static readonly string[] ReaderParameters = { "userId", "viewerId", "readerId", "currentUserId" };

    /// <summary>
    /// Signature followed by a body or an expression body. The capture keeps the
    /// parameter list so the reader parameter can be recognised, and the position
    /// of the match is where the body extraction starts.
    /// </summary>
    private static readonly Regex Method = new(
        @"(?<sig>(?:public|private|protected|internal)[^;{}()]*?\s(?<name>\w+)\s*\((?<args>[^)]*)\))\s*(?<open>=>|\{)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static string RepositoriesRoot => Path.Combine(
        RepositoryRoot, "src", "DM.Infrastructure.Persistence", "Repositories");

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IEnumerable<string> RepositoryFiles() => Directory
        .EnumerateFiles(RepositoriesRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// The body that follows a signature: either everything up to the matching
    /// closing brace, or, for an expression body, up to the semicolon that ends
    /// the statement.
    /// </summary>
    private static string BodyAfter(string source, Match method)
    {
        var start = method.Index + method.Length;
        if (method.Groups["open"].Value == "=>")
        {
            var end = source.IndexOf(';', start);
            return end < 0 ? source[start..] : source[start..end];
        }

        var depth = 1;
        for (var i = start; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return source[start..i];
            }
        }

        return source[start..];
    }

    [Fact]
    public void UseTheReaderItAsksFor()
    {
        var offenders = new List<string>();

        foreach (var file in RepositoryFiles())
        {
            var source = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepositoryRoot, file);

            foreach (Match method in Method.Matches(source))
            {
                var parameters = method.Groups["args"].Value;
                var reader = ReaderParameters.FirstOrDefault(name =>
                    Regex.IsMatch(parameters, $@"\bGuid\??\s+{name}\b"));
                if (reader is null)
                {
                    continue;
                }

                var body = BodyAfter(source, method);
                if (Regex.IsMatch(body, $@"\b{reader}\b"))
                {
                    continue;
                }

                var line = source[..method.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{relative}:{line}: {method.Groups["name"].Value} takes {reader} and never uses it");
            }
        }

        offenders.Should().BeEmpty(
            "a dropped reader argument turns a scoped read into an unscoped one, and the signature " +
            "keeps promising otherwise to every call site");
    }

    [Fact]
    public void HaveASearchThatWouldFindOne()
    {
        // The rule above is a scan, and a scan that stops matching passes in
        // silence. This is the fixture that says it still recognises both the
        // shape it looks for and the shape it must leave alone.
        const string dropped = @"
public async Task<Post?> Get(Guid postId, Guid userId)
{
    return await _dbContext.Posts.Where(p => p.PostId == postId).FirstOrDefaultAsync();
}";
        const string used = @"
public async Task<Post?> Get(Guid postId, Guid userId)
{
    return await _dbContext.Rooms.Where(RoomAvailable(userId)).FirstOrDefaultAsync();
}";

        Offends(dropped).Should().BeTrue("this is the defect the rule exists for");
        Offends(used).Should().BeFalse("a body that mentions the reader is out of scope for this rule");
    }

    private static bool Offends(string source)
    {
        foreach (Match method in Method.Matches(source))
        {
            var reader = ReaderParameters.FirstOrDefault(name =>
                Regex.IsMatch(method.Groups["args"].Value, $@"\bGuid\??\s+{name}\b"));
            if (reader is null)
            {
                continue;
            }

            if (!Regex.IsMatch(BodyAfter(source, method), $@"\b{reader}\b"))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void FindTheFilesToSearch() =>
        RepositoryFiles().Should().HaveCountGreaterThan(20,
            "a file walk that stops matching turns the rule above green by checking nothing");
}
