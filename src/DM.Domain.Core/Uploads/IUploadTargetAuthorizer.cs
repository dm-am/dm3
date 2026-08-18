using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Decides whether the current user may attach an upload to a given entity.
/// </summary>
/// <remarks>
/// One implementation per <see cref="UploadType"/>, each in the module that owns
/// the entity the target points at: the rule for a character avatar is the rule
/// for editing that character, and it belongs where that rule already lives.
///
/// The upload endpoint used to check only that the target was not null, so any
/// authenticated user could name any character or post. Deny is the default: an
/// upload type with no authorizer is refused rather than accepted, so adding a
/// type without a rule fails closed.
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
    /// </remarks>
    /// <param name="targetId">Identifier of the entity the upload points at</param>
    Task EnsureReadAllowedAsync(Guid targetId);

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
