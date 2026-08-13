using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// Tests that install the process-wide logger, run one at a time.
/// </summary>
/// <remarks>
/// AddDmLogging assigns Log.Logger, and that is one field for the whole process.
/// Run in parallel, these classes overwrite each other's logger between the call
/// that installs it and the assertion that reads it, and fail on a configuration
/// neither of them asked for - the flavour of failure that gets rerun until it
/// passes rather than read.
/// </remarks>
[CollectionDefinition(Name)]
public class StaticLoggerCollection
{
    /// <summary>Name the classes join this collection by.</summary>
    public const string Name = "Static logger";
}
