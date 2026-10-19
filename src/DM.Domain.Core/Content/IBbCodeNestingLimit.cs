namespace DM.Domain.Core.Content;

/// <summary>
/// Whether a piece of BBCode is shallow enough for the renderer to build a tree
/// for it.
/// </summary>
/// <remarks>
/// An interface because only the renderer can answer, and this assembly cannot
/// reference the parser.
/// </remarks>
public interface IBbCodeNestingLimit
{
    /// <summary>
    /// Whether the renderer will accept this text. Empty text always will.
    /// </summary>
    /// <param name="source">Raw BBCode as the author submitted it</param>
    bool IsWithinLimit(string? source);
}
