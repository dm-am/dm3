using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// The health endpoints answer without authentication of their own, so whoever
/// reaches the port reads the report. What it must never carry is the message of
/// the exception a failed check caught: those come from the drivers and spell out
/// host, port, database and user.
/// </summary>
public class HealthReportWriterShould
{
    private const string ConnectionDetails = "Host=dm-postgres;Port=5432;Database=dm3;Username=dm_app";

    [Fact]
    public async Task KeepTheExceptionOfAFailedCheckOutOfTheBody()
    {
        var body = await Write(new InvalidOperationException(ConnectionDetails));

        body.Should().NotContain(ConnectionDetails,
            "an endpoint with no authentication must not hand out what the check connects to");
        body.Should().Contain("Unhealthy",
            "the report still has to name the failure, only not its address");
    }

    private static async Task<string> Write(Exception failure)
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = new HealthReportEntry(
                    HealthStatus.Unhealthy, "postgres", TimeSpan.Zero, failure, null)
            },
            TimeSpan.Zero);

        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await Startup.HealthReportWriter(context, report);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return await reader.ReadToEndAsync();
    }
}
