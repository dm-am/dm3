using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A cache key spelled in more than one place is spelled in CacheKeys.
/// </summary>
/// <remarks>
/// A misspelled key fails silently, as a stale read or a cache that never hits. Keys used inside
/// a single file stay there.
/// </remarks>
public class CacheKeyOwnershipShould
{
    /// <summary>
    /// An interpolated key literal: a lowercase prefix ending in an underscore,
    /// then a hole. Cache keys in this codebase are all of this shape.
    /// </summary>
    private static readonly Regex KeyLiteral = new(@"\$""(?<prefix>[a-z][a-z0-9]*(?:_[a-z0-9]+)*_)\{", RegexOptions.Compiled);

    [Fact]
    public void SpellEveryCacheKeyInExactlyOnePlace()
    {
        var owners = new Dictionary<string, HashSet<string>>();

        foreach (var file in RepositoryFiles.ProductionSources())
        {

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

        // Every prefix in exactly one file, CacheKeys included.
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
