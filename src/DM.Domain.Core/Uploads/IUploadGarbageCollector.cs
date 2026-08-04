using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Retires the uploads a target has replaced: the newest one stays, the rest are
/// soft-deleted and handed to the background sweeper together with the grace
/// period that lets the owner change their mind.
///
/// Called from <c>UserService</c> when an avatar is linked or unlinked.
/// </summary>
public interface IUploadGarbageCollector
{
    /// <summary>
    /// Keep the most recent live upload of the target (by CreatedUtc) and mark
    /// the rest removed.
    /// </summary>
    /// <remarks>
    /// "Everything but the newest is obsolete" is a property of a single-slot
    /// target, not of an upload: an entity shows one avatar, a post carries as
    /// many attachments as its author put there. The type is therefore asked for
    /// rather than derived from the identifier, and a multi-slot type is refused
    /// instead of being quietly swept down to one file.
    /// </remarks>
    /// <param name="entityId">Entity identifier (User/Character).</param>
    /// <param name="type">Upload type of the slot that was replaced.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type holds more than one live upload per target.
    /// </exception>
    Task CollectObsoleteAsync(Guid entityId, UploadType type);
}
