using System.Runtime.CompilerServices;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

[assembly: InternalsVisibleTo("DM.Web.API.IntegrationTests")]
[assembly: InternalsVisibleTo("DM.Web.API.Tests")]
// The composition-root rule builds this host's container next to the two workers',
// and that project is the only one referencing all of them.
[assembly: InternalsVisibleTo("DM.Architecture.Tests")]

namespace DM.Web.API;

/// <summary>
/// Hosting
/// </summary>
public class Program
{
    /// <summary>
    /// Main
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args)
    {
        try
        {
            CreateWebHostBuilder(args)
                .WithDmConfiguration()
                .Build().Run();
        }
        finally
        {
            // UseSerilog() borrows the static logger and does not take ownership
            // (dispose: false), so shutdown disposes nothing and the Loki sink,
            // which ships on a timer, dies with its last batch still buffered —
            // the lines that say why the process stopped.
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Create web host builder
    /// </summary>
    /// <param name="args"></param>
    /// <returns>Configured host builder</returns>
    public static IHostBuilder CreateWebHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            // Both validations, in every environment: a missing registration or
            // a process-wide component holding a scoped one is a defect of the
            // composition, not of the environment it surfaced in. Under the
            // Autofac factory both flags were dead - MS.DI never built the
            // final provider - which is how a captive DbContext once reached
            // production.
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<Startup>());
    }
}
