using Moq;

namespace DM.Testing;

/// <summary>
/// Base for unit tests: a single factory for the mocks a test needs.
/// </summary>
/// <remarks>
/// Deliberately not IDisposable. Disposal used to call <c>MockRepository.Verify()</c>,
/// which only verifies setups marked <c>.Verifiable()</c> — the suite has none, so the
/// hook verified nothing while reading as a safety net. Expectations belong at the
/// assertion site: <c>mock.Verify(m =&gt; m.Method(arg), Times.Once)</c>. Tests owning
/// disposable state implement IDisposable themselves.
/// </remarks>
public abstract class UnitTestBase
{
    private readonly MockRepository repository = new(MockBehavior.Loose);

    protected Mock<T> Mock<T>(MockBehavior behavior = MockBehavior.Loose) where T : class =>
        repository.Create<T>(behavior);
}
