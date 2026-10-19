using System;
using ArchUnitNET.Domain;
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
/// A producer opens an AMQP channel on first send and closes it only in Dispose. The registration
/// lifetime is ProducerScopeResolutionShould's.
/// </remarks>
public class MessageProducerOwnershipShould
{
    private static readonly ArchitectureModel Solution = ArchitectureSolution.Model;

    // Production assemblies only, non-static classes only, the builder implementation left out:
    // each of those names the builder and owns no producer.
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
    /// A rule that matches nothing passes, so the owners are counted first.
    /// </summary>
    [Fact]
    public void FindTheProducerOwners() =>
        ProducerOwners.GetObjects(Solution).Should().NotBeEmpty();

    /// <summary>
    /// The workers and the seeder are executables no other project references.
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
