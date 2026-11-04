using System;
using System.Linq;
using DM.Domain.Core.Events;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the lifetimes of the two producers a request resolves.
/// </summary>
/// <remarks>
/// The mail sender owns an AMQP channel, so it is disposable and scoped, not transient and not a singleton
/// (a channel is not thread-safe). The event producer writes with the one DmDbContext of the scope.
/// Inspected rather than resolved: activating either would dial a dependency.
/// </remarks>
public class ProducerScopeResolutionShould
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
    /// The registration a resolution answers with: MS.DI resolves the last descriptor of the service type.
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
    /// The channel half of the rule, which only the mail sender carries.
    /// </summary>
    [Fact]
    public void KeepTheMailSenderDisposable()
    {
        var implementation = EffectiveRegistration(typeof(IMailSender)).ImplementationType;

        implementation.Should().NotBeNull("the sender is registered by type");
        typeof(IDisposable).IsAssignableFrom(implementation)
            .Should().BeTrue("nothing else closes the channel");
    }
}
