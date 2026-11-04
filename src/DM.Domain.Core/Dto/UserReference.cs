using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Dto;

/// <summary>
/// A user as a list renders them: who they are, whether they are online, and the
/// two flags that colour their name.
/// </summary>
/// <remarks>
/// The counterpart of the API's UserRef. Filled by one projection, unlike the
/// aggregate queries of <see cref="GeneralUser" />, so it must not grow fields
/// that need one.
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
