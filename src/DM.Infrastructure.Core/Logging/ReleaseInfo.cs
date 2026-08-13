using System;
using System.Reflection;

namespace DM.Infrastructure.Core.Logging;

/// <summary>
/// The commit this process was built from.
/// </summary>
/// <remarks>
/// A process that cannot name its own release makes every question about a
/// deployment a question about the machine instead: which image is running, when
/// was it pulled, what was in it. The build stamps the commit into the assembly
/// version and this reads it back, so the answer travels with the log line rather
/// than with whoever still has shell access.
///
/// A local build stamps nothing and gets "unknown" — said out loud, because a
/// plausible wrong answer here (the assembly version, which never moves) is worse
/// than no answer.
/// </remarks>
public static class ReleaseInfo
{
    /// <summary>The commit, or "unknown" when the build did not stamp one.</summary>
    public static string Value { get; } = Read();

    private static string Read() => Parse(Assembly.GetEntryAssembly()
        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion);

    private static string Parse(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "unknown";
        }

        // The stamp is appended as build metadata: "1.0.0+<commit>". Without it the
        // string is the plain assembly version, which says nothing about a release.
        var separator = informational.IndexOf('+');
        return separator < 0 || separator == informational.Length - 1
            ? "unknown"
            : informational[(separator + 1)..];
    }
}
