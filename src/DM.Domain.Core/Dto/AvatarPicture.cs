namespace DM.Domain.Core.Dto;

/// <summary>
/// Source-файл аватара (один на upload). SSOT для projection'ов сущностей,
/// у которых есть аватар (User, Character).
///
/// Domain-уровень хранит ТОЛЬКО source-URL и object-key. Thumbnail-варианты
/// (small/medium) генерируются on-the-fly на API-слое через imgproxy при
/// маппинге в DTO (см. AvatarPictureResolver).
/// </summary>
public sealed class AvatarPicture
{
    /// <summary>
    /// S3 object key исходного файла (например <c>avatars/{userId:N}_{8hex}.jpg</c>).
    /// Используется imgproxy URL builder'ом на API-слое. Null если у сущности
    /// нет аватара.
    /// </summary>
    public string? SourceObjectKey { get; set; }

    /// <summary>
    /// Прямой публичный URL к source-файлу. Aspect-preserving original
    /// (≤1024 px). EXIF stripped. Null если нет аватара.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>True если есть source-файл.</summary>
    public bool HasUrl => SourceUrl != null;
}
