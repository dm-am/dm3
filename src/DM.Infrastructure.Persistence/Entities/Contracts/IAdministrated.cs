using System;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Contracts;

/// <summary>
/// Administrative activity contract (Warning, Ban).
/// </summary>
/// <remarks>
/// Deliberately not <see cref="IRemovable" />. A warning is soft-deletable and
/// says so on its own declaration; a ban is not - lifting one is a moderation
/// event with its own columns, and while the two shared a base the ban row fell
/// under the global !IsRemoved filter and left every history the moment it was
/// lifted.
/// </remarks>
internal interface IAdministrated
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
