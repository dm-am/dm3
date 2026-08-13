using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A cache key spelled in more than one place is spelled in CacheKeys.
/// </summary>
/// <remarks>
/// The profile is cached under two keys, by name and by identifier, and is
/// written and invalidated from three modules. Spelled as literals, they drifted:
/// removing an avatar invalidated user_{Username}, which nothing had ever
/// written, and left the by-name entry alone — so the profile page kept serving
/// the deleted avatar for the whole minute of its TTL. The staff lists were a
/// second copy of the same shape in two modules.
///
/// Both directions of a misspelled key fail silently: a stale read, or a cache
/// that never hits. Nothing throws, no test goes red, and the report is a user
/// saying the site "remembers" something they deleted. That is what makes this a
/// rule rather than a matter of care.
///
/// Keys used inside a single file stay there — a name nobody else says cannot
/// drift away from anything.
/// </remarks>
public class CacheKeyOwnershipShould
{
    /// <summary>
    /// An interpolated key literal: a lowercase prefix ending in an underscore,
    /// then a hole. Cache keys in this codebase are all of this shape.
    /// </summary>
    private static readonly Regex KeyLiteral = new(@"\$""(?<prefix>[a-z][a-z0-9]*(?:_[a-z0-9]+)*_)\{", RegexOptions.Compiled);

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DM.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("DM.sln not found above the test binaries"),
            "src");
    }

    [Fact]
    public void SpellEveryCacheKeyInExactlyOnePlace()
    {
        var owners = new Dictionary<string, HashSet<string>>();

        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            foreach (Match match in KeyLiteral.Matches(File.ReadAllText(file)))
            {
                var prefix = match.Groups["prefix"].Value;
                if (!owners.TryGetValue(prefix, out var files))
                {
                    files = [];
                    owners[prefix] = files;
                }

                files.Add(Path.GetFileName(file));
            }
        }

        owners.Should().NotBeEmpty("the codebase caches by interpolated keys");

        // Every prefix in exactly one file, CacheKeys included: a key that lives
        // there and is also spelled out at a call site is the same drift, only
        // with a working spelling standing right next to the broken one.
        var shared = owners
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key} -> {string.Join(", ", pair.Value.OrderBy(name => name))}")
            .OrderBy(line => line)
            .ToArray();

        shared.Should().BeEmpty(
            "a key two files spell apart is a key one of them will get wrong, and both " +
            "halves of that mistake are silent");
    }
}
