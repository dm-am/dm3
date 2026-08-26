using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The two letters the mailed removal path is made of.
/// </summary>
/// <remarks>
/// Both carry a link, which is why they exist as letters rather than as
/// notifications: one asks the owner to confirm a request, the other hands him
/// the way to call the result off. Whatever else the product eventually says by
/// mail about the second factor is announcement and belongs elsewhere.
/// </remarks>
public interface ITwoFactorRemovalMailSender
{
    /// <summary>
    /// The letter that asks whether the request was really made.
    /// </summary>
    /// <param name="email">Confirmed address of the account</param>
    /// <param name="username">Name of the account</param>
    /// <param name="secret">Value of the confirmation link</param>
    Task SendRequest(string email, string username, Guid secret);

    /// <summary>
    /// The letter that says the removal is scheduled and how to stop it.
    /// </summary>
    /// <param name="email">Confirmed address of the account</param>
    /// <param name="username">Name of the account</param>
    /// <param name="dueUtc">When the factor comes off</param>
    /// <param name="secret">Value of the cancellation link</param>
    Task SendScheduled(string email, string username, DateTimeOffset dueUtc, Guid secret);
}
