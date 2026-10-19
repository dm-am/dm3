using NSubstitute;

namespace DM.Testing;

/// <summary>
/// Base for unit tests: a single factory for the substitutes a test needs.
/// </summary>
/// <remarks>Not IDisposable: a substitute holds nothing to release.</remarks>
public abstract class UnitTestBase
{
    protected static T Mock<T>() where T : class => Substitute.For<T>();
}
