namespace DM.Domain.Core.Uploads;

/// <summary>
/// Генератор подписанных imgproxy-URL'ов для thumbnail-вариантов аватара.
///
/// Бэк хранит один source-файл, на лету генерируется любой размер/формат
/// через imgproxy. Подпись HMAC-SHA256 предотвращает abuse (anyone иначе
/// мог бы запросить произвольный transform и сжечь CPU).
/// </summary>
public interface IImgproxyUrlBuilder
{
    /// <summary>
    /// Сгенерировать URL для thumbnail заданного размера (square center-crop).
    /// Format negotiation (AVIF/WebP/JPEG) делается imgproxy на основе Accept
    /// header браузера — один URL для всех форматов.
    /// </summary>
    /// <param name="sourceObjectKey">S3 object key исходного файла (без prefix).</param>
    /// <param name="size">Сторона квадрата в пикселях (100, 400, etc).</param>
    /// <returns>Подписанный URL для GET.</returns>
    string BuildSquareThumbnail(string sourceObjectKey, int size);
}
