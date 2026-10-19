using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A module reads another module through the kernel and writes to it through an
/// event, and the kernel contracts one module implements for another only read.
/// </summary>
/// <remarks>
/// The reading half is DomainBoundaryShould; this is the writing half. A member that hands
/// nothing back is called for its effect, which is what events carry.
/// </remarks>
public class CrossModuleContractShould
{
    private const string Kernel = "DM.Domain.Core";

    private static Assembly[] DomainAssemblies() => Directory
        .GetFiles(AppContext.BaseDirectory, "DM.Domain.*.dll", SearchOption.TopDirectoryOnly)
        .Select(Assembly.LoadFrom)
        .ToArray();

    /// <summary>
    /// Kernel interfaces a domain module implements: the contracts one module
    /// publishes for the others through the architecture centre.
    /// </summary>
    private static Type[] ImplementedByAModule() => DomainAssemblies()
        .Where(assembly => assembly.GetName().Name != Kernel)
        .SelectMany(assembly => assembly.GetTypes())
        .SelectMany(type => type.GetInterfaces())
        .Where(contract => contract.Assembly.GetName().Name == Kernel)
        .Distinct()
        .OrderBy(contract => contract.FullName, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// A rule over an empty set passes, and this set is discovered from the
    /// assemblies.
    /// </summary>
    [Fact]
    public void FindTheContractsOneModulePublishesForTheOthers() =>
        ImplementedByAModule().Should().NotBeEmpty(
            "the kernel exists so that a module can answer another one's question, and " +
            "finding no such contract means this rule reads nothing");

    [Fact]
    public void AnswerQuestionsRatherThanTakeOrders()
    {
        var offenders = ImplementedByAModule()
            .SelectMany(contract => contract
                .GetMethods()
                .Where(IsCommand)
                .Select(method => $"{contract.Name}.{method.Name}"))
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a kernel contract implemented by a module is how the other modules read its " +
            "area; a member that returns nothing is asked for its effect, and an effect on " +
            "another module's area belongs in an event its owner decides what to do with");
    }

    /// <summary>
    /// Verbs that ask a question and answer it by throwing. The answer is still
    /// an answer: nothing of the owner's is changed by asking.
    /// </summary>
    private static readonly string[] CheckVerbs = ["Ensure", "ThrowIf", "Validate", "Assert"];

    /// <summary>
    /// A member called for its effect rather than for its answer: nothing comes
    /// back, or the only thing that comes back is the completion.
    /// </summary>
    /// <remarks>Property accessors are skipped: some contracts are DTO shapes.</remarks>
    private static bool IsCommand(MethodInfo method) =>
        !method.IsSpecialName &&
        !CheckVerbs.Any(verb => method.Name.StartsWith(verb, StringComparison.Ordinal)) &&
        (method.ReturnType == typeof(void) || method.ReturnType == typeof(Task));
}
