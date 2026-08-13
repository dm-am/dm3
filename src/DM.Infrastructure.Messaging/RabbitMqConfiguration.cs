using System;
using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// How long a publisher that asks for confirmation waits for it.
    /// </summary>
    /// <remarks>
    /// Only the mail producer asks; see MailSender for why it is the only one.
    /// The wait is inside a request the reader is watching, so the ceiling is
    /// what a person will sit through rather than what a broker might need: a
    /// broker that has not answered in five seconds is not about to.
    /// </remarks>
    public TimeSpan PublishConfirmTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The one place four settings become a connection.
    /// </summary>
    /// <remarks>
    /// Userinfo in <see cref="Endpoint" /> is ignored on purpose: AmqpTcpEndpoint
    /// takes host, port and scheme off the Uri and nothing else. Credentials and the
    /// virtual host travel as fields instead, so a password containing a reserved
    /// character needs no percent-escaping and the default virtual host needs no
    /// %2f - both of which are silently wrong rather than loud when a connection
    /// string carries them.
    ///
    /// Built once and shared, because the health probe and the client have to reach
    /// the broker as the same user: a probe that connects on the library defaults
    /// answers Healthy about a broker the application cannot log in to.
    ///
    /// Throws on an endpoint that is not an absolute Uri. Call after the guard that
    /// names the setting.
    /// </remarks>
    public ConnectionFactory CreateConnectionFactory() => new()
    {
        Endpoint = new AmqpTcpEndpoint(new Uri(Endpoint)),
        UserName = Username,
        Password = Password,
        VirtualHost = VirtualHost,
    };

    /// <summary>
    /// Bind configuration parameters to new instance
    /// </summary>
    /// <param name="configuration">Configuration source</param>
    /// <returns>Configuration instance</returns>
    public static RabbitMqConfiguration From(IConfiguration configuration)
    {
        var result = new RabbitMqConfiguration();
        configuration.GetSection(nameof(RabbitMqConfiguration)).Bind(result);
        return result;
    }
}
