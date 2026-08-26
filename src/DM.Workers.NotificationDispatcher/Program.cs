using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DM.Workers.NotificationDispatcher;

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
            // Both validations, in every environment: a missing registration or
            // a process-wide component holding a scoped one is a defect of the
            // composition, not of the environment it surfaced in.
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseDefault<Startup>());
}
