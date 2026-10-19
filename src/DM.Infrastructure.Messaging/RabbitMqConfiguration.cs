using System;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Configuration for message queue connection
/// </summary>
public class RabbitMqConfiguration
{
    /// <summary>
    /// Broker endpoint url
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Broker virtual host
    /// </summary>
    public string VirtualHost { get; set; } = string.Empty;

    /// <summary>
    /// User name
    /// </summary>
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// How long a publisher that asks for confirmation waits for it.
    /// </summary>
    /// <remarks>
    /// What a person waiting on a request sits through, not what a broker might need.
    /// </remarks>
    public TimeSpan PublishConfirmTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The one place four settings become a connection.
    /// </summary>
    /// <remarks>
    /// Credentials travel as fields, so they need no percent-escaping; userinfo in <see cref="Endpoint" /> is ignored.
    /// Throws on an endpoint that is not an absolute Uri.
    /// </remarks>
    public ConnectionFactory CreateConnectionFactory() => new()
    {
        Endpoint = new AmqpTcpEndpoint(new Uri(Endpoint)),
        UserName = Username,
        Password = Password,
        VirtualHost = VirtualHost,
    };
}
