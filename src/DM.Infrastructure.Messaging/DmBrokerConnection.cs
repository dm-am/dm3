using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The one AMQP connection of a host.
/// </summary>
/// <remarks>
/// A connection is a TCP session with its own multi-step handshake; what is
/// cheap to open per producer or per consumer is a channel on it. So a host
/// holds one connection, dialed on first use, and everything else — producers,
/// consumers, topology declarations, the health probe — opens channels on it.
/// The probe sharing the connection is deliberate: it answers about the
/// connection the application actually publishes on, not about a parallel one
/// that could be healthy while this one is not.
///
/// Recovery belongs to the client: AutomaticRecoveryEnabled and
/// TopologyRecoveryEnabled are the factory defaults, so a broker restart is
/// redialed and the exchanges, queues, bindings and consumer subscriptions are
/// re-declared without any code here. What this class adds is only the lazy
/// dial and the guarantee that concurrent first callers get one connection
/// rather than a connection each.
/// </remarks>
public sealed class DmBrokerConnection(IConnectionFactory connectionFactory) : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _dialing = new(1, 1);
    private IConnection? _connection;

    /// <summary>
    /// The open connection of this host, dialing the broker on the first call.
    /// </summary>
    /// <remarks>
    /// A failed dial is not cached: the next caller dials again, which is what
    /// the subscription retry policies of the consumers lean on.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
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
    /// Both paths exist because a container disposed synchronously refuses a
    /// component that is only asynchronously disposable, and which way a host
    /// or a test tears its container down is not this class's decision.
    /// </remarks>
    public void Dispose()
    {
        Interlocked.Exchange(ref _connection, null)?.Dispose();
        _dialing.Dispose();
    }
}
