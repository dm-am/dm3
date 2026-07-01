namespace DM.Domain.Core.Configuration;

/// <summary>
/// Конфигурация imgproxy — on-the-fly image transforms (resize + format
/// negotiation AVIF/WebP/JPEG).
///
/// Бэк хранит один source-файл в S3, генерирует подписанные imgproxy-URL'ы
/// для thumbnail-вариантов при проекции в DTO. Никаких pre-generated
/// thumbnails в storage.
/// </summary>
public class ImageProxyConfiguration
{
    /// <summary>
    /// Публичный URL imgproxy (откуда фронт грузит трансформированные images).
    /// В dev: http://localhost:8080. В prod: https://img.dm.com или подобное.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 key (hex). Используется для подписи URL'ов чтобы
    /// anyone не мог потребовать произвольный transform.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 salt (hex). Конкатенируется с path перед HMAC.
    /// </summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Префикс source-URL для imgproxy (где он берет исходник).
    /// Для MinIO S3: <c>s3://dm-uploads/</c>. Для HTTPS-CDN: <c>https://cdn.example.com/</c>.
    /// </summary>
    public string SourceUrlPrefix { get; set; } = string.Empty;
}
