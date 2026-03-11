using System;

namespace DM.Infrastructure.Persistence.Entities.Contracts;

/// <summary>
/// Editable entity contract with creation and modification tracking
/// </summary>
internal interface IEditable
{
    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Author (creator) user identifier
    /// </summary>
    Guid AuthorId { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    Guid? ModifiedByUserId { get; set; }
}
