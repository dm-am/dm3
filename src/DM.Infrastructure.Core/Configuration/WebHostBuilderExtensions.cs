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
                // secrets/ is intentionally absent from the repository (.gitignore) and
                // from every compose file: it is the operator-supplied drop-in that the
                // secrets convention names next to the DM_* variables. Nothing mounts it
                // because it is not meant to be baked into an image.
                // reloadOnChange is false on every source, including these two. Nothing
                // in the tree reads IOptionsMonitor or IOptionsSnapshot, so every
                // consumer holds the snapshot taken at first resolution: the flag bought
                // a file watcher and the promise that editing the secrets file applies,
                // which it never did, and ValidateOnStart would not see the new value
                // either. Hot reload is a decision about consumers, not a flag here.
                .AddJsonFile("secrets/appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"secrets/appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);
            foreach (var defaultCfg in defaultNonJsonCfgs)
            {
                cfg.Add(defaultCfg);
            }
            cfg.AddEnvironmentVariables("DM_");
        });
}
