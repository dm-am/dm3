using AwesomeAssertions;
using Xunit;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <remarks>
/// The rules on owners of a broker producer left with the last one: no letter or event goes
/// through the broker. What stays is the check that the model loads every executable.
/// </remarks>
public class MessageProducerOwnershipShould
{
    private static readonly ArchitectureModel Solution = ArchitectureSolution.Model;

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
}
