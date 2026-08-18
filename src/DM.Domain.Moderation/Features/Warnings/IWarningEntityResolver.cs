using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Reads the object a warning was issued for: its text at issue time, its
/// address, and whether it was edited after the warning.
/// </summary>
/// <remarks>
/// A warning names the offending object by identifier alone, and the author can
/// edit that object afterwards. The snapshot taken here is what makes the
/// warning readable later; the address and the "edited since" flag are derived
/// on every read, because both change on their own and a stored copy of either
/// would be stale by the time a moderator looks.
///
/// Game posts and game room chat are deliberately absent: game content is not
/// moderated, so no warning names it and nothing here resolves it.
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
    /// Resolve the current state of the objects named by the given warnings,
    /// keyed by warning identifier. Warnings naming no object are absent from
    /// the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, WarningEntityState>> ResolveStates(
        IReadOnlyCollection<WarningEntityRequest> requests, CancellationToken ct = default);
}

/// <summary>
/// One warning's reference to the object it was issued for
/// </summary>
public class WarningEntityRequest
{
    /// <summary>
    /// Warning the reference belongs to
    /// </summary>
    public Guid WarningId { get; set; }

    /// <summary>
    /// Offending object identifier
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Offending object kind
    /// </summary>
    public WarningEntityType EntityType { get; set; }

    /// <summary>
    /// Moment the warning was issued: anything edited later is edited after it
    /// </summary>
    public DateTimeOffset IssuedUtc { get; set; }
}

/// <summary>
/// What is currently true about the object a warning was issued for
/// </summary>
public class WarningEntityState
{
    /// <summary>
    /// Site-relative address of the object, null when it cannot be addressed
    /// (deleted, or a kind with no public page)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Whether the edit history holds an edit later than the warning
    /// </summary>
    public bool EditedAfterWarning { get; set; }
}
