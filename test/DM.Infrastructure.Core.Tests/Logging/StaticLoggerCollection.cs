using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// Tests that install the process-wide logger, run one at a time.
/// </summary>
/// <remarks>AddDmLogging assigns Log.Logger, one field for the whole process.</remarks>
[CollectionDefinition(Name)]
public class StaticLoggerCollection
{
    /// <summary>Name the classes join this collection by.</summary>
    public const string Name = "Static logger";
}
