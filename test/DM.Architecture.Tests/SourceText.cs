using System.IO;
using System.Text.RegularExpressions;

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
/// on purpose: a text helper has no reason to depend on Moq and EF, and a
/// reference to that project would put Moq in the output directory the ArchUnit
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
}
