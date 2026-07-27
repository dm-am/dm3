using System.Linq.Expressions;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Entities.Account;
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
/// EF translates these expressions to SQL directly; no inline duplication of
/// ternary formulas <c>u.AvatarUpload == null ? null : ...</c> in every repository.
/// </summary>
public static class AvatarProjections
{
    /// <summary>
    /// Constructor of <see cref="AvatarPicture"/> from a nullable <see cref="DbUpload"/>.
    /// Safe in an EF Select — compiles to SQL CASE / coalesce.
    /// </summary>
    public static AvatarPicture From(DbUpload? upload) =>
        upload == null
            ? new AvatarPicture()
            : new AvatarPicture
            {
                SourceObjectKey = upload.ObjectKey,
                SourceUrl = upload.FilePath,
            };

    /// <summary>
    /// EF-translatable Expression variant — for cases that need
    /// an actual Expression&lt;Func&gt; (e.g. in a reusable selector pattern).
    /// </summary>
    public static readonly Expression<System.Func<User, AvatarPicture>> FromUser = user =>
        user.AvatarUpload == null
            ? new AvatarPicture()
            : new AvatarPicture
            {
                SourceObjectKey = user.AvatarUpload.ObjectKey,
                SourceUrl = user.AvatarUpload.FilePath,
            };
}
