using System.Reflection;

namespace DM.Infrastructure.Core.Logging;

/// <summary>
/// The commit this process was built from.
/// </summary>
/// <remarks>
/// The build stamps the commit into the informational version. A local build gets
/// "unknown" rather than the assembly version, which never moves.
/// </remarks>
public static class ReleaseInfo
{
    /// <summary>The commit, or "unknown" when the build did not stamp one.</summary>
    public static string Value { get; } = Read();

    private static string Read() => Parse(Assembly.GetEntryAssembly()
        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion);

    internal static string Parse(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "unknown";
        }

        // The stamp is build metadata: "1.0.0+<commit>".
        var separator = informational.IndexOf('+');
        return separator < 0 || separator == informational.Length - 1
            ? "unknown"
            : informational[(separator + 1)..];
    }
}
