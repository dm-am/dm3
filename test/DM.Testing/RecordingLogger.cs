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
/// Some of what this suite holds in place is a property of the log and of nothing
/// else: that a retry says which queue it happened on, and that the line written per
/// letter carries neither the letter nor the credentials it goes out under. A null
/// logger cannot answer either question, and a mock of ILogger answers about the
/// state-and-formatter overload rather than about the line a reader would see - so
/// the formatter is run here, once, and what it returned is what the assertions read.
///
/// Concurrent because a message can be handled on a pool thread while the test that
/// asserts about it waits on another.
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
