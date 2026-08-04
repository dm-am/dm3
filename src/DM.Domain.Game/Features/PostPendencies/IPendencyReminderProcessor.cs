using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Reminds players about room posts that are still owed.
/// </summary>
public interface IPendencyReminderProcessor
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of reminders sent.</returns>
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken = default);
}
