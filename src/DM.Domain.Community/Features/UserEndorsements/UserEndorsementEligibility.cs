using System.Net;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Whether the current user may write a recommendation about a given user,
/// and the server's own sentence when they may not.
/// </summary>
/// <remarks>
/// The point of this type is that there is exactly one place where the
/// question is answered. <see cref="IUserEndorsementService.CreateAsync"/>
/// refuses by the same evaluation the client asks for beforehand, so an
/// offered button and the answer to pressing it cannot disagree.
///
/// <see cref="Status"/> travels with the reason because the refusals are not
/// all the same event: a second recommendation for a pair that already has one
/// is a conflict, everything else is a plain refusal.
/// </remarks>
/// <param name="CanCreate">Whether the recommendation may be written.</param>
/// <param name="Reason">Refusal text for the reader; null when allowed.</param>
/// <param name="Status">Status the create endpoint answers a refusal with.</param>
public record UserEndorsementEligibility(
    bool CanCreate,
    string? Reason = null,
    HttpStatusCode Status = HttpStatusCode.OK)
{
    /// <summary>
    /// The recommendation may be written.
    /// </summary>
    public static readonly UserEndorsementEligibility Allowed = new(true);

    /// <summary>
    /// The recommendation is refused, with the sentence the reader is shown.
    /// </summary>
    /// <param name="reason">Refusal text.</param>
    /// <param name="status">Status the create endpoint answers with.</param>
    public static UserEndorsementEligibility Refused(
        string reason, HttpStatusCode status = HttpStatusCode.Forbidden) =>
        new(false, reason, status);
}
