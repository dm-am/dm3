using System;
using System.Linq.Expressions;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Shared.Queries;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// SSOT for projecting the avatar source file from <see cref="DbUpload"/>.
///
/// Returns a single source: <see cref="AvatarPicture.SourceObjectKey"/>
/// (for the imgproxy URL builder at the API layer) and <see cref="AvatarPicture.SourceUrl"/>
/// (a direct public URL for the original variant without transforms).
///
/// Thumbnail variants (small/medium) are generated on-the-fly via imgproxy
/// at serving time — no pre-generated files in S3.
///
/// The formula exists once, as <see cref="Projection"/>. Inside an EF query
/// it is inlined with <see cref="ExpressionSplicer.Splice{T,TResult}"/>, so
/// the provider sees the member accesses and fetches only the upload columns
/// the formula names. <see cref="From"/> is the same expression compiled
/// once, for call sites that already hold a materialised row; called inside
/// an EF Select it stays opaque to the provider, which then loads the whole
/// upload row and runs the formula on the client.
/// </summary>
public static class AvatarProjections
{
    /// <summary>
    /// The one formula: <see cref="AvatarPicture"/> from a nullable
    /// <see cref="DbUpload"/>. Splice into EF projections via
    /// <see cref="ExpressionSplicer"/>.
    /// </summary>
    public static readonly Expression<Func<DbUpload?, AvatarPicture>> Projection =
        upload => upload == null
            ? new AvatarPicture()
            : new AvatarPicture
            {
                SourceObjectKey = upload.ObjectKey,
                SourceUrl = upload.FilePath,
                SourceWidth = upload.Width,
                SourceHeight = upload.Height,
            };

    private static readonly Func<DbUpload?, AvatarPicture> Compiled = Projection.Compile();

    /// <summary>
    /// <see cref="Projection"/> compiled, for rows already in memory
    /// </summary>
    public static AvatarPicture From(DbUpload? upload) => Compiled(upload);
}
