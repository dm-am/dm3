using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Pipeline для аватаров (UserAvatar, CharacterAvatar):
///   1) magic-byte валидация формата (не доверяем client content-type),
///   2) decompression-bomb защита (pre-decode pixel area check),
///   3) min/max dimension guards,
///   4) EXIF/IPTC/XMP strip (re-encode metadata-free),
///   5) downscale до <see cref="ImageProcessingDefaults.OriginalMaxDimension"/>
///      если изображение больше (Max-mode, aspect-preserving).
///
/// Возвращает один файл (source). Thumbnails генерируются on-the-fly
/// через imgproxy при serving — не пре-генерируются.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>True если тип upload'а требует image-pipeline (validation+EXIF strip).</summary>
    bool IsImageType(UploadType type);

    /// <summary>
    /// Прочесть stream, провалидировать (magic-byte, размеры, decompression-
    /// bomb), застрипать EXIF, downscale если &gt;1024 px. Кидает
    /// <see cref="DM.Domain.Core.Exceptions.HttpBadRequestException"/> при любой
    /// ошибке валидации.
    /// </summary>
    Task<ProcessedImage> ProcessAsync(
        Stream input,
        string declaredContentType,
        CancellationToken ct = default);
}

/// <summary>
/// Результат обработки — единственный re-encoded source-файл,
/// готовый к S3 PUT. Thumbnails не пре-генерируются — imgproxy
/// делает on-the-fly transform по запросу.
/// </summary>
public sealed record ProcessedImage(
    byte[] Bytes,
    string ContentType,
    string Extension);

/// <summary>
/// Public-доступные константы pipeline'а — SSOT для документов, тестов,
/// imgproxy presets.
/// </summary>
public static class ImageProcessingDefaults
{
    /// <summary>Максимальная сторона source-файла после обработки.</summary>
    public const int OriginalMaxDimension = 1024;
}
