using System;

namespace DM.Domain.Core.Abstractions;

/// <summary>
/// Correlation token setter for request context
/// </summary>
public interface ICorrelationTokenSetter
{
    /// <summary>
    /// Set correlation token for current context
    /// </summary>
    Guid Current { set; }
}
