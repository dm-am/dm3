using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// Getting back into an account whose second factor is gone.
/// </summary>
/// <remarks>
/// A letter does not take the factor off, and this is the whole design of the
/// path. Restoring the factor through a link to the mailbox makes the second
/// factor equal to the mailbox, and the mailbox is where the first factor is
/// restored from; such a second factor is the first one in another wrapper.
///
/// What the mailbox buys instead is a delay: it schedules the removal, says so
/// twice by letter, and any successful sign-in with the factor calls it off.
/// Whoever holds both the mailbox and the password still gets the account - after
/// a week of visible, cancellable noise rather than instantly and silently.
///
/// For the ranks that owe a factor the mailed path is closed altogether. Their
/// factor is taken off by the second administrator, which is the arrangement the
/// second administrator exists for.
/// </remarks>
public interface ITwoFactorRemovalService
{
    /// <summary>
    /// Ask, from the mailbox, for the factor to be taken off.
    /// </summary>
    /// <remarks>
    /// Answers the same for every address it is given. Which accounts have a
    /// factor is not something this endpoint may disclose, and the address is
    /// all an anonymous caller has supplied.
    /// </remarks>
    /// <param name="email">Address the request names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Request(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Follow the link from the letter: schedule the removal, do not perform it.
    /// </summary>
    /// <remarks>
    /// Sessions of the account end here, and a second letter goes out at once
    /// with the way to call the removal off. The waiting period starts now.
    /// </remarks>
    /// <param name="secret">Value from the link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Schedule(Guid secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Follow the second link: call the scheduled removal off.
    /// </summary>
    /// <param name="secret">Value from the link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Cancel(Guid secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Take the factor off every account whose waiting period has run out.
    /// </summary>
    /// <returns>How many were taken off</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> RunDue(CancellationToken cancellationToken = default);

    /// <summary>
    /// Take a colleague's factor off, as the second administrator.
    /// </summary>
    /// <remarks>
    /// Hands the caller nothing: no session of the other account and none of its
    /// rights. It opens the owner a way to sign in with the password and set the
    /// factor up again, and it ends every session the account had - which is the
    /// point when the reason is not a lost device but a stolen account.
    ///
    /// Written into the journal of both accounts. An administrator can already
    /// ban, demote and delete anybody, so being able to give a colleague their
    /// way back adds no authority - but it makes enough noise that it cannot be
    /// done quietly.
    /// </remarks>
    /// <param name="username">Whose factor to take off</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearForColleague(string username, CancellationToken cancellationToken = default);
}
