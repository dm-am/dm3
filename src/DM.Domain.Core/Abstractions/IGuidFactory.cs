namespace DM.Domain.Core.Abstractions;

/// <summary>
/// Generates GUIDs
/// </summary>
public interface IGuidFactory
{
    Guid Create();
}
