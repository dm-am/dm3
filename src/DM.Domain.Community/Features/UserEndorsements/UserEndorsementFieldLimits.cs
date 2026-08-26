namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// How long an endorsement text may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class UserEndorsementFieldLimits
{
    /// <summary>Longest endorsement text.</summary>
    public const int TextMaxLength = 5000;
}
