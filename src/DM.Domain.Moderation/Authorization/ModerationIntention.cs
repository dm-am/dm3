namespace DM.Domain.Moderation.Authorization;

public enum ModerationIntention
{
    // Bans, warnings and curatorship compare the role in their services, because the
    // refusal names the role it wants (docs/architecture/AUTHORIZATION.md). They get no
    // intention: one nobody routes through is a second copy of the threshold, free to
    // drift from the check that runs.

    ViewModNotes,

    CreateModNote,

    EditModNote,

    DeleteModNote,

    SetUserRole,

    /// <summary>
    /// Edit the Info field of a user profile
    /// </summary>
    ModerateUserProfile,

    /// <summary>
    /// Put a user under moderation watch, or take them off it
    /// </summary>
    SetModerationWatch,

    ManageTags
}
