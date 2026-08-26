using System;
using System.IO;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using AwesomeAssertions;
using DM.Infrastructure.Messaging;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <summary>
/// Whoever builds a message producer has to be able to release it.
/// </summary>
/// <remarks>
/// The builder returns a producer that opens an AMQP channel on its first send
/// and closes it only in Dispose. A wrapper that is not disposable therefore
/// leaks one channel per instance for the life of the process, until the
/// broker's channel ceiling turns every further publish into an exception.
///
/// A rule rather than three tests, because the defect appeared three times
/// independently — the event producer, the mail sender and the notification
/// processor — and each time from the same slip: the producer was built in a
/// constructor and nobody owned it. Anything that takes IDmProducerBuilder is
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
        .Build();

    // Production assemblies only, and only types that can hold what they build.
    // Naming the builder is enough to match, so without the assembly filter the
    // rule flags the class holding the rule; without the static-class filter it
    // flags the registration extensions, which name the builder in a lambda and
    // own nothing — a static class has no instance to keep a producer in, so it
    // cannot be the one to release it. The builder implementation is excluded
    // the same way: it hands producers out and holds none.
    private static readonly IObjectProvider<Class> ProducerOwners = Classes()
        .That().DependOnAny(typeof(IDmProducerBuilder))
        .And().FollowCustomPredicate(
            c => c.Assembly.Name.StartsWith("DM.", StringComparison.Ordinal)
                 && !c.Assembly.Name.EndsWith(".Tests", StringComparison.Ordinal)
                 && !(c.IsAbstract == true && c.IsSealed == true)
                 // Named as a string because the builder implementation is
                 // internal to the messaging assembly.
                 && c.FullName != "DM.Infrastructure.Messaging.DmProducerBuilder",
            "are instantiable types declared in a DM production assembly, " +
            "not the messaging plumbing itself")
        .As("classes that build a message producer");

    /// <summary>
    /// A rule that matches nothing passes. There are three producer owners today,
    /// so a loader that stops resolving the messaging assembly turns the rule
    /// green instead of red.
    /// </summary>
    [Fact]
    public void FindTheProducerOwners() =>
        ProducerOwners.GetObjects(Solution).Should().HaveCountGreaterThanOrEqualTo(3);

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
            .Because("the AMQP channel the producer opens is closed only on Dispose, " +
                     "so a wrapper that cannot be disposed leaks one channel per " +
                     "instance until the broker's ceiling stops publishing altogether")
            .Check(Solution);
}
