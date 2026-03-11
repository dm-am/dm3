namespace DM.Infrastructure.Persistence.Entities.Contracts;

/// <summary>
/// Removable entity contract
/// </summary>
internal interface IRemovable
{
    /// <summary>
    /// Removed flag
    /// </summary>
    bool IsRemoved { get; set; }
}
