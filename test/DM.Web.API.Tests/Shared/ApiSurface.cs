using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DM.Web.API.Tests.Shared;

// One spelling, so a controller shape none of the rules expected is missed by all of them at once.
internal static class ApiSurface
{
    /// <summary>
    /// How an action is declared: public, on an instance, and on the controller
    /// itself rather than inherited from a base that is not an action of it.
    /// </summary>
    public const BindingFlags ActionBinding =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static readonly Type[] MutatingAttributes =
    [
        typeof(HttpPostAttribute),
        typeof(HttpPutAttribute),
        typeof(HttpPatchAttribute),
        typeof(HttpDeleteAttribute),
    ];

    public static IEnumerable<Type> Controllers() => typeof(Startup).Assembly
        .GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

    public static IEnumerable<MethodInfo> Actions() => Controllers()
        .SelectMany(controller => controller.GetMethods(ActionBinding));

    public static IEnumerable<MethodInfo> RoutedActions() => Actions()
        .Where(action => action.GetCustomAttributes<HttpMethodAttribute>().Any());

    public static IEnumerable<MethodInfo> MutatingActions() => Actions()
        .Where(action => action.GetCustomAttributes()
            .Any(attribute => MutatingAttributes.Contains(attribute.GetType())));

    public static string Named(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";
}
