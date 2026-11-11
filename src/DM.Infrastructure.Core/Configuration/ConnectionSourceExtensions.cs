using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DM.Infrastructure.Core.Configuration;

/// <summary>
/// The process's one source of database connections and the check that probes it.
/// </summary>
/// <remarks>
/// Here and not in the data layer, so a host that drains a queue without composing the data
/// layer, the mail worker, takes its connections from the same place as the others.
/// </remarks>
public static class ConnectionSourceExtensions
{
    /// <summary>
    /// The process's one source of database connections, built from ConnectionStrings:Rdb.
    /// </summary>
    /// <remarks>
    /// Everything in the process that talks to the database takes its connections here, so
    /// the process holds one pool and its size is the one the connection string names.
    /// </remarks>
    public static IServiceCollection AddDmConnectionSource(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => NpgsqlDataSource.Create(
            provider.GetRequiredService<IOptions<ConnectionStrings>>().Value.Rdb));
        return services;
    }

    /// <summary>
    /// The database check, tagged ready, over the process's own source, so it probes the pool
    /// the host draws from and not one of its own.
    /// </summary>
    public static IHealthChecksBuilder AddDmDatabaseCheck(this IHealthChecksBuilder checks) =>
        checks.AddNpgSql(
            provider => provider.GetRequiredService<NpgsqlDataSource>(),
            name: "postgresql",
            tags: ["db", "ready"]);
}
