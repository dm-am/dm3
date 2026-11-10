using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace DM.Testing;

/// <remarks>
/// The formatter is run once, so assertions read the line a reader would see. Concurrent: a message may
/// be handled on a pool thread.
/// </remarks>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> written = new();

    /// <summary>Everything written, in order, as the formatter rendered it.</summary>
    public IReadOnlyCollection<string> Messages =>
        written.Select(entry => entry.Message).ToArray();

    public IReadOnlyCollection<string> At(LogLevel level) => written
        .Where(entry => entry.Level == level)
        .Select(entry => entry.Message)
        .ToArray();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        written.Enqueue((logLevel, formatter(state, exception)));
}
