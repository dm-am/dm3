using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Web.API.Shared.Authentication;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// An action marked [AllowAnonymous] is reachable without a session.
/// </summary>
/// <remarks>
/// [AllowAnonymous] is metadata, and this project authorises with a filter of its own, which must read it.
/// Checked on the filter: the defect is the filter's.
/// </remarks>
public class AnonymousActionsShould
{
    private static AuthorizationFilterContext ContextFor(params object[] metadata)
    {
        var descriptor = new ActionDescriptor { EndpointMetadata = metadata.ToList() };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            new DefaultHttpContext(), new RouteData(), descriptor);
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    /// <summary>The filter the attribute applies, made the way MVC makes it.</summary>
    private static IAuthorizationFilter Filter(bool authenticated)
    {
        var identityProvider = Substitute.For<IIdentityProvider>();
        identityProvider.Current.Returns(
            authenticated ? Identities.User(Guid.NewGuid(), "reader") : Identities.Guest());

        using var services = new ServiceCollection().AddSingleton(identityProvider).BuildServiceProvider();
        return (IAuthorizationFilter)new AuthenticationRequiredAttribute().CreateInstance(services);
    }

    [Fact]
    public void LetAnAnonymousCallerThrough()
    {
        var context = ContextFor(new AllowAnonymousAttribute());

        Filter(authenticated: false).OnAuthorization(context);

        context.Result.Should().BeNull(
            "the attribute exists to open exactly this door, and a link from a letter " +
            "is opened by somebody who is not signed in");
    }

    [Fact]
    public void RefuseAnAnonymousCallerWithoutTheAttribute()
    {
        var context = ContextFor();

        Action refusal = () => Filter(authenticated: false).OnAuthorization(context);

        refusal.Should().Throw<HttpException>("this is the rule the filter exists for")
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        context.Result.Should().BeNull(
            "the refusal is thrown and not shaped here: ErrorHandlingMiddleware writes the " +
            "body of every one of them, which is what puts the correlation token in");
    }

    [Fact]
    public void LetAnAuthenticatedCallerThrough()
    {
        var context = ContextFor();

        Filter(authenticated: true).OnAuthorization(context);

        context.Result.Should().BeNull();
    }
}
