using System;

namespace DM.Domain.Core.Abstractions;

/// <summary>
/// Correlation token provider
/// </summary>
public interface ICorrelationTokenProvider
{
    /// <summary>
    /// Get current context correlation token
    /// </summary>
    Guid Current { get; }
}
