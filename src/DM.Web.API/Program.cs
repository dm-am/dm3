using System.Runtime.CompilerServices;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

[assembly: InternalsVisibleTo("DM.Web.API.IntegrationTests")]
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
            // UseSerilog() does not own the static logger, so nothing else flushes
            // the last batch: the lines that say why the process stopped.
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Create web host builder
    /// </summary>
    /// <returns>Configured host builder</returns>
    public static IHostBuilder CreateWebHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            // In every environment: a captive or missing registration is a defect
            // of the composition, not of the environment it surfaced in.
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<Startup>());
    }
}
