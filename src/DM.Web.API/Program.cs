using System.Runtime.CompilerServices;
using Autofac.Extensions.DependencyInjection;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

[assembly: InternalsVisibleTo("DM.Web.API.IntegrationTests")]

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
        CreateWebHostBuilder(args)
            .WithDmConfiguration()
            .Build().Run();
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