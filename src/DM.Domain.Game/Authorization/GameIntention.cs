namespace DM.Domain.Game.Authorization;

public enum GameIntention
{
    Create = 0,

    Read = 1,

    Edit = 2,

    Delete = 3,

    /// <summary>
    /// Approve somebody else's game or send it back for edits. Site-wide and
    /// targetless: the rule is a rank (Mentor and above), not a relation to the game.
    /// </summary>
    SetStatusModeration = 4,

    SetStatusDraft = 5,

    SetStatusActive = 7,

    SetStatusClosed = 10,

    CreateComment = 12,

    CreateCharacter = 15,

    InvitePlayer = 16,

    InviteReader = 17,

    CancelInvitation = 18,

    InviteAssistant = 19,

    /// <summary>
    /// Remove an assistant from the game
    /// </summary>
    RemoveUser = 20,

    /// <summary>
    /// Edit the settings page: the information form and the invitations it lists.
    /// Wider than <see cref="Edit" />, which the roster, the rooms and the blacklist keep.
    /// </summary>
    EditSettings = 21,

    /// <summary>
    /// Ask for a premoderation verdict on your own game: the master's move alone,
    /// because who may fill in the form is not who may declare it ready.
    /// </summary>
    SubmitForApproval = 22
}
