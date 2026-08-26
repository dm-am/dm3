using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The one place a presented second factor is judged.
/// </summary>
/// <remarks>
/// Three paths need the same answer - finishing a login, switching the factor
/// off, and reissuing the recovery codes - and the rules they need are the same
/// down to the replay guard. Written out three times, one of the three would
/// eventually accept a step already spent.
/// </remarks>
public interface ITwoFactorVerifier
{
    /// <summary>
    /// Whether the value passes as this account's second factor, and spends it
    /// if it does.
    /// </summary>
    /// <remarks>
    /// Both kinds count: a code from the device and a recovery code. Accepting
    /// has a side effect either way - a time step is claimed or a recovery code
    /// is spent - so a caller must not ask twice about one value.
    /// </remarks>
    /// <param name="state">Factor of the account</param>
    /// <param name="value">Code or recovery code, as the person typed it</param>
    /// <param name="ipAddress">Address the value was presented from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> Accept(
        TwoFactorState state, string value, string? ipAddress,
        CancellationToken cancellationToken = default);
}
