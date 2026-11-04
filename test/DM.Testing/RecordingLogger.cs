using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace DM.Testing;

/// <summary>
/// Keeps what was written to it, for the tests that assert about the log itself.
/// </summary>
/// <remarks>
/// The formatter is run once, so assertions read the line a reader would see. Concurrent: a message may
/// be handled on a pool thread.
/// </remarks>
/// <typeparam name="T">Type the logger is resolved for.</typeparam>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> written = new();

    /// <summary>Everything written, in order, as the formatter rendered it.</summary>
    public IReadOnlyCollection<string> Messages =>
        written.Select(entry => entry.Message).ToArray();

    /// <summary>What was written at one level.</summary>
    /// <param name="level">Level to read.</param>
    /// <returns>The rendered lines of that level, in order.</returns>
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
