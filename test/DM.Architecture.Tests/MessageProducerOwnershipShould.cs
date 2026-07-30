using System;
using System.IO;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using FluentAssertions;
using Jamq.Client.Abstractions.Producing;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <summary>
/// Whoever builds a message producer has to be able to release it.
/// </summary>
/// <remarks>
/// BuildRabbit returns a producer that takes an AMQP channel out of the pool on
/// its first send. The channel comes back only when the producer is disposed:
/// ConnectionAdapter counts channels on a semaphore of ChannelsLimit and releases
/// a slot on the channel's ModelShutdown, and RabbitProducer closes the channel
/// only in Dispose. With the shipped defaults — 16 connections of 256 channels —
/// a wrapper that is not disposable exhausts the pool after about four thousand
/// publishes, and from then on every publish in that process throws.
///
/// A rule rather than three tests, because the defect appeared three times
/// independently — the event producer, the mail sender and the notification
/// processor — and each time from the same slip: the producer was built in a
/// constructor and nobody owned it. Anything that takes IProducerBuilder is
/// covered the moment it is written.
///
/// Disposability is the half that lives in the type. The other half is the
/// registration lifetime, asserted in ProducerScopeResolutionShould, which the
/// IL cannot see.
/// </remarks>
public class MessageProducerOwnershipShould
{
    private static readonly ArchitectureModel Solution = new ArchLoader()
        .LoadFilteredDirectory(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .LoadAssembly(typeof(IProducerBuilder).Assembly)
        .Build();

    // Production assemblies only, and only types that can hold what they build.
    // Naming the builder is enough to match, so without the first filter the rule
    // flags the class holding the rule and the Jamq extension class that declares
    // BuildRabbit; without the second it flags the registration extensions, which
    // name the builder in a lambda and own nothing — a static class has no
    // instance to keep a producer in, so it cannot be the one to release it.
    private static readonly IObjectProvider<Class> ProducerOwners = Classes()
        .That().DependOnAny(typeof(IProducerBuilder))
        .And().FollowCustomPredicate(
            c => c.Assembly.Name.StartsWith("DM.", StringComparison.Ordinal)
                 && !c.Assembly.Name.EndsWith(".Tests", StringComparison.Ordinal)
                 && !(c.IsAbstract == true && c.IsSealed == true),
            "are instantiable types declared in a DM production assembly")
        .As("classes that build a message producer");

    /// <summary>
    /// A rule that matches nothing passes. There are three producer owners today,
    /// so a loader that stops resolving the Jamq assembly turns the rule green
    /// instead of red.
    /// </summary>
    [Fact]
    public void FindTheProducerOwners() =>
        ProducerOwners.GetObjects(Solution).Should().HaveCountGreaterOrEqualTo(3);

    /// <summary>
    /// The workers and the seeder are separate executables that no other project
    /// references, so the loader saw neither until this suite referenced them. Two
    /// of the three producer owners live outside the API.
    /// </summary>
    [Fact]
    public void LoadEveryExecutable()
    {
        var assemblies = Solution.Assemblies;
        foreach (var name in new[]
                 {
                     "DM.Web.API",
                     "DM.Workers.Mail",
                     "DM.Workers.NotificationDispatcher",
                     "DM.Tools.Seeder",
                 })
        {
            assemblies.Should().Contain(a => a.Name == name, $"{name} must be in the model");
        }
    }

    [Fact]
    public void KeepEveryProducerOwnerDisposable() =>
        Classes().That().Are(ProducerOwners)
            .Should().ImplementInterface(typeof(IDisposable))
            .Because("the AMQP channel the producer leases comes back to the pool only " +
                     "on Dispose, so a wrapper that cannot be disposed leaks one channel " +
                     "per instance until the pool throws and publishing stops")
            .Check(Solution);
}
