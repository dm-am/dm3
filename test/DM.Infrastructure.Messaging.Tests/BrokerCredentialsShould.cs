using System;
using DM.Infrastructure.Messaging;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// The health probe reaches the broker as the same user the application does.
/// </summary>
/// <remarks>
/// The endpoint carries no credentials — docker-compose writes it as a bare
/// amqp://host:port and the login, password and virtual host arrive as three
/// separate settings. Handed only the endpoint, the probe connected on the client
/// library's defaults, guest/guest on "/", and a broker the application could not
/// log in to answered Healthy on the one signal that exists to say otherwise.
///
/// So the factory is built once from all four settings and shared. Asserted on the
/// object rather than on a connection string, because a connection string is where
/// the other half of this goes wrong: a password with a reserved character in it
/// needs percent-escaping and the default virtual host needs %2f, and both of those
/// are wrong quietly.
/// </remarks>
public class BrokerCredentialsShould
{
    private static RabbitMqConfiguration Settings => new()
    {
        Endpoint = "amqp://dm-rmq:5672",
        Username = "dm-app",
        Password = "p@ss/word",
        VirtualHost = "dm3",
    };

    [Fact]
    public void CarryEverySettingIntoTheConnection()
    {
        var factory = Settings.CreateConnectionFactory();

        factory.UserName.Should().Be("dm-app", "the defaults of the client are guest/guest");
        factory.Password.Should().Be("p@ss/word",
            "as a field it needs no escaping, which is what a connection string would have got wrong");
        factory.VirtualHost.Should().Be("dm3");
        factory.Endpoint.HostName.Should().Be("dm-rmq");
        factory.Endpoint.Port.Should().Be(5672);
    }

    /// <summary>
    /// Credentials in the endpoint are not a second way of setting them.
    /// </summary>
    [Fact]
    public void IgnoreWhateverUserinfoTheEndpointCarries()
    {
        var settings = Settings;
        settings.Endpoint = "amqp://someone:else@dm-rmq:5672";

        var factory = settings.CreateConnectionFactory();

        factory.UserName.Should().Be("dm-app",
            "AmqpTcpEndpoint reads host, port and scheme off the Uri and nothing else, so " +
            "userinfo there is a value that looks like it is in force and is not");
    }

    /// <summary>
    /// A relative endpoint is refused here rather than connected somewhere.
    /// </summary>
    /// <remarks>
    /// The guard that names the setting runs before this and is where a bad value is
    /// supposed to be caught. What this holds is the behaviour if it ever does not:
    /// throwing beats quietly reaching an address nobody wrote down.
    /// </remarks>
    [Fact]
    public void RefuseAnEndpointThatIsNotAnAbsoluteAddress()
    {
        var settings = Settings;
        settings.Endpoint = "dm-rmq";

        var build = () => settings.CreateConnectionFactory();

        build.Should().Throw<UriFormatException>();
    }
}
