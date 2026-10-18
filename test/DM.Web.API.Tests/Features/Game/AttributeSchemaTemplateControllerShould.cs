using System.Linq;
using System.Reflection;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Game.AttributeSchemaTemplates;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Tests.Shared;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// The catalogue of starting character sheets is read by everybody and written
/// by the senior moderation.
/// </summary>
/// <remarks>
/// Asked of the attributes rather than of a running host, because that is where
/// the rule lives: the rank is declared on each action, and an action added
/// later without one answers whoever asks. The service asks the same question a
/// second time, and that half is covered by its own tests - this one is about
/// the door.
/// </remarks>
public class AttributeSchemaTemplateControllerShould
{
    private static MethodInfo[] Actions() =>
        typeof(AttributeSchemaTemplateController)
            .GetMethods(ApiSurface.ActionBinding)
            .Where(action => action.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToArray();

    private static MethodInfo[] Writes() => Actions()
        .Where(action => action.GetCustomAttributes()
            .Any(attribute => ApiSurface.MutatingAttributes.Contains(attribute.GetType())))
        .ToArray();

    private static MethodInfo[] Reads() => Actions()
        .Where(action => action.GetCustomAttributes<HttpGetAttribute>().Any())
        .ToArray();

    [Fact]
    public void DeclareTheWritesTheDesignGivesIt()
    {
        Writes().Should().HaveCount(4,
            "the catalogue is created, replaced, hidden and pointed at, and a walk " +
            "that finds fewer is checking less than it claims to");
        Reads().Should().HaveCount(2, "the catalogue is read as a list and one at a time");
    }

    [Fact]
    public void GateEveryWriteBySeniorModeration()
    {
        var ungated = Writes()
            .Where(action => action.GetCustomAttributes<RequireRoleAttribute>()
                .All(attribute => attribute.Arguments?.FirstOrDefault() is not UserRole.SeniorModerator))
            .Select(ApiSurface.Named)
            .ToArray();

        ungated.Should().BeEmpty(
            "a write with no rank on it answers whoever asks, under the guest identity");
    }

    [Fact]
    public void LeaveEveryReadOpen()
    {
        var gated = Reads()
            .Where(action => action.GetCustomAttributes()
                .Any(attribute => attribute is AuthenticationRequiredAttribute or RequireRoleAttribute))
            .Select(ApiSurface.Named)
            .ToArray();

        gated.Should().BeEmpty(
            "the creation form fills itself from the catalogue, and it is open to " +
            "everybody who can reach the page behind it");
    }
}
