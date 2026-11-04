using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The one AMQP connection of a host.
/// </summary>
/// <remarks>
/// Everything opens channels on it, the health probe included; recovery is the client's default.
/// </remarks>
public sealed class DmBrokerConnection(IConnectionFactory connectionFactory) : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _dialing = new(1, 1);
    private IConnection? _connection;

    /// <summary>
    /// The open connection of this host, dialing the broker on the first call.
    /// </summary>
    /// <remarks>
    /// A failed dial is not cached, so the subscription retries dial again.
    /// </remarks>
    public async Task<IConnection> GetOpenConnection(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _connection) is { } dialed)
        {
            return dialed;
        }

        await _dialing.WaitAsync(cancellationToken);
        try
        {
            return _connection ??= await connectionFactory.CreateConnectionAsync(cancellationToken);
        }
        finally
        {
            _dialing.Release();
        }
    }

    /// <summary>
    /// Closes the connection of the host, channels and consumers with it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        _dialing.Dispose();
    }

    /// <summary>
    /// Synchronous fallback, for a container that is disposed synchronously.
    /// </summary>
    /// <remarks>
    /// A container disposed synchronously refuses a component that is only asynchronously disposable.
    /// </remarks>
    public void Dispose()
    {
        Interlocked.Exchange(ref _connection, null)?.Dispose();
        _dialing.Dispose();
    }
}
