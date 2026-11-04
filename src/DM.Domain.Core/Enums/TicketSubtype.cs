namespace DM.Domain.Core.Enums;

/// <summary>
/// Category of a moderation ticket ("обращение").
/// </summary>
public enum TicketSubtype
{
    /// <summary>
    /// Complaint about a user
    /// </summary>
    UserComplaint = 0,

    /// <summary>
    /// Complaint about a junior moderator decision
    /// </summary>
    ModeratorDecisionComplaint = 1,

    /// <summary>
    /// Complaint about a senior moderator decision
    /// </summary>
    SeniorModeratorDecisionComplaint = 2,

    /// <summary>
    /// Bug report
    /// </summary>
    Bug = 4,

    /// <summary>
    /// Account access recovery request
    /// </summary>
    /// <remarks>
    /// Administrators only, with <see cref="AccountRestoration"/> and <see cref="PersonalData"/>:
    /// ownership of an account is checked by its address, which only an administrator reads.
    /// </remarks>
    AccessRecovery = 5,

    /// <summary>
    /// Registration problems
    /// </summary>
    RegistrationIssue = 6,

    /// <summary>
    /// Request to remove the account
    /// </summary>
    /// <remarks>
    /// The form performs no action on the account: removal happens only through
    /// moderation, by hand.
    /// </remarks>
    AccountRemoval = 7,

    /// <summary>
    /// Request to bring a removed account back.
    /// </summary>
    AccountRestoration = 8,

    /// <summary>
    /// A request about the person's own data: to show, correct or delete it, or
    /// to stop processing it.
    /// </summary>
    PersonalData = 9,

    /// <summary>
    /// A question about the user agreement.
    /// </summary>
    AgreementQuestion = 10
}
