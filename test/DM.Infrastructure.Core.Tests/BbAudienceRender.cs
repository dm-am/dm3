using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The rendering of a text on behalf of its author, for each audience; the tag tests read it through
/// using static.
/// </summary>
internal static class BbAudienceRender
{
    private static readonly IBbParserProvider ParserProvider = new BbParserProvider();

    internal static readonly IAuthorizationSubject Author = BbVisibilityRender.Viewer(UserRole.RegularUser, BbVisibilityRender.AuthorId);

    /// <summary>What the author of the text reads, private text included.</summary>
    internal static string Html(BbSurface surface, string bbCode) =>
        ParserProvider.GetForSurface(surface).RenderHtml(bbCode, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = Author,
            PostAuthorUserId = Author.UserId
        });

    /// <summary>The rendering that is safe to embed anywhere.</summary>
    internal static string EmbedSafe(BbSurface surface, string bbCode) =>
        ParserProvider.GetSafeForSurface(surface)
        .RenderHtml(bbCode, RenderContext.ForEmbedSafe(surface));

    internal static string Comment(string bbCode) => Html(BbSurface.Comment, bbCode);
}
