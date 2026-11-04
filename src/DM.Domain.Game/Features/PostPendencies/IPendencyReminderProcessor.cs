using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Reminds players about room posts that are still owed.
/// </summary>
public interface IPendencyReminderProcessor
{
    /// <returns>Number of reminders sent.</returns>
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken = default);
}
