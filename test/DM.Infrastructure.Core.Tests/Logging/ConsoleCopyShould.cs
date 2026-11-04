using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DM.Infrastructure.Core.Logging;
using AwesomeAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Logging;

/// <summary>
/// The console copy of a line carries what the line was about.
/// </summary>
/// <remarks>
/// The console copy is the one left when the log store is down. Asserted on the formatter: the sink
/// resolves its stream on its own terms.
/// </remarks>
public class ConsoleCopyShould
{
    [Fact]
    public void CarryThePropertiesOfALineOnAServer()
    {
        var line = Render(LoggingConfiguration.ConsoleFormat(isDevelopment: false));

        line.Should().Contain("\"@m\"",
            "the reader of this copy is looking for what happened, and \"@mt\" leaves them " +
            "to substitute the placeholders by hand");
        line.Should().Contain("CorrelationToken",
            "one request is many lines, and without the token nothing says which lines " +
            "belong to it");
        line.Should().Contain("\"@tr\"",
            "the trace of the line is what connects this copy to the request that caused " +
            "it, and it comes off the event rather than off a property");
    }

    [Fact]
    public void StayReadableOnADeveloperMachine() =>
        LoggingConfiguration.ConsoleFormat(isDevelopment: true).Should().BeNull(
            "this console is read by a person as it is written, and JSON one line per event " +
            "is not a thing a person reads");

    /// <summary>One event with a property, written the way the sink would write it.</summary>
    private static string Render(Serilog.Formatting.ITextFormatter? formatter)
    {
        formatter.Should().NotBeNull("a server writes this copy with a formatter of its own");

        using var activity = new Activity("probe").Start();
        var written = new StringWriter();

        formatter!.Format(new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Warning,
                exception: null,
                new MessageTemplateParser().Parse("a line about {What}"),
                new List<LogEventProperty> { new("CorrelationToken", new ScalarValue("1b2c3d4e")) },
                activity.TraceId,
                activity.SpanId),
            written);

        return written.ToString();
    }
}
