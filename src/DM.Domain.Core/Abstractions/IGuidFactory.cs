using System;

namespace DM.Domain.Core.Abstractions;

/// <summary>
/// Generates GUIDs
/// </summary>
public interface IGuidFactory
{
    /// <summary>
    /// Creates GUID
    /// </summary>
    /// <returns>New GUID</returns>
    Guid Create();
}
