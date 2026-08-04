using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Releases the email addresses of registrations that were never finished.
/// </summary>
public interface IPendingRegistrationCleanupProcessor
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of pending registrations deleted.</returns>
    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
