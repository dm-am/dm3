using System;

namespace DM.Services.DataAccess.BusinessObjects.DataContracts;

/// <summary>
/// Editable entity contract with creation and modification tracking
/// </summary>
public interface IEditable
{
    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Creator user identifier
    /// </summary>
    Guid UserId { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    Guid? ModifiedByUserId { get; set; }
}
