using Microsoft.Extensions.Configuration;

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
