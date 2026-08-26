namespace DM.Domain.Core.Content;

/// <summary>
/// How long a body of user-written BBCode may be, wherever it is checked.
/// </summary>
/// <remarks>
/// One number rather than one per surface, because it answers one question -
/// how much text the server agrees to parse and every reader of the page agrees
/// to pay for - and the validators that ask it cannot read each other. Private
/// messages were the only surface that had an answer; posts and comments had
/// none at all, so nothing but the request size limit stood between an author
/// and a body the parser walks on every open of the page. The parse and the
/// render are linear now, but linear in an unbounded input is still unbounded.
///
/// The figure is the one messages were already saved with, so no body that
/// saves today stops saving: what changes is that two surfaces which promised
/// nothing now promise the same thing as the third.
/// </remarks>
public static class BodyTextLimits
{
    /// <summary>
    /// The longest body a post, a comment or a message may be saved with.
    /// </summary>
    public const int MaxLength = 50000;
}
