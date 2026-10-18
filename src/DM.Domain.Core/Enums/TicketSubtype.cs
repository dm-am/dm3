namespace DM.Domain.Core.Enums;

/// <summary>
/// Category of a moderation ticket ("обращение").
/// Visibility of each category on the moderation list endpoints is derived
/// from the caller role: Moderator sees user complaints and suggestions,
/// SeniorModerator additionally sees complaints about moderator decisions,
/// Admin sees everything including support categories.
/// </summary>
public enum TicketSubtype
{
    /// <summary>
    /// Complaint about a user (visible to moderators and above)
    /// </summary>
    UserComplaint = 0,

    /// <summary>
    /// Complaint about a junior moderator decision (visible to senior moderators and above)
    /// </summary>
    ModeratorDecisionComplaint = 1,

    /// <summary>
    /// Complaint about a senior moderator decision (visible to administrators)
    /// </summary>
    SeniorModeratorDecisionComplaint = 2,

    /// <summary>
    /// Site improvement suggestion (visible to moderators and above)
    /// </summary>
    /// <remarks>
    /// Nothing files one any more: suggestions are discussed on the forum, and
    /// the intake forms refuse the category. It stays in the enumeration for
    /// the moderation queues that still filter by it.
    /// </remarks>
    SiteImprovementSuggestion = 3,

    /// <summary>
    /// Bug report (visible to administrators)
    /// </summary>
    Bug = 4,

    /// <summary>
    /// Account access recovery request (visible to administrators)
    /// </summary>
    AccessRecovery = 5,

    /// <summary>
    /// Registration problems (visible to administrators)
    /// </summary>
    RegistrationIssue = 6,

    /// <summary>
    /// Request to remove the account, "Удаление аккаунта" on the support form
    /// (visible to administrators).
    /// </summary>
    /// <remarks>
    /// An ordinary support ticket and nothing more: the form performs no action
    /// on the account. Removal happens only through moderation, by hand, on the
    /// moderated profile. The category exists so the request arrives in the
    /// support queue under its own name instead of hiding inside a free-form
    /// message.
    /// </remarks>
    AccountRemoval = 7,

    /// <summary>
    /// Request to bring a removed account back (visible to administrators).
    /// Reads "Восстановление удаленного аккаунта" on the support form.
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="AccountRemoval"/>: the ticket asks, moderation
    /// acts. Scoped to administrators for the reason <see cref="AccessRecovery"/>
    /// is - both are claims about who owns an account, and the address needed to
    /// check such a claim is an administrator's to read.
    /// </remarks>
    AccountRestoration = 8,

    /// <summary>
    /// A request about the person's own data: to show, correct or delete it, or
    /// to stop processing it (visible to administrators). Reads "Персональные
    /// данные" on the support form; the privacy policy sends its reader here.
    /// </summary>
    /// <remarks>
    /// An ordinary support ticket, as <see cref="AccountRemoval"/> is: the ticket
    /// asks, the administration acts. Scoped to administrators because the
    /// answer is about the data of an account.
    /// </remarks>
    PersonalData = 9,

    /// <summary>
    /// A question about the user agreement (visible to administrators). Reads
    /// "Вопрос по соглашению" on the support form; the agreement sends its
    /// reader here.
    /// </summary>
    AgreementQuestion = 10
}
