using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// Nothing published may depend on the broker staying up.
/// </summary>
/// <remarks>
/// The client writes the body and leaves BasicProperties alone, so delivery mode
/// stayed at 1 — non-persistent. Queues are declared durable, which makes the
/// queue survive a restart of the broker and its contents not: dm.mail.sending
/// was emptied along with the dead-letter queue behind it. A letter is the only
/// record that a registration confirmation is owed, and the dead-letter queue is
/// the only artefact of one that could not be sent, so both went silently.
///
/// The pipeline is assembled here the way the producer assembles it, out of the
/// middlewares the shared registration actually installs. Asserting that the
/// middleware exists, or that the container can resolve it, would leave those two
/// halves free to drift apart — what matters is the properties the last step sees.
/// </remarks>
public class PersistentPublishingShould
{
    /// <summary>Message type of the pipeline under test.</summary>
    /// <param name="Value">Anything the codec can serialize.</param>
    public sealed record Published(string Value);

    [Fact]
    public async Task MarkEveryPublishedMessagePersistent()
    {
        using var provider = new ServiceCollection().AddDmJamqClient().BuildServiceProvider();

        var basicProperties = new Mock<IBasicProperties>();
        basicProperties.SetupAllProperties();

        var context = Context(provider, basicProperties.Object, new Published("payload"));

        var reachedTheBroker = false;
        ProducerDelegate<string, Published, RabbitProducerProperties> publish = (_, _) =>
        {
            reachedTheBroker = true;
            return Task.CompletedTask;
        };

        var pipeline = Middlewares<Published>(provider.GetRequiredService<IProducerBuilder>())
            .Reverse()
            .Aggregate(publish, (current, component) => component(current));

        await pipeline(context, CancellationToken.None);

        reachedTheBroker.Should().BeTrue(
            "the assembled pipeline has to reach the step that publishes");
        basicProperties.Object.Persistent.Should().BeTrue(
            "a non-persistent message is dropped when the broker restarts, durable " +
            "queue or not, and the mail queue is the only record that a letter is owed");
    }

    /// <summary>
    /// One registration point, or the rule holds only in the hosts that remembered it.
    /// </summary>
    [Fact]
    public void RegisterTheClientThroughOneCallOnly()
    {
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            .ToList();

        sources.Should().NotBeEmpty("the production sources live under src/");

        var callers = sources
            .Where(path => File.ReadAllText(path).Contains("AddJamqClient(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .ToList();

        callers.Should().ContainSingle(
            "a host that registers the client itself gets none of the producer " +
            "defaults, and everything it publishes goes back to being lost when the " +
            "broker restarts");
        callers[0].Should().Be("MessagingConfigurationExtensions.cs");
    }

    /// <summary>
    /// The middlewares the builder hands to a producer. The accessor is internal to
    /// the client, and it is the only view of what a real publish would run.
    /// </summary>
    private static IEnumerable<Func<
            ProducerDelegate<string, TMessage, RabbitProducerProperties>,
            ProducerDelegate<string, TMessage, RabbitProducerProperties>>>
        Middlewares<TMessage>(IProducerBuilder builder) =>
        (IEnumerable<Func<
            ProducerDelegate<string, TMessage, RabbitProducerProperties>,
            ProducerDelegate<string, TMessage, RabbitProducerProperties>>>)
        typeof(IProducerBuilder)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "GetMiddlewares")
            .MakeGenericMethod(typeof(string), typeof(TMessage), typeof(RabbitProducerProperties))
            .Invoke(builder, null)!;

    /// <summary>
    /// Both constructors are internal to the client: a producer context is only ever
    /// built inside Send, which is the call this test stands in for.
    /// </summary>
    private static ProducerContext<string, TMessage, RabbitProducerProperties> Context<TMessage>(
        IServiceProvider serviceProvider, IBasicProperties basicProperties, TMessage message)
    {
        var properties = Activator.CreateInstance(
            typeof(RabbitProducerProperties),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { basicProperties, new RabbitProducerParameters("dm.test") },
            culture: null)!;

        return (ProducerContext<string, TMessage, RabbitProducerProperties>)Activator.CreateInstance(
            typeof(ProducerContext<string, TMessage, RabbitProducerProperties>),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { serviceProvider, properties, string.Empty, message! },
            culture: null)!;
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
