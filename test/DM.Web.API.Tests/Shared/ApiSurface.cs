using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// The actions the host exposes, asked of the assembly rather than listed.
/// </summary>
/// <remarks>
/// Rules about the whole surface — what a write may take, what a stranger may
/// reach, what a cache header says — all begin by finding every action there is,
/// and each of them used to spell that search out again. One spelling, so that a
/// controller shape none of them expected is missed by all of them at once
/// rather than by some of them silently.
/// </remarks>
internal static class ApiSurface
{
    /// <summary>
    /// How an action is declared: public, on an instance, and on the controller
    /// itself rather than inherited from a base that is not an action of it.
    /// </summary>
    public const BindingFlags ActionBinding =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>The verbs that write.</summary>
    public static readonly Type[] MutatingAttributes =
    [
        typeof(HttpPostAttribute),
        typeof(HttpPutAttribute),
        typeof(HttpPatchAttribute),
        typeof(HttpDeleteAttribute),
    ];

    /// <summary>Every controller the host can route to.</summary>
    public static IEnumerable<Type> Controllers() => typeof(Startup).Assembly
        .GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

    /// <summary>Every method a controller declares.</summary>
    public static IEnumerable<MethodInfo> Actions() => Controllers()
        .SelectMany(controller => controller.GetMethods(ActionBinding));

    /// <summary>Every action that answers a verb.</summary>
    public static IEnumerable<MethodInfo> RoutedActions() => Actions()
        .Where(action => action.GetCustomAttributes<HttpMethodAttribute>().Any());

    /// <summary>Every action that writes.</summary>
    public static IEnumerable<MethodInfo> MutatingActions() => Actions()
        .Where(action => action.GetCustomAttributes()
            .Any(attribute => MutatingAttributes.Contains(attribute.GetType())));

    /// <summary>An action under the name the rules of this tier report it by.</summary>
    public static string Named(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";
}
