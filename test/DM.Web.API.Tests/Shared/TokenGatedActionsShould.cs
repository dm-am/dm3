using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.Http;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A token that is itself the caller's credential travels in a header, never in
/// the path.
/// </summary>
/// <remarks>
/// A path is written verbatim into the reverse-proxy access log, into the request
/// log and into the trace, so a token placed there is disclosed by construction to
/// everyone who can read any of the three, and rotating it means purging logs
/// rather than changing a value. Eight endpoints took theirs from the path —
/// password reset, activation, email change confirmation, username change approval,
/// guest ticket tracking — which put a working password-reset credential into the
/// access log for the whole lifetime of the token, next to a body carrying the new
/// password. API_DESIGN states the rule; this is what keeps it true, because a new
/// endpoint is written by copying its neighbour.
///
/// Two halves, and both are needed. The route must not name the token, and the
/// action must still read one: the first check alone is satisfied by an endpoint
/// that stopped taking a token at all.
/// </remarks>
public class TokenGatedActionsShould
{
    /// <summary>The headers a token-gated action may take its credential from.</summary>
    private static readonly string[] CredentialHeaders = [TokenHeaders.Account, TokenHeaders.Ticket];

    /// <summary>Every controller action in the host.</summary>
    private static MethodInfo[] Actions() => ApiSurface.RoutedActions().ToArray();

    [Fact]
    public void NotNameTheTokenInTheRoute()
    {
        var offenders = new List<string>();

        foreach (var action in Actions())
        {
            // Both levels count: the verb attribute carries the action's own
            // template, and the controller's [Route] carries the prefix.
            var named = action.GetCustomAttributes(true).OfType<IRouteTemplateProvider>()
                .Concat(action.DeclaringType!.GetCustomAttributes(true).OfType<IRouteTemplateProvider>())
                .Select(p => p.Template)
                .Where(t => t != null && t.Contains("{token", StringComparison.OrdinalIgnoreCase));

            offenders.AddRange(named.Select(t => $"{action.DeclaringType!.Name}.{action.Name} -> {t}"));
        }

        offenders.Should().BeEmpty(
            "a path is written verbatim into the access log and into the trace - see the class remarks");
    }

    [Fact]
    public void TakeTheTokenFromItsHeader()
    {
        var carried = Actions()
            .SelectMany(m => m.GetParameters().Select(p => (Action: m, Parameter: p)))
            .Where(x => x.Parameter.Name == "token")
            .ToArray();

        // Without this the rule above could be met by an endpoint that stopped
        // asking for a credential rather than by one that moved it.
        carried.Should().NotBeEmpty("the token-gated endpoints still take a token");

        var offenders = carried
            .Where(x => x.Parameter.GetCustomAttribute<FromHeaderAttribute>()?.Name is not string name ||
                        !CredentialHeaders.Contains(name))
            .Select(x => $"{x.Action.DeclaringType!.Name}.{x.Action.Name}")
            .OrderBy(n => n)
            .ToArray();

        offenders.Should().BeEmpty(
            "the token is the credential of the call, and a credential travels in a header");
    }
}
