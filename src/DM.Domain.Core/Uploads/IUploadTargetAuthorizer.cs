using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Decides whether the current user may attach an upload to a given entity.
/// </summary>
/// <remarks>
/// One implementation per <see cref="UploadType"/>, each in the module that owns
/// the entity the target points at: the rule for a character avatar is the rule
/// for editing that character, and it belongs where that rule already lives.
/// An upload type with no authorizer is refused, so a new type fails closed.
/// </remarks>
public interface IUploadTargetAuthorizer
{
    /// <summary>
    /// The upload type this authorizer decides
    /// </summary>
    UploadType Type { get; }

    /// <summary>
    /// Throws unless the current user may attach an upload to the target
    /// </summary>
    /// <param name="targetId">Identifier of the entity the upload points at</param>
    Task EnsureAllowedAsync(Guid targetId);

    /// <summary>
    /// Throws unless the current user may read the bytes of an upload attached to
    /// the target.
    /// </summary>
    /// <remarks>
    /// Asked on every request for the file, not once when a link is handed out: a
    /// URL that grants access by being known is a pass to whoever ends up holding
    /// it, and the closed prefixes exist precisely so that no such pass is issued.
    ///
    /// Refuses with 404 rather than 403 wherever the target itself would be
    /// invisible. A 403 on a private game's attachment answers the question the
    /// closed room was keeping shut.
    ///
    /// The upload is named because the files on one entity are not always read
    /// by the same people: a post attachment marked for chosen characters, or one
    /// the game text shows only inside a hidden block, is read by those the mark
    /// and the block are open to. A type whose files are all read alike leaves it
    /// unread. A file the target keeps is refused with the same 404 as an
    /// invisible target.
    /// </remarks>
    /// <param name="upload">The upload, whose target is set</param>
    Task EnsureReadAllowedAsync(StoredUpload upload);

    /// <summary>
    /// Which of <paramref name="uploads"/> the target keeps from the current user,
    /// whom the file's own right already lets past the entity: its owner, or Moderator+.
    /// </summary>
    /// <remarks>
    /// That right passes the room, not what the entity keeps from all its readers
    /// but some. A post attachment marked for chosen characters, or shown only
    /// inside a hidden block, is closed to Moderator+ as the block is: moderation
    /// has no way around a hidden block, and the address of a file is no way
    /// around it either. A file whose target is gone is kept: nothing is left to
    /// say it was not. One answer for a page of uploads, so a list asks once.
    /// </remarks>
    /// <param name="uploads">Uploads of this type, each with its target set</param>
    Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads);

    /// <summary>
    /// Whether a guest reads the bytes of an upload attached to the target, so that
    /// every caller is answered alike.
    /// </summary>
    /// <remarks>
    /// Asked of a file already given to the current user, to decide whether the
    /// browser may keep it: a copy kept for one account is handed to the next one
    /// signed in the same browser, so only a file everybody reads is kept.
    /// </remarks>
    /// <param name="upload">The upload, whose target is set</param>
    Task<bool> OpenToGuestAsync(StoredUpload upload);

    /// <summary>
    /// The mark to store for a file of the target seen by <paramref name="audience"/>,
    /// or null for a file everybody who reads the target sees.
    /// </summary>
    /// <remarks>
    /// Asked of a caller already let attach to the target (<see cref="EnsureAllowedAsync"/>).
    /// The upload service refuses a chosen audience for every type but a post
    /// attachment, and a type without one throws on it rather than store it as everybody.
    /// </remarks>
    /// <param name="targetId">Identifier of the entity the upload points at</param>
    /// <param name="uploadId">The file whose mark changes; null for a file not stored yet</param>
    /// <param name="audience">Who the uploader chose</param>
    Task<string?> FreezeAudienceAsync(Guid targetId, Guid? uploadId, UploadAudience audience);

    /// <summary>
    /// Whether the current user may take an upload off the target, beyond the
    /// file's owner and Moderator+ that the upload's own intention already admits.
    /// </summary>
    /// <remarks>
    /// An answer rather than a refusal, because the caller composes it with that
    /// intention: a false here is not "no", it is "no extra right from the entity
    /// this file hangs on".
    /// </remarks>
    /// <param name="targetId">Identifier of the entity the upload points at</param>
    Task<bool> MayDetachAsync(Guid targetId);
}
