using DM.Infrastructure.Core.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// The host every background worker runs in
/// </summary>
public static class WorkerHost
{
    /// <summary>
    /// Builds the worker host around its startup class and runs it until it stops
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <typeparam name="TStartup">Startup class of the worker</typeparam>
    public static void Run<TStartup>(string[] args)
        where TStartup : class
    {
        try
        {
            Host.CreateDefaultBuilder(args)
                // In every environment: a broken composition is not a property of one.
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<TStartup>())
                .WithDmConfiguration()
                .Build().Run();
        }
        finally
        {
            // UseSerilog() does not own the static logger, and the Loki sink buffers its last batch.
            Log.CloseAndFlush();
        }
    }
}
