using NSubstitute;

namespace DM.Testing;

/// <remarks>Not IDisposable: a substitute holds nothing to release.</remarks>
public abstract class UnitTestBase
{
    protected static T Mock<T>() where T : class => Substitute.For<T>();
}
