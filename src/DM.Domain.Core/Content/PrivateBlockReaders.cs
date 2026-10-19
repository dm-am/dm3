namespace DM.Domain.Core.Content;

/// <summary>
/// Who reads what a game post keeps for some of its readers: a hidden block, and
/// a file of the post marked for chosen characters.
/// </summary>
/// <remarks>
/// One rule for both, so a block and a file with the same addressees answer every
/// reader alike (BBCODE_RENDERING.md, "[private]"; UPLOADS.md). The room and the
/// post have been read before this is asked: the overrides open nothing past the
/// room.
/// </remarks>
public static class PrivateBlockReaders
{
    /// <summary>
    /// Whether <paramref name="viewerId"/> reads it.
    /// </summary>
    /// <param name="viewerId">The reader; null for a guest, who matches no id.</param>
    /// <param name="authorId">The author of the post, who reads everything in it.</param>
    /// <param name="leadIds">Master and assistants of the game, who read everything in it.</param>
    /// <param name="addresseeOwnerIds">The owners frozen as its addressees; null when it has none to read.</param>
    /// <param name="namesAddressees">
    /// Whether it is addressed to characters at all. One that is not, a bare or a
    /// broken block and a file marked for nobody, is for the author and the leads
    /// alone: the overrides share what was addressed to characters.
    /// </param>
    /// <param name="gameShowsAll">The game setting "Показывать всем скрытые блоки".</param>
    /// <param name="roomShowsAll">The room setting "Показывать всем скрытые блоки".</param>
    public static bool Reads(
        Guid? viewerId,
        Guid? authorId,
        IReadOnlyCollection<Guid> leadIds,
        IReadOnlySet<Guid>? addresseeOwnerIds,
        bool namesAddressees,
        bool gameShowsAll,
        bool roomShowsAll)
    {
        if (viewerId is { } viewer &&
            (viewer == authorId || leadIds.Contains(viewer) || addresseeOwnerIds?.Contains(viewer) == true))
            return true;

        return namesAddressees && (gameShowsAll || roomShowsAll);
    }
}
