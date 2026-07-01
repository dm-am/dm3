using System.Linq.Expressions;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Entities.Account;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// SSOT для projection'а source-файла аватара из <see cref="DbUpload"/>.
///
/// Возвращает single source: <see cref="AvatarPicture.SourceObjectKey"/>
/// (для imgproxy URL builder'а на API-слое) и <see cref="AvatarPicture.SourceUrl"/>
/// (прямой публичный URL для original-варианта без transform'а).
///
/// Thumbnail-варианты (small/medium) генерируются on-the-fly через imgproxy
/// при serving — никаких пре-сгенерированных файлов в S3.
///
/// EF translates these expressions to SQL directly; нет inline-дублирования
/// тернарных формул <c>u.AvatarUpload == null ? null : ...</c> в каждом репозитории.
/// </summary>
public static class AvatarProjections
{
    /// <summary>
    /// Конструктор <see cref="AvatarPicture"/> из nullable <see cref="DbUpload"/>.
    /// Безопасен в EF Select — компилируется в SQL CASE / coalesce.
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
    /// EF-translatable Expression вариант — для случаев, когда нужна
    /// именно Expression&lt;Func&gt; (например, в reusable selector pattern).
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
