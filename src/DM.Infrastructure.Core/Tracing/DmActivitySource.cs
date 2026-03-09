using System.Diagnostics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Activity source for distributed tracing
/// </summary>
public static class DmActivitySource
{
    /// <summary>
    /// Name of the activity source
    /// </summary>
    public const string Name = "DM.Services";

    /// <summary>
    /// Activity source instance for creating activities
    /// </summary>
    public static readonly ActivitySource Source = new(Name, "1.0.0");
}
