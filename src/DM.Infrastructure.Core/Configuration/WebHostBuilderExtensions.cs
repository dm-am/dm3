using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// Extensions for quick environment variables configuration provider setup
/// </summary>
public static class WebHostBuilderExtensions
{
    /// <summary>
    /// Enrich web host builder with DM configuration sources
    /// </summary>
    /// <param name="builder">Web host builder</param>
    /// <returns>Builder itself for chaining</returns>
    public static IHostBuilder WithDmConfiguration(this IHostBuilder builder) =>
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            var env = ctx.HostingEnvironment;
            var defaultNonJsonCfgs = cfg.Sources
                .Where(s => s is not JsonConfigurationSource)
                .ToArray();
            cfg.Sources.Clear();
            cfg
                .AddJsonFile("secrets/appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("commonCfg/appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"secrets/appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"commonCfg/appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);
            foreach (var defaultCfg in defaultNonJsonCfgs)
            {
                cfg.Add(defaultCfg);
            }
            cfg.AddEnvironmentVariables("DM_");
        });
}