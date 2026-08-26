namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// How long a moderator note about a profile may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class ProfileNoteFieldLimits
{
    /// <summary>Longest note text.</summary>
    public const int ContentMaxLength = 4000;
}
