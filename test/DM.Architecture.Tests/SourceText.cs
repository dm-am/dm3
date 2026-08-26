using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace DM.Architecture.Tests;

/// <summary>
/// Source read as text, with the comments taken out.
/// </summary>
/// <remarks>
/// This tier asserts properties that belong to the code and would otherwise need
/// the whole stack up to observe — the order of the middleware, the absence of a
/// machine-clock read, the determinism of the seed. Reading the file is the only
/// way to get at them, and a file is not code: a paragraph explaining why a call
/// is absent contains the call, and two slashes in front of a line neither move
/// it nor remove the literal on it.
///
/// The order gate is where that stopped being a detail. It compares the
/// positions of three calls in Startup.cs, and a commented-out pipeline
/// satisfies every one of those comparisons — a green build over an application
/// whose per-account rate limits had silently become per-address ones, which is
/// exactly what its own remarks say nobody would notice otherwise.
///
/// One spelling, in one place, because five copies of it existed and the sixth
/// check copied none of them. It lives in this project rather than in DM.Testing
/// on purpose: a text helper has no reason to depend on NSubstitute and EF, and a
/// reference to that project would put NSubstitute in the output directory the ArchUnit
/// loader scans. It moves to a shared home when a consumer outside this tier
/// appears, rather than being copied to it.
/// </remarks>
internal static class SourceText
{
    private static readonly Regex Comments = new(
        @"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// The file's code, with block and line comments blanked out.
    /// </summary>
    /// <remarks>
    /// Replaced with nothing rather than with spaces: every consumer either
    /// searches for a substring or compares relative positions, and removal
    /// shifts all of those equally.
    /// </remarks>
    /// <param name="path">Absolute path of the source file.</param>
    internal static string ReadCode(string path) =>
        Comments.Replace(File.ReadAllText(path), string.Empty);

    /// <summary>
    /// The slice from one marker up to the next, the opening marker included.
    /// </summary>
    /// <remarks>
    /// Both markers are asserted rather than answered around, because the rules
    /// that ask for a slice are asking about what is inside it: a marker that has
    /// been renamed away leaves an empty slice, and an empty slice contains
    /// nothing to complain about.
    /// </remarks>
    /// <param name="source">Text to slice</param>
    /// <param name="start">Marker the slice opens at</param>
    /// <param name="end">Marker the slice stops before</param>
    internal static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"the source must still declare {start}");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        to.Should().BeGreaterThan(-1, $"the declaration of {start} must be terminated");
        return source[from..to];
    }

    /// <summary>
    /// Signature followed by a body or an expression body. The capture keeps the
    /// parameter list, and the position of the match is where the body starts.
    /// </summary>
    private static readonly Regex Signature = new(
        @"(?<sig>(?:public|private|protected|internal)[^;{}()]*?\s(?<name>\w+)\s*\((?<args>[^)]*)\))\s*(?<open>=>|\{)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>One method of a type: what it is called, what it takes, what it does.</summary>
    internal readonly record struct Member(string Name, string Parameters, string Body);

    /// <summary>
    /// The methods a file declares, each with its body.
    /// </summary>
    /// <remarks>
    /// One parser, because two of them existed and the second was written by
    /// copying the first. Brace counting rather than a syntax tree: the rules that
    /// read this ask where a call sits relative to another call, which survives
    /// being approximate, and a parser dependency in this tier would put a compiler
    /// in the directory the ArchUnit loader scans.
    /// </remarks>
    internal static IReadOnlyList<Member> Members(string code)
    {
        var members = new List<Member>();
        foreach (Match match in Signature.Matches(code))
        {
            members.Add(new Member(
                match.Groups["name"].Value,
                match.Groups["args"].Value,
                BodyAfter(code, match)));
        }

        return members;
    }

    /// <summary>
    /// Everything after a signature: up to the matching closing brace, or, for an
    /// expression body, up to the semicolon that ends the statement.
    /// </summary>
    private static string BodyAfter(string code, Match signature)
    {
        var start = signature.Index + signature.Length;
        if (signature.Groups["open"].Value == "=>")
        {
            var end = code.IndexOf(';', start);
            return end < 0 ? code[start..] : code[start..end];
        }

        var depth = 1;
        for (var i = start; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return code[start..i];
            }
        }

        return code[start..];
    }
}
