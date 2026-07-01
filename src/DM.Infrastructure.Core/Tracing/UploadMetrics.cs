using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Метрики для upload-пайплайна (Prometheus + OTel). Глобальный SSOT —
/// все upload-related instrumentation живет здесь.
///
/// Имена следуют OpenTelemetry semantic conventions: snake_case, единицы
/// в имени, dot-separated namespace.
/// </summary>
public static class UploadMetrics
{
    /// <summary>Имя Meter для регистрации в OTel.</summary>
    public const string MeterName = "DM.Uploads";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Счетчик успешных upload'ов. Атрибуты: <c>type</c> (UserAvatar /
    /// CharacterAvatar / PostAttachment), <c>content_type</c> (image/jpeg / png / webp).
    /// </summary>
    public static readonly Counter<long> Success =
        Meter.CreateCounter<long>("dm.uploads.success", "uploads", "Successfully completed uploads");

    /// <summary>
    /// Счетчик неуспешных upload'ов. Атрибуты: <c>type</c>, <c>reason</c>
    /// (validation / s3 / db / processing).
    /// </summary>
    public static readonly Counter<long> Failure =
        Meter.CreateCounter<long>("dm.uploads.failure", "uploads", "Failed uploads (any stage)");

    /// <summary>
    /// Histogram длительности всего upload-пайплайна (от первого байта до DB-commit),
    /// миллисекунды. Атрибуты: <c>type</c>.
    /// </summary>
    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>("dm.uploads.duration_ms", "ms", "End-to-end upload latency");

    /// <summary>
    /// Histogram размера original-файла на входе (байты). Помогает анти-DoS
    /// тюнингу: если большинство файлов меньше 100 KB, можно ужесточить лимит.
    /// </summary>
    public static readonly Histogram<long> InputSizeBytes =
        Meter.CreateHistogram<long>("dm.uploads.input_size_bytes", "By", "Uploaded file size (input)");

    /// <summary>
    /// Histogram размера всех 3 вариантов после processing (сумма байт).
    /// Метрика «сколько мы реально кладем в S3 на один upload».
    /// </summary>
    public static readonly Histogram<long> OutputSizeBytes =
        Meter.CreateHistogram<long>("dm.uploads.output_size_bytes", "By", "Total bytes written to S3 across 3 variants");
}
