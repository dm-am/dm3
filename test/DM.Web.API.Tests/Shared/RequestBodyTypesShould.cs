using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A request body is its own type, never the response DTO of the same resource.
/// </summary>
/// <remarks>
/// A response DTO as a body asks for fields that do nothing and leaves an Ignore per field between input
/// and domain. The list below is the frozen debt: it may shrink, not grow.
/// </remarks>
public class RequestBodyTypesShould
{
    // Named by action rather than by type: the fix is a per-endpoint request DTO, so the list
    // shrinks one action at a time.
    private static readonly string[] Legacy =
    [
        "CharacterController.PostCharacter",
        "RoomController.PostAccess",
    ];

    private static MethodInfo[] Actions() => ApiSurface.RoutedActions().ToArray();

    private static HashSet<Type> ResponseTypes()
    {
        var types = new HashSet<Type>();
        foreach (var action in Actions())
        {
            foreach (var declared in action.GetCustomAttributes<ProducesResponseTypeAttribute>())
            {
                if (declared.StatusCode >= 400 || declared.Type == null || declared.Type == typeof(void))
                {
                    continue;
                }

                // Envelope<T> and its kin are response shapes by construction;
                // what matters is the resource inside them.
                types.Add(declared.Type);
                if (declared.Type.IsGenericType)
                {
                    foreach (var argument in declared.Type.GetGenericArguments())
                    {
                        types.Add(argument);
                    }
                }
            }
        }

        return types;
    }

    [Fact]
    public void NotBeAResponseType()
    {
        ResponseTypes().Should().NotBeEmpty("the host declares success bodies");

        var offenders = TakingAResponseType()
            .Where(name => !Legacy.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a request body names what the caller may change, see the class remarks");
    }

    [Fact]
    public void LeaveNoStaleNameOnTheLegacyList()
    {
        Legacy.Should().BeSubsetOf(TakingAResponseType(),
            "an entry that outlives the action it exempts is an exemption nobody " +
            "reviewed, waiting for a new action to be given the same name");
    }

    private static string[] TakingAResponseType()
    {
        var responses = ResponseTypes();
        return Actions()
            .Where(action => action.GetParameters()
                .Any(p => p.GetCustomAttribute<FromBodyAttribute>() != null && responses.Contains(p.ParameterType)))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToArray();
    }
}
