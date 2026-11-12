using System;
using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Web.API.HostedServices;
using DM.Web.API.Realtime;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DM.Web.API.Tests.Shared.Configuration;

// The list is explicit: the migration container runs the same host and starts none.
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

        var registered = new ServiceCollection().AddDmHostedServices(runPeriodicServices: true)
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

    // The twin serves requests next to the primary: a periodic job started on both runs twice.
    [Fact]
    public void RegisterOnlyTheFourWhenPeriodicServicesAreOff()
    {
        var services = new ServiceCollection().AddDmHostedServices(runPeriodicServices: false);
        var hosted = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList();

        hosted.Should().HaveCount(4, "the listener, the warm-up, the index assertion and the queue gauges stay");
        hosted.Select(descriptor => descriptor.ImplementationType).OfType<Type>().Should().BeEquivalentTo(
            [typeof(PushListener), typeof(WarmupService), typeof(ExpressionIndexInitializer)]);
        hosted.Where(descriptor => descriptor.ImplementationFactory is not null).Should()
            .ContainSingle("the queue gauges are the one factory left, the country table refresh is gone");
        services.Should().Contain(descriptor => descriptor.ServiceType.Name == "QueueMetrics",
            "the factory hands out the gauges' singleton");

        Type[] periodic =
        [
            typeof(TokenCleanupService), typeof(SessionCleanupService), typeof(RetentionSweepService),
            typeof(PendingRegistrationCleanupService), typeof(TwoFactorMaintenanceService),
            typeof(PeriodDigestService), typeof(UsernameChangeCleanupService), typeof(PendencyReminderService),
            typeof(GameInactivityService), typeof(PopularityScoreService), typeof(UploadOrphanCleanupService),
        ];
        services.Select(descriptor => descriptor.ImplementationType).Should().NotContain(periodic);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void ReadPeriodicServicesOnByDefault(string? value, bool expected)
    {
        var settings = new Dictionary<string, string?>();
        if (value is not null)
        {
            settings["PeriodicServices:Enabled"] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        HostedServiceExtensions.PeriodicServicesEnabled(configuration).Should().Be(expected);
    }
}
