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
/// PATTERNS used to answer the same question twice and differently: the layer
/// section allowed cross-module contracts in Domain.Core, the FAQ said modules
/// may not call each other at all and that communication is events only. The
/// code followed the first and ignored the second, in twenty-two files of six
/// modules. The document now states one rule — synchronous reading through a
/// kernel contract, writing through events — and says both halves are checked.
///
/// The reading half is DomainBoundaryShould: no module names another, in either
/// direction. This is the writing half. Its shape is the signal: a member that
/// hands nothing back is being called for its effect, and an effect on somebody
/// else's area asked for synchronously is exactly what the events exist to carry
/// — the caller waits for it, shares its failure, and the owner never gets to
/// decide what to do about it.
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
    /// <remarks>
    /// Property accessors are skipped. Some of these contracts are DTO shapes
    /// (ILikable is the like counter four DTOs carry) and a settable property on
    /// one is not a module being told to do something.
    /// </remarks>
    private static bool IsCommand(MethodInfo method) =>
        !method.IsSpecialName &&
        !CheckVerbs.Any(verb => method.Name.StartsWith(verb, StringComparison.Ordinal)) &&
        (method.ReturnType == typeof(void) || method.ReturnType == typeof(Task));
}
