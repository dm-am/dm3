using System;

namespace DM.Infrastructure.Persistence.Entities.Contracts;

/// <summary>
/// Soft-deletable entity contract with audit information
/// </summary>
internal interface ISoftDeletable : IRemovable
{
    /// <summary>
    /// User who deleted the entity
    /// </summary>
    Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// When the entity was deleted
    /// </summary>
    DateTimeOffset? DeletedUtc { get; set; }
}
