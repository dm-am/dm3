using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;

namespace DM.Infrastructure.Core.Tests;

internal static class BbAudienceRender
{
    private static readonly IBbParserProvider ParserProvider = new BbParserProvider();

    internal static readonly IAuthorizationSubject Author = BbVisibilityRender.Viewer(UserRole.RegularUser, BbVisibilityRender.AuthorId);

    internal static string Html(BbSurface surface, string bbCode) =>
        ParserProvider.GetForSurface(surface).RenderHtml(bbCode, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = Author,
            PostAuthorUserId = Author.UserId
        });

    internal static string EmbedSafe(BbSurface surface, string bbCode) =>
        ParserProvider.GetSafeForSurface(surface)
        .RenderHtml(bbCode, RenderContext.ForEmbedSafe(surface));

    internal static string Comment(string bbCode) => Html(BbSurface.Comment, bbCode);
}
