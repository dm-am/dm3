using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Shared.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// An action marked [AllowAnonymous] is reachable without a session.
/// </summary>
/// <remarks>
/// [AllowAnonymous] is metadata. The framework's own authorization middleware
/// reads it; this project authorises with a filter of its own, and that filter
/// read nothing but the current identity. So the attribute did nothing, and the
/// actions it sits on are the ones a person opens from their mailbox — confirming
/// an email change, finishing a username change — often in a browser where they
/// are not signed in. They answered 401 and the letter looked broken.
///
/// Checked on the filter rather than over HTTP because the defect is the filter's:
/// an integration test would need the whole stack to assert one branch.
/// </remarks>
public class AnonymousActionsShould : UnitTestBase
{
    private static AuthorizationFilterContext ContextFor(bool authenticated, params object[] metadata)
    {
        var descriptor = new ActionDescriptor { EndpointMetadata = metadata.ToList() };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            new DefaultHttpContext(), new RouteData(), descriptor);
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    /// <summary>
    /// The filter is private to the attribute, the way a filter should be. The
    /// test reaches it through the attribute's own declaration rather than
    /// widening its visibility for testing.
    /// </summary>
    private static IAuthorizationFilter Filter(bool authenticated)
    {
        var identityProvider = new Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(
            authenticated ? Identities.User(Guid.NewGuid(), "reader") : Identities.Guest());

        var type = typeof(AuthenticationRequiredAttribute)
            .GetNestedType("AuthenticationRequiredFilter", BindingFlags.NonPublic);
        type.Should().NotBeNull("the attribute declares the filter it applies");

        return (IAuthorizationFilter)Activator.CreateInstance(type!, identityProvider.Object)!;
    }

    [Fact]
    public void LetAnAnonymousCallerThrough()
    {
        var context = ContextFor(authenticated: false, new AllowAnonymousAttribute());

        Filter(authenticated: false).OnAuthorization(context);

        context.Result.Should().BeNull(
            "the attribute exists to open exactly this door, and a link from a letter " +
            "is opened by somebody who is not signed in");
    }

    [Fact]
    public void RefuseAnAnonymousCallerWithoutTheAttribute()
    {
        var context = ContextFor(authenticated: false);

        Filter(authenticated: false).OnAuthorization(context);

        context.Result.Should().NotBeNull("this is the rule the filter exists for");
    }

    [Fact]
    public void LetAnAuthenticatedCallerThrough()
    {
        var context = ContextFor(authenticated: true);

        Filter(authenticated: true).OnAuthorization(context);

        context.Result.Should().BeNull();
    }
}
