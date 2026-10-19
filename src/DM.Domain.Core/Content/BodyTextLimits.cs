namespace DM.Domain.Core.Content;

/// <summary>
/// How long a body of user-written BBCode may be, wherever it is checked.
/// </summary>
/// <remarks>
/// One number for every surface: it bounds the text the parser walks on every
/// open of the page.
/// </remarks>
public static class BodyTextLimits
{
    /// <summary>
    /// The longest body a post, a comment or a message may be saved with.
    /// </summary>
    public const int MaxLength = 50000;
}
