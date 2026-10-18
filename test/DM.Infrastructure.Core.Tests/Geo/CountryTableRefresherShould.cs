using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Geo;
using DM.Infrastructure.Core.Geo;
using DM.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Geo;

/// <summary>
/// The country table is downloaded and put in use by the running process, and a
/// download that is not a table changes nothing.
/// </summary>
/// <remarks>
/// The tables here are made to pass the size check with real ranges, so what
/// decides each case is the check under test and not the padding around it. The
/// address 203.0.113.42 is the probe: one table puts it in Russia, the other in
/// Germany, so which table answers is visible from outside.
/// </remarks>
public class CountryTableRefresherShould : IDisposable
{
    private const string Probe = "203.0.113.42";
    private const string Source = "https://tables.example.test/user-country.csv";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dm3-geo-{Guid.NewGuid():N}");
    private readonly string _path;
    private readonly RecordingLogger<CountryTableRefresher> _log = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

    public CountryTableRefresherShould()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "ip-country.csv");
    }

    /// <summary>
    /// A table big enough to pass the size check, the probe range in the given country.
    /// </summary>
    private static byte[] TableOf(string probeCountry)
    {
        var text = new StringBuilder();
        text.Append("203.0.113.0,203.0.113.255,").Append(probeCountry).Append('\n');
        for (var block = 0; text.Length < CountryTableRefresher.MinimumBytes + 4096; block++)
        {
            text.Append("10.").Append(block / 256).Append('.').Append(block % 256).Append(".0,")
                .Append("10.").Append(block / 256).Append('.').Append(block % 256).Append(".255,AU\n");
        }

        return Encoding.ASCII.GetBytes(text.ToString());
    }

    private IpCountryLookup Lookup() => new(
        Options.Create(new GeoLocationConfiguration { CountryDatabasePath = _path }),
        NullLogger<IpCountryLookup>.Instance);

    private CountryTableRefresher Refresher(IpCountryLookup lookup, StubHandler handler) => new(
        lookup,
        Options.Create(new GeoLocationConfiguration { CountryDatabasePath = _path, CountryDatabaseUrl = Source }),
        _clock,
        _log,
        new HttpClient(handler));

    /// <summary>
    /// The point of the whole thing: the running lookup answers from the new
    /// table, and the new table is what a restart will read.
    /// </summary>
    [Fact]
    public async Task PutTheDownloadedTableInUseWithoutARestart()
    {
        await File.WriteAllBytesAsync(_path, TableOf("RU"), TestContext.Current.CancellationToken);
        var lookup = Lookup();
        lookup.Find(Probe).Should().Be("Россия", "the table on disk is read first");

        using var refresher = Refresher(lookup, StubHandler.Answering(TableOf("DE")));
        var replaced = await refresher.RefreshAsync(TestContext.Current.CancellationToken);

        replaced.Should().BeTrue();
        lookup.Find(Probe).Should().Be("Германия",
            "the same lookup, never rebuilt, answers from the table that was just downloaded");
        Lookup().Find(Probe).Should().Be("Германия",
            "the new table is the file at the path, so a restart reads it too");
        File.Exists(_path + ".download").Should().BeFalse("the download is renamed into place, not left beside it");
    }

    /// <summary>The first start of a stand: no file yet, and nobody to put one there.</summary>
    [Fact]
    public async Task DownloadTheTableOnStartWhenThereIsNone()
    {
        var lookup = Lookup();
        var handler = StubHandler.Answering(TableOf("DE"));
        using var refresher = Refresher(lookup, handler);

        await refresher.StartAsync(TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (lookup.Find(Probe) is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        await refresher.StopAsync(TestContext.Current.CancellationToken);

        lookup.Find(Probe).Should().Be("Германия");
        File.Exists(_path).Should().BeTrue("the downloaded table is kept for the next start");
        handler.Requests.Should().Be(1, "after a download the next one is a week away");
    }

    /// <summary>What does not pass changes nothing: not the answers, not the file.</summary>
    [Theory]
    [InlineData("an error page")]
    [InlineData("a page as big as a table")]
    [InlineData("a cut answer that still reads as ranges")]
    [InlineData("a length announced past any table")]
    [InlineData("a body past any table with no length announced")]
    [InlineData("a failed request")]
    [InlineData("no connection")]
    public async Task KeepTheTableInUseWhenTheDownloadIsNotATable(string download)
    {
        var original = TableOf("RU");
        await File.WriteAllBytesAsync(_path, original, TestContext.Current.CancellationToken);
        var lookup = Lookup();
        lookup.Find(Probe).Should().Be("Россия");

        var handler = download switch
        {
            "an error page" => StubHandler.Answering(
                Encoding.UTF8.GetBytes("<html><body>Not Found</body></html>")),
            // Big enough for the size check, and not a line of it is a range.
            "a page as big as a table" => StubHandler.Answering(
                Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
                    "<p>Lorem ipsum, dolor sit amet</p>\n", 40_000)))),
            // Every line a range, so only the lower bound of the size turns it away.
            "a cut answer that still reads as ranges" => StubHandler.Answering(TableOf("DE")[..4096]),
            // Only the header is past the bound; the body itself would pass.
            "a length announced past any table" => StubHandler.Announcing(
                TableOf("DE"), CountryTableRefresher.MaximumBytes + 1),
            // Ranges all the way, as a chunked answer sends them: no header to
            // refuse, so only the count of bytes read turns it away.
            "a body past any table with no length announced" => StubHandler.Streaming(
                TableOf("DE"), CountryTableRefresher.MaximumBytes + (1L << 20)),
            "a failed request" => StubHandler.Failing(HttpStatusCode.ServiceUnavailable),
            "no connection" => StubHandler.Throwing(new HttpRequestException("Connection refused")),
            _ => throw new ArgumentOutOfRangeException(nameof(download), download, null),
        };

        using var refresher = Refresher(lookup, handler);
        var replaced = await refresher.RefreshAsync(TestContext.Current.CancellationToken);

        replaced.Should().BeFalse();
        lookup.Find(Probe).Should().Be("Россия", "the table in use stays when the new one does not pass");
        (await File.ReadAllBytesAsync(_path, TestContext.Current.CancellationToken)).Should().Equal(original,
            "the file a restart reads is not overwritten by what did not pass");
        File.Exists(_path + ".download").Should().BeFalse("a copy that did not pass is not left beside the table");
        _log.At(LogLevel.Error).Should().ContainSingle(
            "the failure goes to the log, and the next attempt is the retry");
    }

    /// <summary>
    /// The schedule follows the file: a restart does not download a table that is
    /// days old, and a server that was down for longer than a week does at once.
    /// </summary>
    [Theory]
    [InlineData(1, 6)]
    [InlineData(6.5, 0.5)]
    [InlineData(7, 0)]
    [InlineData(30, 0)]
    public async Task WaitOutTheWeekOfTheTableOnDisk(double ageInDays, double waitInDays)
    {
        await File.WriteAllBytesAsync(_path, TableOf("RU"), TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(_path, _clock.Now.UtcDateTime - TimeSpan.FromDays(ageInDays));

        using var refresher = Refresher(Lookup(), StubHandler.Answering(TableOf("DE")));

        refresher.FirstWait().Should().Be(TimeSpan.FromDays(waitInDays));
    }

    /// <summary>A fresh file nothing can be read from is replaced now, not in a week.</summary>
    [Fact]
    public async Task DownloadAtOnceOverAFreshFileThatHoldsNoRange()
    {
        await File.WriteAllTextAsync(_path, "garbage\n", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(_path, _clock.Now.UtcDateTime - TimeSpan.FromHours(1));

        using var refresher = Refresher(Lookup(), StubHandler.Answering(TableOf("DE")));

        refresher.FirstWait().Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The refresh and the letter must hold one lookup. Two would mean a refresh
    /// that fills a table no letter ever reads.
    /// </summary>
    [Fact]
    public void ShareOneLookupBetweenTheLetterAndTheRefresh()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDmCore();
        using var provider = services.BuildServiceProvider();

        var refreshed = provider.GetRequiredService<IpCountryLookup>();

        provider.GetRequiredService<IIpCountryLookup>().Should().BeSameAs(refreshed);
        provider.GetRequiredService<IpCountryLookup>().Should().BeSameAs(refreshed,
            "the lookup holds the table for the life of the process");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset Now { get; } = now;
    }

    /// <summary>Answers every request the same way and counts them.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;
        private int _requests;

        private StubHandler(Func<HttpResponseMessage> respond) => _respond = respond;

        public int Requests => Volatile.Read(ref _requests);

        public static StubHandler Answering(byte[] body) =>
            new(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });

        /// <summary>A body whose header claims another length, as a server may.</summary>
        public static StubHandler Announcing(byte[] body, long announcedLength) => new(() =>
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentLength = announcedLength;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        /// <summary>
        /// The table repeated up to the given length, produced as it is read and
        /// sent with no length, so nothing that large is ever held in memory.
        /// </summary>
        public static StubHandler Streaming(byte[] table, long length) => new(() =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new RepeatingStream(table, length)),
            });

        public static StubHandler Failing(HttpStatusCode status) =>
            new(() => new HttpResponseMessage(status) { Content = new StringContent("unavailable") });

        public static StubHandler Throwing(Exception failure) => new(() => throw failure);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            request.RequestUri.Should().Be(new Uri(Source), "the request asks for the table and nothing else");
            return Task.FromResult(_respond());
        }
    }

    /// <summary>
    /// Reads as one source repeated up to a length. Not seekable, so the content
    /// around it cannot work out a length to announce.
    /// </summary>
    private sealed class RepeatingStream(byte[] source, long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(Span<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, length - _position);
            for (var written = 0; written < count;)
            {
                var at = (int)((_position + written) % source.Length);
                var piece = Math.Min(count - written, source.Length - at);
                source.AsSpan(at, piece).CopyTo(buffer[written..]);
                written += piece;
            }

            _position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
