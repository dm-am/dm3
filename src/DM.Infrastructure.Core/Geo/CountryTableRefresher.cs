using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Geo;

/// <summary>
/// Keeps the country table fresh without a restart and without anyone on the
/// server: downloads it when there is none, then once a week.
/// </summary>
/// <remarks>
/// A copy replaces the old one only once checked, renamed over it so the file on
/// disk is always a whole table; a failure changes nothing and is retried. The
/// schedule follows the age of the file, not the process.
/// </remarks>
internal sealed class CountryTableRefresher : BackgroundService
{
    /// <summary>The publisher rebuilds the table daily; a week keeps it fresh enough for a country.</summary>
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(7);

    /// <summary>Soon enough to ride out an outage, rare enough not to flood the log over a long one.</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// An error page or a cut answer is kilobytes; the table is tens of megabytes.
    /// </summary>
    internal const long MinimumBytes = 1L << 20;

    /// <summary>
    /// A replacement holds the old table and the new one at once, so a file far past
    /// the expected one would cost the API its memory.
    /// </summary>
    internal const long MaximumBytes = 64L << 20;

    /// <summary>Bounds the whole download, body included, not only the wait for headers.</summary>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    private const int BufferSize = 81920;

    private readonly IpCountryLookup _lookup;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CountryTableRefresher> _logger;
    private readonly HttpClient _http;
    private readonly string _path;
    private readonly string _url;
    private readonly Uri? _source;

    public CountryTableRefresher(
        IpCountryLookup lookup,
        IOptions<GeoLocationConfiguration> configuration,
        IDateTimeProvider clock,
        ILogger<CountryTableRefresher> logger,
        HttpClient http)
    {
        _lookup = lookup;
        _clock = clock;
        _logger = logger;
        _http = http;
        _path = configuration.Value.CountryDatabasePath;
        _url = configuration.Value.CountryDatabaseUrl;

        // Https only, or anyone on the path decides what the letter says. Without
        // throwing: a bad value turns the refresh off, not the host.
        _source = Uri.TryCreate(_url, UriKind.Absolute, out var source) &&
                  source.Scheme == Uri.UriSchemeHttps
            ? source
            : null;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Off the startup path: reading a table of this size takes a moment.
        await Task.Yield();

        if (string.IsNullOrWhiteSpace(_path) || string.IsNullOrWhiteSpace(_url))
        {
            _logger.LogInformation(
                "Country table refresh is off: no table path or no download address configured");
            return;
        }

        if (_source is null)
        {
            _logger.LogError(
                "Country table refresh is off: {Url} is not an https address", _url);
            return;
        }

        TimeSpan wait;
        try
        {
            wait = FirstWait();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Treated as no file: an exception here would stop the host.
            _logger.LogError(e, "Could not read the country table at {Path}; downloading a new one", _path);
            wait = TimeSpan.Zero;
        }

        try
        {
            _logger.LogInformation(
                "Country table refresh started: {Path} from {Url}, every {Interval}, first download in {Wait}",
                _path, _url, RefreshInterval, wait);

            while (true)
            {
                if (wait > TimeSpan.Zero) await Task.Delay(wait, stoppingToken);
                wait = await RefreshAsync(stoppingToken) ? RefreshInterval : RetryDelay;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; a download cut short leaves the table as it was.
        }
    }

    /// <summary>
    /// How long the table on disk is still fresh: nothing when there is no file,
    /// when it is a week old, or when it holds no range at all.
    /// </summary>
    internal TimeSpan FirstWait()
    {
        if (!File.Exists(_path)) return TimeSpan.Zero;

        var age = _clock.Now.UtcDateTime - File.GetLastWriteTimeUtc(_path);
        if (age >= RefreshInterval) return TimeSpan.Zero;

        // A file nothing can be read from is downloaded over now.
        if (!_lookup.HoldsRanges) return TimeSpan.Zero;

        // A clock set back, or a file dated in the future, still waits one interval at most.
        return age < TimeSpan.Zero ? RefreshInterval : RefreshInterval - age;
    }

    internal async Task<bool> RefreshAsync(CancellationToken stoppingToken)
    {
        var download = _path + ".download";
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(DownloadTimeout);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var size = await DownloadAsync(download, deadline.Token);

            var table = IpCountryLookup.Read(download, out var skipped)
                ?? throw new InvalidDataException("The downloaded file holds no usable range");

            // Within one directory, so the file is never half of either table.
            File.Move(download, _path, overwrite: true);
            _lookup.Use(table);

            _logger.LogInformation(
                "Country table replaced from {Url}: {Size} bytes, {V4} IPv4 ranges, {V6} IPv6 ranges, {Skipped} lines skipped",
                _url, size, table.V4.Length, table.V6.Length, skipped);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Country table download from {Url} failed; the table in use stays, next attempt in {Retry}",
                _url, RetryDelay);
            return false;
        }
        finally
        {
            Discard(download);
        }
    }

    private async Task<long> DownloadAsync(string destination, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            _source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var announced = response.Content.Headers.ContentLength;
        if (announced > MaximumBytes)
        {
            throw new InvalidDataException(
                $"The server announced {announced} bytes, more than the {MaximumBytes} a table may take");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = new FileStream(
            destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);

        var buffer = new byte[BufferSize];
        long total = 0;
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaximumBytes)
            {
                throw new InvalidDataException(
                    $"The download passed {MaximumBytes} bytes, more than a table may take");
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total < MinimumBytes)
        {
            throw new InvalidDataException(
                $"The download is {total} bytes, fewer than the {MinimumBytes} of the smallest table");
        }

        return total;
    }

    private void Discard(string download)
    {
        try
        {
            if (File.Exists(download)) File.Delete(download);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "Could not remove the unfinished country table at {Path}", download);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }
}
