using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Queues;
using Npgsql;

namespace DM.Infrastructure.Persistence.Pushes;

/// <summary>
/// Sends a push as database notifications on the push channel, one per piece.
/// </summary>
/// <remarks>
/// Never stored: a push nobody hears is lost, and the tab is right again after
/// a page load. Outside any caller's transaction, ambient one included, since a notification
/// inside a transaction goes out on its commit and vanishes with its rollback.
/// </remarks>
public static class PushSender
{
    /// <summary>
    /// Sends one push to every listener.
    /// </summary>
    /// <param name="dataSource">The process's source of connections.</param>
    /// <param name="push">The push with all its recipients.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SendAsync(
        NpgsqlDataSource dataSource, RealtimeNotification push, CancellationToken cancellationToken)
    {
        using var outsideCallers = new TransactionScope(
            TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        foreach (var piece in PushPayload.Cut(push))
        {
            await using var notify = new NpgsqlCommand("SELECT pg_notify($1, $2)", connection)
            {
                Parameters = { new() { Value = QueueNames.PushChannel }, new() { Value = piece } }
            };
            await notify.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
