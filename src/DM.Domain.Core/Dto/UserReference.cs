using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Dto;

/// <summary>
/// A user as a list renders them: who they are, whether they are online, and the
/// two flags that colour their name.
/// </summary>
/// <remarks>
/// The counterpart of the API's UserRef, and the whole reason it exists is that
/// <see cref="GeneralUser" /> promises far more. Filling a GeneralUser costs
/// twenty-one aggregate queries — achievement counters, hosting breakdowns,
/// subscribers, username history — and a caller that only needs a name and an
/// avatar-less badge pays all of it. This type is filled by one projection, so
/// asking for it cannot quietly buy the hydration; the flip side is that it must
/// not grow fields that need one, or it turns back into GeneralUser.
/// </remarks>
public class UserReference
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// Last activity moment (UTC), for the online indicator
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// Role, for the badge
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Whether the user is a newbie, which affects the name colour.
    /// Read from the database's own stored definition rather than recomputed.
    /// </summary>
    public bool IsNewbie { get; set; }
}
