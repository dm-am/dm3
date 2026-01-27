using System;

namespace DM.Services.DataAccess.BusinessObjects.DataContracts;

/// <summary>
/// Soft-deletable entity contract with audit information
/// </summary>
public interface ISoftDeletable : IRemovable
{
    /// <summary>
    /// User who deleted the entity
    /// </summary>
    Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// When the entity was deleted
    /// </summary>
    DateTimeOffset? DeletedAtUtc { get; set; }
}
