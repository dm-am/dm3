using System;
using System.Collections.Generic;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;

namespace DM.Infrastructure.Core.Tests;

internal static class BbVisibilityRender
{
    private static readonly IBbParserProvider ParserProvider = new BbParserProvider();

    internal static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid MasterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid OwnerB = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly Guid OtherUser = Guid.Parse("44444444-4444-4444-4444-444444444444");
    internal static readonly Guid GameId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    internal static string Render(string input, BbSurface surface, IAuthorizationSubject viewer)
    {
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = viewer
        };
        return RenderWithContext(input, surface, ctx);
    }

    internal static string RenderText(string input, BbSurface surface, RenderAudience audience)
    {
        var ctx = new RenderContext
        {
            Audience = audience,
            Surface = surface,
            Viewer = null
        };
        return ParserProvider.GetForSurface(surface).RenderText(input, ctx);
    }

    internal static string RenderWithContext(string input, BbSurface surface, RenderContext ctx)
    {
        return ParserProvider.GetForSurface(surface).RenderHtml(input, ctx);
    }

    // The render, or null where the page shows nothing: a tree too deep to build.
    internal static string? RenderOrNull(Func<string> render)
    {
        try
        {
            return render();
        }
        catch (BbParserException)
        {
            return null;
        }
    }

    internal static RenderContext DisplayCtxForGamePost(IAuthorizationSubject viewer) => new()
    {
        Audience = RenderAudience.Display,
        Surface = BbSurface.GamePost,
        Viewer = viewer,
        PostAuthorUserId = AuthorId,
        GameId = GameId
    };

    internal static RenderContext AddressedCtx(IAuthorizationSubject viewer, string name, Guid owner) =>
        DisplayCtxForGamePost(viewer) with
        {
            PrivateAddresseeOwnerUserIdsByAttribute = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
            {
                [name] = new HashSet<Guid> { owner }
            }
        };

    internal static IAuthorizationSubject Viewer(UserRole role, Guid? userId = null) =>
        new TestSubject
        {
            UserId = userId ?? Guid.Parse("99999999-9999-9999-9999-999999999999"),
            Role = role,
            IsAuthenticated = role != UserRole.Guest,
            AccessPolicy = AccessPolicy.NotSpecified
        };
}
