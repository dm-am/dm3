using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A write in this codebase is not atomic. There are three BeginTransaction calls
/// in the whole persistence layer, so a multi-step write — the two-phase PublicId
/// assignment on blogs and games, accepting an invitation and then burning the
/// token — has no rollback of its own. Handing such an action the request
/// cancellation token turns a dropped connection into a torn write: a game with a
/// placeholder PublicId nothing recomputes, an invitation that granted rights and
/// stayed redeemable.
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
        var offenders = typeof(Startup).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes()
                .Any(a => MutatingAttributes.Contains(a.GetType())))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .OrderBy(n => n)
            .ToArray();

        offenders.Should().BeEmpty(
            "a cancelled write cannot be rolled back — see the class remarks");
    }
}
