using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Core.Tracing;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Serilog;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// Every histogram is exported over the boundaries chosen for it.
/// </summary>
/// <remarks>
/// The boundaries are where a quantile on the dashboard comes from: a latency
/// read off a ladder meant for something else is still a number, only a wrong
/// one, and sizes measured against a ladder meant for seconds fall into one
/// bucket and answer nothing. That every histogram has a view is held as text
/// (ConsumerMetricsShould); this holds the values.
///
/// Read off the provider the host builds, through the instruments the code
/// records into, so the values are the ones the exporter publishes whether they
/// are declared in a view or on the instrument itself.
/// </remarks>
[Collection(StaticLoggerCollection.Name)]
public class HistogramBucketsShould
{
    private static readonly double[] Seconds = [0.01, 0.05, 0.1, 0.5, 1, 2, 5, 10, 30, 60, 120, 300];

    private static readonly double[] UploadSeconds =
        [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    /// <summary>The top boundary is the size the upload endpoint refuses above.</summary>
    private static readonly double[] Bytes = [1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 10485760];

    [Fact]
    public async Task PublishEveryHistogramOverTheBoundariesChosenForIt()
    {
        var exported = await ExportedBoundaries();

        exported.Should().ContainKey("dm.messaging.duration")
            .WhoseValue.Should().Equal(Seconds,
                "the retry ladder of the consumers alone outruns ten seconds, so a top bucket of " +
                "ten puts every retried message into the overflow");
        exported.Should().ContainKey("dm.uploads.duration")
            .WhoseValue.Should().Equal(UploadSeconds);
        exported.Should().ContainKey("dm.uploads.input_size")
            .WhoseValue.Should().Equal(Bytes,
                "measured against boundaries meant for seconds, every upload lands in one bucket");
        exported.Should().ContainKey("dm.uploads.output_size")
            .WhoseValue.Should().Equal(Bytes);
    }

    /// <summary>
    /// The finite boundaries of every histogram the host's meter provider exports,
    /// by instrument name.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, double[]>> ExportedBoundaries()
    {
        var capture = new BoundaryCapture();
        // Owned by the provider it is added to, which disposes it with itself.
        var reader = new BaseExportingMetricReader(capture);

        var previous = Log.Logger;
        var services = new ServiceCollection().AddDmLogging("DM.Probe", Configuration(), Environment());
        services.ConfigureOpenTelemetryMeterProvider(builder => builder.AddReader(reader));

        try
        {
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<MeterProvider>();

            // One measurement each: a histogram nobody recorded into has no point
            // to carry its buckets.
            MessagingMetrics.Duration.Record(0.2);
            UploadMetrics.Duration.Record(0.2);
            UploadMetrics.InputSizeBytes.Record(2048);
            UploadMetrics.OutputSizeBytes.Record(2048);

            reader.Collect().Should().BeTrue("the reader has to hand the exporter a batch to read");
            return capture.Boundaries;
        }
        finally
        {
            // AddDmLogging installs the process-wide logger with a push sink on a
            // timer; it is closed here and whatever the process had is put back.
            await Log.CloseAndFlushAsync();
            Log.Logger = previous;
        }
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Nothing is written in this test, so the addresses only have to parse.
                ["ConnectionStrings:Logs"] = "http://127.0.0.1:3100",
                ["ConnectionStrings:TracingEndpoint"] = "http://127.0.0.1:4317",
            })
            .Build();

    private static IHostEnvironment Environment() =>
        new HostingEnvironment { EnvironmentName = Environments.Production };

    /// <summary>An exporter that keeps the bucket boundaries of what it is handed.</summary>
    private sealed class BoundaryCapture : BaseExporter<Metric>
    {
        public Dictionary<string, double[]> Boundaries { get; } = new();

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                if (metric.MetricType != MetricType.Histogram)
                {
                    continue;
                }

                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    var bounds = new List<double>();
                    foreach (var bucket in point.GetHistogramBuckets())
                    {
                        // The last bucket is the overflow, bounded by infinity on
                        // every histogram there is.
                        if (!double.IsPositiveInfinity(bucket.ExplicitBound))
                        {
                            bounds.Add(bucket.ExplicitBound);
                        }
                    }

                    Boundaries[metric.Name] = bounds.ToArray();
                }
            }

            return ExportResult.Success;
        }
    }
}
