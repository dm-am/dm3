using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Reads carry the request cancellation token; writes run to completion: most writes have no transaction,
/// and a dropped connection would tear them.
/// </summary>
public class MutatingActionsShould
{
    [Fact]
    public void NotAcceptACancellationToken()
    {
        var mutating = MutatingActions();

        // A rule that matches nothing passes everything.
        mutating.Should().NotBeEmpty(
            "a walk that finds no writing action has stopped seeing them rather than stopped finding offenders");

        mutating
            .Where(action => action.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .Should().BeEmpty(
                "a cancelled write cannot be rolled back — see the class remarks");
    }

    /// <summary>Every action of the API that writes.</summary>
    private static MethodInfo[] MutatingActions() => ApiSurface.MutatingActions().ToArray();
}
