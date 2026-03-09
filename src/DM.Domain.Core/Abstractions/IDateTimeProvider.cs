using System;

namespace DM.Domain.Core.Abstractions;

/// <summary>
/// Provides date in the same format across the application
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Current moment in UTC
    /// </summary>
    DateTimeOffset Now { get; }
}
