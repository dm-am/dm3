using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of the upload pipeline. Units as in <see cref="MessagingMetrics"/>.
/// </summary>
public static class UploadMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Uploads";

    private static readonly Meter Meter = new(MeterName, DmActivitySource.Version);

    /// <summary>
    /// Counter of successful uploads. Attributes: <c>type</c> (UserAvatar /
    /// CharacterAvatar / PostAttachment), <c>content_type</c> (image/jpeg / png / webp).
    /// </summary>
    public static readonly Counter<long> Success =
        Meter.CreateCounter<long>("dm.uploads.success", null, "Successfully completed uploads");

    /// <summary>
    /// Counter of failed uploads. Attributes: <c>type</c>, <c>reason</c>
    /// (validation / s3 / db / processing).
    /// </summary>
    public static readonly Counter<long> Failure =
        Meter.CreateCounter<long>("dm.uploads.failure", null, "Failed uploads (any stage)");

    /// <summary>
    /// Histogram of the whole upload pipeline duration (first byte to DB commit),
    /// seconds. Attributes: <c>type</c>.
    /// </summary>
    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("dm.uploads.duration", "s", "End-to-end upload latency");

    /// <summary>
    /// Histogram of the incoming file size (bytes), for tuning the upload limit.
    /// </summary>
    public static readonly Histogram<long> InputSizeBytes =
        Meter.CreateHistogram<long>("dm.uploads.input_size", "By", "Uploaded file size (input)");

    /// <summary>
    /// Histogram of the size of the stored file (bytes).
    /// </summary>
    public static readonly Histogram<long> OutputSizeBytes =
        Meter.CreateHistogram<long>("dm.uploads.output_size", "By", "Bytes written to S3 per upload");
}
