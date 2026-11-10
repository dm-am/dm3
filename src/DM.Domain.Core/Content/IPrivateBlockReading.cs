using DM.Domain.Core.Authorization;

namespace DM.Domain.Core.Content;

/// <summary>
/// A text that may hold hidden blocks, with what decides who reads each of them.
/// </summary>
/// <remarks>
/// What the render of a game post is given beside its viewer: the author and the
/// leads read every block, the frozen addressees the blocks they are named in, and
/// either setting opens the addressed blocks to every reader of the room
/// (<see cref="PrivateBlockReaders"/>).
/// </remarks>
/// <param name="GameLeadUserIds">Master and assistants of the game; mentors are not leads.</param>
/// <param name="PrivateAddresseeSnapshotJson">The addressees frozen at save time (<see cref="PrivateAddresseeSnapshot"/>).</param>
/// <param name="GameShowPrivateMessages">The game setting "Показывать всем скрытые блоки".</param>
/// <param name="RoomViewPrivateText">The room setting of the same words.</param>
public sealed record PrivateBlockText(
    string? Source,
    Guid AuthorUserId,
    IReadOnlyCollection<Guid> GameLeadUserIds,
    string? PrivateAddresseeSnapshotJson,
    bool GameShowPrivateMessages,
    bool RoomViewPrivateText);

/// <summary>
/// What the hidden blocks of a text keep from a reader: the blocks themselves, and
/// the uploads the text mentions only inside them.
/// </summary>
/// <remarks>
/// An interface because only the renderer can answer, and this assembly cannot
/// reference the parser: where a block begins and ends is the parser's reading, and
/// who reads it is the visibility filter's. A mention is the identifier of the
/// upload with its dashes or without, in any case, wherever it stands in the text.
/// </remarks>
public interface IPrivateBlockReading
{
    /// <summary>
    /// The uploads of <paramref name="uploadIds"/> the text mentions and the text as
    /// <paramref name="reader"/> reads it mentions nowhere.
    /// </summary>
    /// <remarks>
    /// The rule the read of a file keeps beside its own mark: a picture shown under
    /// the post would say what the block keeps from the reader. An upload the text
    /// does not mention is kept from nobody here.
    /// </remarks>
    /// <param name="reader">Who reads the text; null reads as a guest.</param>
    /// <param name="text">The text and who reads its hidden blocks.</param>
    /// <param name="uploadIds">The uploads asked about.</param>
    IReadOnlySet<Guid> HiddenFrom(
        IAuthorizationSubject? reader, PrivateBlockText text, IReadOnlyCollection<Guid> uploadIds);

    /// <summary>
    /// The uploads of <paramref name="uploadIds"/> the text mentions, and mentions
    /// only inside hidden blocks, whoever their readers are.
    /// </summary>
    /// <param name="source">Raw BBCode of a game post.</param>
    /// <param name="uploadIds">The uploads asked about.</param>
    IReadOnlySet<Guid> OnlyInsideBlocks(string? source, IReadOnlyCollection<Guid> uploadIds);

    /// <summary>
    /// Whether no hidden block of the text is closed to <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">Who reads the text; null reads as a guest.</param>
    /// <param name="text">The text and who reads its hidden blocks.</param>
    bool ReadsEveryBlock(IAuthorizationSubject? reader, PrivateBlockText text);
}
