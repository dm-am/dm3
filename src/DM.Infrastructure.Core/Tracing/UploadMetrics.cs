using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics for the upload pipeline (Prometheus + OTel). Global SSOT —
/// all upload-related instrumentation lives here.
///
/// Names follow OpenTelemetry semantic conventions: snake_case, units
/// in the name, dot-separated namespace.
/// </summary>
public static class UploadMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Uploads";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter of successful uploads. Attributes: <c>type</c> (UserAvatar /
    /// CharacterAvatar / PostAttachment), <c>content_type</c> (image/jpeg / png / webp).
    /// </summary>
    public static readonly Counter<long> Success =
        Meter.CreateCounter<long>("dm.uploads.success", "uploads", "Successfully completed uploads");

    /// <summary>
    /// Counter of failed uploads. Attributes: <c>type</c>, <c>reason</c>
    /// (validation / s3 / db / processing).
    /// </summary>
    public static readonly Counter<long> Failure =
        Meter.CreateCounter<long>("dm.uploads.failure", "uploads", "Failed uploads (any stage)");

    /// <summary>
    /// Histogram of the whole upload pipeline duration (first byte to DB commit),
    /// milliseconds. Attributes: <c>type</c>.
    /// </summary>
    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>("dm.uploads.duration_ms", "ms", "End-to-end upload latency");

    /// <summary>
    /// Histogram of the incoming original file size (bytes). Helps anti-DoS
    /// tuning: if most files are under 100 KB, the limit can be tightened.
    /// </summary>
    public static readonly Histogram<long> InputSizeBytes =
        Meter.CreateHistogram<long>("dm.uploads.input_size_bytes", "By", "Uploaded file size (input)");

    /// <summary>
    /// Histogram of the size of all 3 variants after processing (byte sum).
    /// The "how much we actually put into S3 per upload" metric.
    /// </summary>
    public static readonly Histogram<long> OutputSizeBytes =
        Meter.CreateHistogram<long>("dm.uploads.output_size_bytes", "By", "Total bytes written to S3 across 3 variants");
}
