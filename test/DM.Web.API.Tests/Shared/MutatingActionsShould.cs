using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A write in this codebase is not atomic by default. Only a handful of writes
/// open a transaction, so a multi-step one — accepting an invitation, which
/// grants the right and then burns the token — has no rollback of its own.
/// Handing such an action the request cancellation token turns a dropped
/// connection into a torn write: rights granted by an invitation that stayed
/// redeemable.
///
/// Reads carry the token; writes run to completion. This test is the rule.
/// </summary>
public class MutatingActionsShould
{
    private static readonly Type[] MutatingAttributes =
    [
        typeof(HttpPostAttribute),
        typeof(HttpPutAttribute),
        typeof(HttpPatchAttribute),
        typeof(HttpDeleteAttribute),
    ];

    [Fact]
    public void NotAcceptACancellationToken()
    {
        var mutating = MutatingActions();

        // A rule that matches nothing passes everything. This walk finds its
        // subjects by assembly, base type and attribute, and each of those is a
        // thing that can change without anybody thinking about this file - the
        // controllers moving to a base of their own, the attributes gaining a
        // wrapper - after which the rule goes green by finding nobody to check.
        mutating.Should().HaveCountGreaterThan(100,
            "the API writes through a couple of hundred actions, and a walk that finds far " +
            "fewer has stopped seeing them rather than stopped finding offenders");

        mutating
            .Where(action => action.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .Should().BeEmpty(
                "a cancelled write cannot be rolled back — see the class remarks");
    }

    /// <summary>Every action of the API that writes.</summary>
    private static MethodInfo[] MutatingActions() => typeof(Startup).Assembly
        .GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.GetCustomAttributes()
            .Any(a => MutatingAttributes.Contains(a.GetType())))
        .ToArray();
}
