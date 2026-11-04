namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// How long a moderator note about a profile may be, on creation and on editing alike.
/// </summary>
internal static class ProfileNoteFieldLimits
{
    public const int ContentMaxLength = 4000;
}
