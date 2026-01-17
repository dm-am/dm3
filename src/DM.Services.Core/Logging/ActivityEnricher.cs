using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace DM.Services.Core.Logging;

/// <summary>
/// Enriches log events with OpenTelemetry Activity information (TraceId, SpanId)
/// </summary>
public class ActivityEnricher : ILogEventEnricher
{
    /// <summary>
    /// Enriches the log event with TraceId and SpanId from the current Activity
    /// </summary>
    /// <param name="logEvent">The log event to enrich</param>
    /// <param name="propertyFactory">Factory for creating log event properties</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        }
    }
}
