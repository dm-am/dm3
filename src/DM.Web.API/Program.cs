using System.Runtime.CompilerServices;
using Autofac.Extensions.DependencyInjection;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

[assembly: InternalsVisibleTo("DM.Web.API.IntegrationTests")]
[assembly: InternalsVisibleTo("DM.Web.API.Tests")]

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
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<Startup>());
    }
}