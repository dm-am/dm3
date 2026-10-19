using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Reads the object a warning was issued for: its text at issue time, and
/// where it stands now.
/// </summary>
/// <remarks>
/// The author can edit the object afterwards, so the snapshot taken here is the text
/// the log shows; the address is derived on every read, since it changes on its own.
/// Game content is not moderated, so nothing here resolves a game post or room chat.
/// </remarks>
public interface IWarningEntityResolver
{
    /// <summary>
    /// Read the current text of the offending object, to be stored on the
    /// warning. Returns null when there is no such object, or when its kind
    /// carries no text moderation reads.
    /// </summary>
    Task<string?> CaptureSnapshot(WarningEntityType entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Read what the profile of the user shows everybody, to be stored on a
    /// warning issued on that profile: the status, "О себе" and the upload the
    /// avatar is. Returns null when there is no such user.
    /// </summary>
    /// <remarks>
    /// The one read of a user row here, and a plain one under the filter of
    /// removed accounts: the warned user was just found by his name.
    /// </remarks>
    Task<ProfileCapture?> CaptureProfile(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Resolve the current state of the objects named by the given warnings,
    /// keyed by warning identifier. Warnings naming no object are absent from
    /// the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, WarningEntityState>> ResolveStates(
        IReadOnlyCollection<WarningEntityRequest> requests, CancellationToken ct = default);
}
