using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

// AddDmLogging assigns Log.Logger, one field for the whole process.
[CollectionDefinition(Name)]
public class StaticLoggerCollection
{
    public const string Name = "Static logger";
}
