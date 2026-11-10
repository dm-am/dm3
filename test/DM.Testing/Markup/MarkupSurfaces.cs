using DM.Infrastructure.Core.Parsing;

namespace DM.Testing.Markup;

/// <summary>
/// The surfaces of content: what the author writes with the tags of a post,
/// and what the checks that go over "every surface" go over.
/// </summary>
/// <remarks>
/// The hint reads one tag of its own and none of a post, so it is not one of them; HintMarkupShould holds
/// it, and EveryBbSurfaceIsDecided asks that a new surface is placed on purpose.
/// </remarks>
public static class MarkupSurfaces
{
    public static readonly BbSurface[] Content =
    [
        BbSurface.GamePost,
        BbSurface.Comment,
        BbSurface.GlobalChatMessage,
        BbSurface.Profile,
        BbSurface.DirectMessage
    ];

    /// <summary>The surfaces with a tag set of their own, which no check of content goes over.</summary>
    public static readonly BbSurface[] Other = [BbSurface.Hint];
}
