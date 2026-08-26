using NSubstitute;

namespace DM.Testing;

/// <summary>
/// Base for unit tests: a single factory for the substitutes a test needs.
/// </summary>
/// <remarks>
/// Deliberately not IDisposable. A substitute records what it received and answers
/// about it on demand, so there is nothing to release and nothing a teardown hook
/// could verify that the test did not already say out loud. Expectations belong at
/// the assertion site: <c>substitute.Received(1).Method(arg)</c>. Tests owning
/// disposable state implement IDisposable themselves.
/// </remarks>
public abstract class UnitTestBase
{
    protected static T Mock<T>() where T : class => Substitute.For<T>();
}
