using System.Diagnostics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Activity source for distributed tracing
/// </summary>
public static class DmActivitySource
{
    public const string Name = "DM";

    /// <summary>The version the source and every meter of this project report.</summary>
    public const string Version = "1.0.0";

    public static readonly ActivitySource Source = new(Name, Version);
}
