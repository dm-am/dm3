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
}
