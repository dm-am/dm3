namespace DM.Domain.Core.Abstractions;

public interface ICorrelationTokenProvider
{
    /// <summary>
    /// Get current context correlation token
    /// </summary>
    Guid Current { get; }
}
