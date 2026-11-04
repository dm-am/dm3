using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Which attachments of a post its reader gets.
/// </summary>
/// <remarks>
/// Every one but those its mark keeps from him and those the game text mentions
/// only inside hidden blocks he does not read. The mark is read as a hidden block
/// with the same addressees is (<see cref="PrivateBlockReaders"/>): the author, the
/// leads, the owners chosen, and the settings of the game and the room for a file
/// addressed to anybody. The text is the second rule, kept as a guard: a picture
/// shown under the post would say what the block keeps from him. The files under
/// the post, the address of a file, the pictures in the text and the lists of
/// uploads all ask this, so none can answer one reader differently.
/// </remarks>
public static class PostAttachmentVisibility
{
    /// <summary>
    /// The attachments of <paramref name="addresseesById"/> the post keeps from the reader.
    /// </summary>
    /// <param name="reading">The rule of hidden blocks.</param>
    /// <param name="reader">Who reads the post; null reads as a guest.</param>
    /// <param name="post">The post, as the read of its reader returned it.</param>
    /// <param name="addresseesById">The attachments asked about, each with its stored mark.</param>
    public static IReadOnlySet<Guid> HiddenFrom(
        IPrivateBlockReading reading,
        IAuthorizationSubject? reader,
        Post post,
        IReadOnlyDictionary<Guid, string?> addresseesById)
    {
        var hidden = addresseesById
            .Where(attachment => !ReadsMark(reader, post, PrivateAddresseeSnapshot.ReadList(attachment.Value)))
            .Select(attachment => attachment.Key)
            .ToHashSet();

        var rest = addresseesById.Keys.Where(id => !hidden.Contains(id)).ToArray();
        hidden.UnionWith(reading.HiddenFrom(reader, post.GameTextPrivateBlocks(), rest));
        return hidden;
    }

    /// <summary>The attachments of <paramref name="post"/> it keeps from the reader.</summary>
    public static IReadOnlySet<Guid> HiddenFrom(
        IPrivateBlockReading reading, IAuthorizationSubject? reader, Post post) =>
        HiddenFrom(reading, reader, post, post.Attachments.ToDictionary(a => a.Id, a => a.AddresseesJson));

    /// <summary>Whether the mark of a file lets the reader see it; null is a file for everybody.</summary>
    private static bool ReadsMark(IAuthorizationSubject? reader, Post post, PrivateAddresseeList? addressees) =>
        addressees == null ||
        PrivateBlockReaders.Reads(
            AnonymousIdentity.Of(reader),
            post.AuthorUserId,
            post.GameLeadUserIds,
            addressees.OwnerUserIds,
            addressees.NamesAddressees,
            post.GameShowPrivateMessages,
            post.RoomViewPrivateText);
}
