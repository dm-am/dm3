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
    public static IHostBuilder CreateWebHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<Startup>());
}
