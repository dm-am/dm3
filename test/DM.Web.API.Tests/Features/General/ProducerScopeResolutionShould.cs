using System;
using System.Linq;
using DM.Domain.Core.Events;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Persistence;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the lifetimes of the two producers a request resolves.
/// </summary>
/// <remarks>
/// The mail sender owns an AMQP channel: the builder returns a producer that
/// opens one on its first send and closes it only on Dispose. Its wrapper was
/// once registered per dependency by the blanket scan and was not disposable,
/// so every resolution that published anything left a channel open for the life
/// of the process — until the broker's channel ceiling turned every further
/// publish in the API into an exception. Three properties keep that closed, and
/// all three are asserted because any one alone is not enough: the
/// implementation must be disposable, the registration must be scoped rather
/// than transient, and scoped rather than a singleton. Deliberately not a
/// singleton: a RabbitMQ channel is not safe to publish on from several threads
/// at once.
///
/// The event producer stopped owning a channel with the outbox (W1.5):
/// publishing an event is an INSERT now, its registration moved from the
/// messaging module to the persistence module, and what its lifetime protects
/// is different — scoped so that it writes with the one DmDbContext of the
/// request's scope. A per-dependency copy would be handed a context of its own
/// by the blanket scan, and EF calls on two contexts in one scope is exactly
/// what the one-context-per-scope rule exists to prevent.
///
/// The registrations are inspected rather than resolved: the mail sender is
/// built over the real broker plumbing and the event producer over the real
/// context, so activating either in a unit test would dial a dependency.
/// Lifetime is exactly what the fix consists of, and it is visible without
/// constructing anything.
/// </remarks>
public class ProducerScopeResolutionShould : UnitTestBase
{
    private readonly IServiceCollection _services;

    public ProducerScopeResolutionShould()
    {
        var services = new ServiceCollection();
        services.AddDmMessaging();
        services.AddDmMail();
        services.AddDmPersistence();
        _services = services;
    }

    /// <summary>
    /// The registration a resolution answers with. MS.DI hands a single
    /// resolution to the last descriptor of the service type, so that is the
    /// one whose lifetime matters — the blanket scan may have left earlier
    /// pairs on the enumerable.
    /// </summary>
    private ServiceDescriptor EffectiveRegistration(Type service)
    {
        var descriptor = _services.LastOrDefault(d => d.ServiceType == service);
        descriptor.Should().NotBeNull($"{service.Name} must be registered");
        return descriptor!;
    }

    [Theory]
    [InlineData(typeof(IEventProducer))]
    [InlineData(typeof(IMailSender))]
    public void RegisterProducersAsScoped(Type service)
    {
        EffectiveRegistration(service).Lifetime.Should().Be(ServiceLifetime.Scoped,
            "per-dependency means one open AMQP channel, or one DbContext of its own, " +
            "per consumer that asks; a singleton would share one channel, or capture " +
            "one context, across every concurrent request");
    }

    /// <summary>
    /// The channel half of the rule, which only the mail sender still carries:
    /// nothing else closes the channel it opens. The event producer is
    /// deliberately absent — it has no channel, and the scoped DmDbContext it
    /// writes with is disposed by the scope that owns it.
    /// </summary>
    [Fact]
    public void KeepTheMailSenderDisposable()
    {
        var implementation = EffectiveRegistration(typeof(IMailSender)).ImplementationType;

        implementation.Should().NotBeNull("the sender is registered by type");
        typeof(IDisposable).IsAssignableFrom(implementation)
            .Should().BeTrue("nothing else closes the channel");
    }

    /// <summary>
    /// The move is asserted, not assumed: the implementation behind the
    /// interface is the outbox producer from the persistence module, so a
    /// revived registration in the messaging module — which would quietly bring
    /// fire-and-forget back — loses to nothing silently.
    /// </summary>
    [Fact]
    public void ResolveTheEventProducerToTheOutboxImplementation()
    {
        EffectiveRegistration(typeof(IEventProducer)).ImplementationType!.Name
            .Should().Be("OutboxEventProducer",
                "publishing an event is an outbox INSERT since W1.5, and any other default " +
                "implementation would silently return to losing events with the broker");
    }
}
