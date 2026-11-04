using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// The rule of an upload type whose files everybody sees: an avatar, shown
/// wherever its owner is named and served from a prefix the bucket answers to
/// anybody. Only who may attach one differs from type to type.
/// </summary>
public abstract class PublicUploadTargetAuthorizer : IUploadTargetAuthorizer
{
    /// <inheritdoc />
    public abstract UploadType Type { get; }

    /// <inheritdoc />
    public abstract Task EnsureAllowedAsync(Guid targetId);

    /// <inheritdoc />
    /// <remarks>Withholding a file the public address gives away would withhold nothing.</remarks>
    public Task EnsureReadAllowedAsync(StoredUpload upload) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Nothing the file hangs on keeps it from anybody.</remarks>
    public Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    /// <inheritdoc />
    /// <remarks>The bucket answers the same file to anybody.</remarks>
    public Task<bool> OpenToGuestAsync(StoredUpload upload) => Task.FromResult(true);

    /// <inheritdoc />
    /// <remarks>
    /// None. The upload service refuses a chosen audience on such a type before it
    /// gets here, so one arriving is a caller's bug, and storing it as everybody
    /// would widen what was chosen.
    /// </remarks>
    public Task<string?> FreezeAudienceAsync(Guid targetId, Guid? uploadId, UploadAudience audience) =>
        audience.VisibleTo == UploadVisibleTo.All
            ? Task.FromResult<string?>(null)
            : throw new InvalidOperationException("A chosen audience reached a type whose files everybody sees");

    /// <inheritdoc />
    /// <remarks>
    /// Nobody but the owner and Moderator+, which is what the upload's own
    /// intention already says: an avatar has no editors of its own, and a
    /// portrait is retired by replacement, never left absent.
    /// </remarks>
    public Task<bool> MayDetachAsync(Guid targetId) => Task.FromResult(false);
}
