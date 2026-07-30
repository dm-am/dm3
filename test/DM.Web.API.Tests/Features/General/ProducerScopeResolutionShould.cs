using System;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using DM.Domain.Core.Events;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the AMQP channel lifetime.
/// </summary>
/// <remarks>
/// BuildRabbit returns a producer that leases a channel from the pool on its first
/// send and gives it back only on Dispose — the pool has a Get and no Return. The
/// two wrappers around it were registered per dependency by the blanket scan and
/// were not disposable, so every resolution that published anything left a channel
/// open for the life of the process. With the shipped defaults (16 pools of 256)
/// the pool threw after about four thousand published events, and from then on
/// every publish in the API failed: no search indexing, no notifications, no mail.
///
/// Three properties keep that closed, and this asserts all three because any one
/// alone is not enough: the implementation must be disposable (or nothing returns
/// the channel), the registration must be shared (or a request leases one channel
/// per consumer that asks), and it must be scoped rather than rooted.
///
/// Deliberately not SingleInstance: a RabbitMQ channel is not safe to publish on
/// from several threads at once, and a singleton would share one across every
/// concurrent request. Revisiting that needs evidence about the client's own
/// locking, not a lifetime change.
///
/// The registration is inspected rather than resolved: BuildRabbit is an extension
/// method over the real Rabbit plumbing, so activating these types in a unit test
/// would open a connection. Sharing and lifetime are exactly the two things the
/// fix consists of, and they are visible without constructing anything.
/// </remarks>
public class ProducerScopeResolutionShould : UnitTestBase, IDisposable
{
    private readonly IContainer _container;

    public ProducerScopeResolutionShould()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<MessageQueuingModule>();
        builder.RegisterModule<MailModule>();
        _container = builder.Build();
    }

    [Theory]
    [InlineData(typeof(IEventProducer))]
    [InlineData(typeof(IInvokedEventProducer))]
    [InlineData(typeof(IMailSender))]
    public void RegisterProducersAsDisposableAndScopedToTheLifetimeScope(Type service)
    {
        // Спрашиваем ту регистрацию, которую реестр отдаст на разрешение, а не
        // последнюю в перечислении: blanket-скан регистрирует эти же типы, и
        // порядок в Registrations не совпадает с порядком регистрации.
        _container.ComponentRegistry
            .TryGetRegistration(new TypedService(service), out var registration)
            .Should().BeTrue($"{service.Name} must be registered");

        registration!.Sharing.Should().Be(InstanceSharing.Shared,
            "per-dependency means one leased AMQP channel per consumer that asks");
        registration.Lifetime.Should().BeOfType<CurrentScopeLifetime>(
            "a shared channel across concurrent requests is not safe to publish on");

        typeof(IDisposable).IsAssignableFrom(registration.Activator.LimitType)
            .Should().BeTrue("nothing else returns the channel to the pool");
    }

    public void Dispose() => _container.Dispose();
}
