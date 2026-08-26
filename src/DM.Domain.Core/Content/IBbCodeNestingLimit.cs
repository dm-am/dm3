namespace DM.Domain.Core.Content;

/// <summary>
/// Whether a piece of BBCode is shallow enough for the renderer to build a tree
/// for it.
/// </summary>
/// <remarks>
/// The renderer refuses text nested past a fixed depth, and the refusal used to
/// be invisible from both ends: nothing on the save path looked, so the post was
/// stored, and from then on every reader but the author got an empty field with
/// no error and no marker while the author, whose own view shows the source,
/// saw their text and could not reproduce the complaint.
///
/// It is an interface rather than a function on a static class because the only
/// honest answer comes from the renderer itself — anything else is a second
/// implementation of its depth counting, which would have to be kept equal to it
/// by hand. This assembly is the architecture centre and cannot reference the
/// parser, so the check is declared here and implemented next to it.
/// </remarks>
public interface IBbCodeNestingLimit
{
    /// <summary>
    /// Whether the renderer will accept this text. Empty text always will.
    /// </summary>
    /// <param name="source">Raw BBCode as the author submitted it</param>
    bool IsWithinLimit(string? source);
}
