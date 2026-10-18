using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DM.Testing.Markup;

/// <summary>
/// Runs a check over generated texts, and when one fails, makes it as small as
/// it can and reports that one.
/// </summary>
/// <remarks>
/// <para>
/// Deterministic: each case is generated from a seed of its own, derived from
/// the name of the check and the number of the case, so a run fails on the same
/// text every time and on every machine. The report names the seed of the case
/// and the text it was made smaller to.
/// </para>
/// <para>
/// Three environment variables steer a run, and none is set in CI:
/// DM_MARKUP_SEED runs the one case of that seed, the way a report says to;
/// DM_MARKUP_RUNS sets how many cases each check runs; DM_MARKUP_SALT mixes a
/// word into the seeds, for a search over cases no default run has tried.
/// docs/guides/TESTING.md, section "Генеративные проверки разметки", has the
/// commands.
/// </para>
/// <para>
/// A text is made smaller a piece at a time: runs of pieces are taken out,
/// halving the run until it is a single piece, for as long as the text still
/// fails; then a piece written many times in a row is written fewer times. What
/// is reported still fails, and nothing more can be taken out of it piece by
/// piece.
/// </para>
/// </remarks>
public static class MarkupProperty
{
    /// <summary>How many texts a check tries to take out before it reports what it has.</summary>
    private const int ShrinkBudget = 4000;

    /// <summary>
    /// Run the check. <paramref name="fails"/> answers null for a text that
    /// passes and the reason for one that does not; it must answer the same for
    /// the same text every time.
    /// </summary>
    /// <param name="name">The name of the check, which its seeds are derived from</param>
    /// <param name="runs">How many cases it runs by default</param>
    /// <param name="generate">The generator of a case</param>
    /// <param name="fails">The check</param>
    public static void Check(string name, int runs, Func<MarkupRandom, MarkupSample> generate,
        Func<string, string?> fails)
    {
        var baseSeed = MarkupRandom.SeedOf(name + Environment.GetEnvironmentVariable("DM_MARKUP_SALT"));
        var cases = Seeds(baseSeed, runs);

        foreach (var seed in cases)
        {
            var sample = generate(new MarkupRandom(seed));
            var reason = Safely(fails, sample.Text);
            if (reason == null) continue;

            var smallest = Shrink(sample.Atoms, fails);
            throw new MarkupPropertyException(Report(name, seed, sample.Text, smallest, Safely(fails, smallest)!));
        }
    }

    /// <summary>The seeds of the cases a check runs: the one asked for, or the default run.</summary>
    private static IEnumerable<ulong> Seeds(ulong baseSeed, int runs)
    {
        var asked = Environment.GetEnvironmentVariable("DM_MARKUP_SEED");
        if (!string.IsNullOrWhiteSpace(asked))
        {
            yield return ulong.Parse(asked, CultureInfo.InvariantCulture);
            yield break;
        }

        var count = int.TryParse(Environment.GetEnvironmentVariable("DM_MARKUP_RUNS"), out var told) && told > 0
            ? told
            : runs;
        for (var index = 0; index < count; index++)
        {
            yield return MarkupRandom.CaseSeed(baseSeed, index);
        }
    }

    /// <summary>The check, with an exception it throws counted as a failure.</summary>
    private static string? Safely(Func<string, string?> fails, string text)
    {
        try
        {
            return fails(text);
        }
        catch (Exception exception)
        {
            return $"threw {exception.GetType().Name}: {exception.Message}";
        }
    }

    private static string Shrink(IReadOnlyList<string> atoms, Func<string, string?> fails)
    {
        var current = atoms.ToList();
        var budget = ShrinkBudget;
        bool StillFails(List<string> candidate) => --budget >= 0 && Safely(fails, string.Concat(candidate)) != null;

        var changed = true;
        while (changed && budget > 0)
        {
            changed = false;
            for (var chunk = Math.Max(1, current.Count / 2); chunk >= 1; chunk /= 2)
            {
                for (var start = 0; start < current.Count && budget > 0;)
                {
                    var candidate = current.Take(start).Concat(current.Skip(start + chunk)).ToList();
                    if (candidate.Count < current.Count && StillFails(candidate))
                    {
                        current = candidate;
                        changed = true;
                    }
                    else
                    {
                        start += chunk;
                    }
                }

                if (chunk == 1) break;
            }

            for (var index = 0; index < current.Count && budget > 0; index++)
            {
                var shorter = Shorter(current[index]);
                if (shorter == null) continue;

                var candidate = current.ToList();
                candidate[index] = shorter;
                if (!StillFails(candidate)) continue;

                current = candidate;
                changed = true;
                index--;
            }
        }

        return string.Concat(current);
    }

    /// <summary>A piece written many times in a row, written half as many; null for any other piece.</summary>
    private static string? Shorter(string atom)
    {
        if (atom.Length < 8) return null;

        for (var period = 1; period <= atom.Length / 4; period++)
        {
            if (atom.Length % period != 0) continue;

            var unit = atom[..period];
            if (!Enumerable.Range(0, atom.Length / period).All(at => string.CompareOrdinal(atom, at * period, unit, 0, period) == 0))
                continue;

            return string.Concat(Enumerable.Repeat(unit, atom.Length / period / 2));
        }

        return null;
    }

    private static string Report(string name, ulong seed, string original, string smallest, string reason) =>
        new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"{name} fails.")
            .AppendLine(CultureInfo.InvariantCulture, $"Smallest failing text: {MarkupLiteral.Of(smallest)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Why: {reason}")
            .AppendLine(CultureInfo.InvariantCulture, $"Generated as: {MarkupLiteral.Of(original)}")
            .AppendLine(CultureInfo.InvariantCulture,
                $"Run this case alone: DM_MARKUP_SEED={seed} dotnet test --filter {name}")
            .ToString();

}

/// <summary>A generated text a check failed on, made as small as it goes.</summary>
public sealed class MarkupPropertyException(string message) : Exception(message);
