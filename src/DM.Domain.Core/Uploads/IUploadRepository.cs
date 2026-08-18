using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Storage access for user uploads.
/// </summary>
/// <remarks>
/// Callers state what they need about a file; how the row is laid out stays on
/// this side of the interface. In particular the three mutually exclusive target
/// columns are exposed as one <see cref="StoredUpload.TargetId"/>, and hiding a
/// file is a soft delete whose flags no caller has to know about.
/// </remarks>
public interface IUploadRepository
{
    /// <summary>
    /// One page of uploads matching <paramref name="filter"/>, newest first,
    /// together with the total number of matching rows — the caller needs both
    /// to build paging.
    /// </summary>
    /// <param name="filter">Owner / type / status restrictions; an empty filter matches everything.</param>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Page size.</param>
    Task<(IReadOnlyCollection<StoredUpload> Uploads, int TotalCount)> GetPageAsync(
        UploadFilter filter, int skip, int take);

    /// <summary>
    /// Single upload, or null when no live row carries that identifier.
    /// </summary>
    /// <param name="uploadId">Upload identifier.</param>
    Task<StoredUpload?> GetAsync(Guid uploadId);

    /// <summary>
    /// Key of the object in the bucket, or null when no live row carries that
    /// identifier.
    /// </summary>
    /// <remarks>
    /// Deliberately not a field of <see cref="StoredUpload"/>: that record is what
    /// the API answers with, and the key is the one thing about a file that must
    /// never leave the server. Asking for it is a separate, named act, so a
    /// projection that grows a field cannot carry it out by accident.
    /// </remarks>
    /// <param name="uploadId">Upload identifier.</param>
    Task<string?> GetObjectKeyAsync(Guid uploadId);

    /// <summary>
    /// How many live files are attached to the post.
    /// </summary>
    /// <remarks>
    /// Its own question rather than a filter on the listing: the listing pages and
    /// projects rows for a screen, and this is a limit check on a write path that
    /// needs one number.
    /// </remarks>
    /// <param name="postId">Post identifier.</param>
    Task<int> CountPostAttachmentsAsync(Guid postId);

    /// <summary>
    /// Hide the upload from the site and start the grace period after which the
    /// background sweeper drops the object from the bucket.
    /// </summary>
    /// <param name="uploadId">Upload identifier.</param>
    /// <param name="deletedByUserId">
    /// Who asked for the deletion, null when a background collector did. Deleting
    /// somebody else's file is a moderation action, so the null case is the sweeper
    /// and only the sweeper — an empty author after a person pressed delete is what
    /// leaves moderation unable to answer who removed the file.
    /// </param>
    /// <param name="deletedUtc">Moment the deletion was requested.</param>
    Task SoftDeleteAsync(Guid uploadId, Guid? deletedByUserId, DateTimeOffset deletedUtc);

    /// <summary>
    /// Persist a record for a file that has already been written to the bucket.
    /// </summary>
    /// <remarks>
    /// A rejected write surfaces as <see cref="Exceptions.StorageException"/>: the
    /// caller owns an object nothing will ever reference and has to undo it, and
    /// it must be able to tell that case from a bug on its own side without
    /// catching the ORM's exception type.
    /// </remarks>
    /// <param name="upload">Values of the row to create.</param>
    Task<StoredUpload> AddAsync(NewUpload upload);
}

/// <summary>
/// Restrictions for listing uploads. Every property left null widens the result.
/// </summary>
public class UploadFilter
{
    /// <summary>Only uploads owned by this user.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Only uploads of this type.</summary>
    public UploadType? Type { get; init; }

    /// <summary>Only uploads in this processing status.</summary>
    public UploadStatus? Status { get; init; }
}

/// <summary>
/// An upload as it exists in storage.
/// </summary>
public class StoredUpload
{
    /// <summary>Upload identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owner identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Owner username. Null when the owner was not resolved — a freshly created
    /// record, or an owner row the soft-delete filter hides.
    /// </summary>
    public string? OwnerUsername { get; init; }

    /// <summary>Upload type / purpose.</summary>
    public UploadType Type { get; init; }

    /// <summary>Processing status.</summary>
    public UploadStatus Status { get; init; }

    /// <summary>Entity the file belongs to; its kind follows from <see cref="Type"/>.</summary>
    public Guid? TargetId { get; init; }

    /// <summary>Display file name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>MIME content type.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Public URL of the file.</summary>
    public string? Url { get; init; }

    /// <summary>Creation moment (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>Confirmation moment (UTC).</summary>
    public DateTimeOffset? ConfirmedUtc { get; init; }
}

/// <summary>
/// Values of an upload record to create.
/// </summary>
public class NewUpload
{
    /// <summary>Upload identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owner identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Upload type / purpose.</summary>
    public UploadType Type { get; init; }

    /// <summary>Processing status.</summary>
    public UploadStatus Status { get; init; }

    /// <summary>Entity the file belongs to; its kind follows from <see cref="Type"/>.</summary>
    public Guid? TargetId { get; init; }

    /// <summary>Display file name.</summary>
    public string FileName { get; init; } = null!;

    /// <summary>MIME content type.</summary>
    public string ContentType { get; init; } = null!;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Intrinsic width of the stored file in pixels. Null when the caller does
    /// not know it — a non-image upload, or a path that never measured one.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>Intrinsic height of the stored file in pixels; travels with <see cref="Width"/>.</summary>
    public int? Height { get; init; }

    /// <summary>Key of the object in the bucket.</summary>
    public string ObjectKey { get; init; } = null!;

    /// <summary>Flag of file source: original or modified.</summary>
    public bool Original { get; init; }

    /// <summary>Public URL of the file.</summary>
    public string? Url { get; init; }

    /// <summary>Creation moment (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>Confirmation moment (UTC).</summary>
    public DateTimeOffset? ConfirmedUtc { get; init; }
}
