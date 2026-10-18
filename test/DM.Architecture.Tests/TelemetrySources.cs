using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;

namespace DM.Architecture.Tests;

/// <summary>
/// The files the telemetry providers are composed in, found rather than named.
/// </summary>
/// <remarks>
/// The rules about meters, histogram buckets and trace sources read the place
/// where the providers are built. Read by the path of the logging configuration,
/// they fail the day tracing and metrics get a file of their own, and the
/// failure names a missing line instead of the rule. A file that builds a tracer
/// or a meter provider is the composition wherever it lives.
/// </remarks>
internal static class TelemetrySources
{
    /// <summary>The calls that build a provider, as the OpenTelemetry builder spells them.</summary>
    private static readonly string[] Composes = [".WithTracing(", ".WithMetrics("];

    /// <summary>The authored files that compose a tracer or a meter provider.</summary>
    internal static IReadOnlyList<string> CompositionFiles
    {
        get
        {
            var files = RepositoryFiles.ProductionSources()
                .Where(path => Composes.Any(call =>
                    SourceText.ReadCode(path).Contains(call, StringComparison.Ordinal)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            files.Should().NotBeEmpty(
                "the hosts export traces and metrics, so some file builds the providers, and a walk " +
                "that finds none hands every rule reading it an empty text to pass over");

            return files;
        }
    }

    /// <summary>The code of the composition, comments left out, as one text.</summary>
    internal static string CompositionCode =>
        string.Join("\n", CompositionFiles.Select(SourceText.ReadCode));
}
