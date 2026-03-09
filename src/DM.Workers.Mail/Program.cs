using Autofac.Extensions.DependencyInjection;
using DM.Infrastructure.Core.Configuration;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DM.Workers.Mail;

class Program
{
    static void Main(string[] args)
    {
        CreateWebHostBuilder(args)
            .WithDmConfiguration()
            .Build().Run();
    }

    /// <summary>
    /// Create web host builder
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static IHostBuilder CreateWebHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<Startup>());
}