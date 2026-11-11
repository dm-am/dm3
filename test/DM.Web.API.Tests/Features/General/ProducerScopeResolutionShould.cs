using System;
using System.Linq;
using DM.Domain.Core.Events;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

// The producers take scoped services: the event producer the request's DbContext, the mail sender
// the validator of its letter.
public class ProducerScopeResolutionShould
{
    private readonly IServiceCollection _services;

    public ProducerScopeResolutionShould()
    {
        var services = new ServiceCollection();
        services.AddDmMail();
        services.AddDmPersistence();
        _services = services;
    }

    // The registration a resolution answers with: MS.DI resolves the last descriptor of the service
    // type.
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
            "per-dependency means one DbContext of its own per consumer that asks, and a " +
            "singleton would capture one context across every concurrent request");
    }
}
