using DM.Infrastructure.Core.Parsing;

namespace DM.Testing.Markup;

/// <summary>
/// The surfaces of content: what the author writes with the tags of a post,
/// and what the checks that go over "every surface" go over.
/// </summary>
/// <remarks>
/// Not every value of <see cref="BbSurface"/>. The hint, the description of a
/// game tag, reads one tag of its own ([tip]) and none of a post: a generated
/// post is prose there, and the corpus the client reads would move by a
/// surface for every text if it were one of them. Its rows are in the grammar
/// table under its own name, and its markup is held by HintMarkupShould. A
/// surface added later has to be put here or next to the hint on purpose,
/// which EveryBbSurfaceIsDecided in DM.Infrastructure.Core.Tests asks.
/// </remarks>
public static class MarkupSurfaces
{
    /// <summary>The five surfaces of content, in the order of the enumeration.</summary>
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
