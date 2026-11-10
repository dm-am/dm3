using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// The single place premoderation is turned into a question about the reader:
/// who may pass the verdict, and which statuses the verdict is pending on.
/// </summary>
/// <remarks>
/// Seeing a premoderated module and passing the verdict on it are one right, so
/// the verdict intention, the read gates and the storage scope read one rank.
/// </remarks>
public static class PremoderationAccess
{
    /// <summary>
    /// Whether the user may pass a premoderation verdict on somebody's module —
    /// and, by the same right, open the module in order to pass it.
    /// </summary>
    /// <remarks>
    /// A rank and nothing else. Being the assigned curator is a separate arm of
    /// the gates that ask this, and it cannot stand in for the rank: a module in
    /// AwaitingEdits has no curator recorded, which is the state every newbie's
    /// module is created in.
    /// </remarks>
    public static bool MayJudgePremoderation(this IAuthorizationSubject user) =>
        user.IsAuthenticated && user.Role >= UserRole.Mentor;

    /// <summary>
    /// Whether premoderation is what currently hides the module.
    /// </summary>
    /// <remarks>
    /// Deliberately narrower than "the module is hidden". A draft and a removed
    /// module are hidden too, and no rank opens them here: the verdict is about
    /// modules waiting on it, so the rank arm of the read gates and of the
    /// storage scope is keyed on exactly these two statuses.
    /// </remarks>
    public static bool IsPending(PremoderationStatus status) =>
        status == PremoderationStatus.AwaitingApproval ||
        status == PremoderationStatus.AwaitingEdits;
}
