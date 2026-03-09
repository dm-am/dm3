using System;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.DataContracts;

/// <summary>
/// Administrative activity contract (Warning, Ban)
/// </summary>
internal interface IAdministrated : IRemovable
{
    /// <summary>
    /// Target user identifier (who received the warning/ban)
    /// </summary>
    Guid TargetUserId { get; set; }

    /// <summary>
    /// Author identifier (moderator who issued the warning/ban)
    /// </summary>
    Guid AuthorId { get; set; }

    /// <summary>
    /// Target user (who received the warning/ban)
    /// </summary>
    User TargetUser { get; set; }

    /// <summary>
    /// Author (moderator who issued the warning/ban)
    /// </summary>
    User Author { get; set; }
}
