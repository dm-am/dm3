using System;
using System.Linq;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DM.Web.API.Tests.Shared.Configuration;

/// <summary>
/// Every background service the API host declares is started by it.
/// </summary>
/// <remarks>
/// The services are registered by hand, one line each, and the list has to stay
/// explicit: the migration container runs the same host and must start none of
/// them. A service written and left out of the list compiles, passes every test
/// of its own, and never runs; a nightly cleanup that never runs looks exactly
/// like a night with nothing to clean.
///
/// Only the types of this assembly are compared: the list also starts services
/// of the infrastructure modules, which declare their own and are started by
/// whichever host needs them.
/// </remarks>
public class HostedServiceRegistrationShould
{
    [Fact]
    public void StartEveryBackgroundServiceTheHostDeclares()
    {
        var assembly = typeof(HostedServiceExtensions).Assembly;

        var declared = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(IHostedService).IsAssignableFrom(type))
            .ToList();

        var registered = new ServiceCollection().AddDmHostedServices()
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type is not null && type.Assembly == assembly)
            .Select(type => type!)
            .ToList();

        declared.Should().NotBeEmpty("the host runs background services, and a walk that finds none checks nothing");
        registered.Should().OnlyHaveUniqueItems("a service registered twice runs twice");
        registered.Should().BeEquivalentTo(declared,
            "a background service of this host that is not in the list is one somebody wrote and " +
            "nothing ever starts");
    }
}
