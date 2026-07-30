using System;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Health checks of the API host.
/// </summary>
internal static class HealthCheckExtensions
{
    /// <summary>
    /// Registers a check per backing store the host depends on.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Source of the connection strings to probe.</param>
    public static IServiceCollection AddDmHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);
        var rabbitMq = new RabbitMqConfiguration();
        configuration.GetSection(nameof(RabbitMqConfiguration)).Bind(rabbitMq);

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionStrings.Rdb,
                name: "postgresql",
                tags: new[] { "db", "ready" })
            .AddMongoDb(
                mongodbConnectionString: connectionStrings.Mongo,
                name: "mongodb",
                tags: new[] { "db", "ready" })
            // Not "ready" here, unlike in the consumer workers: readiness answers
            // "can this instance serve a request", and every request is served
            // without the broker — publishing an event is fire-and-forget
            // alongside the response. Tagging it ready pulled the whole site out
            // of rotation over a delayed notification. A worker has the opposite
            // answer: taking messages off a queue is all it does.
            .AddRabbitMQ(
                rabbitConnectionString: new Uri(rabbitMq.Endpoint),
                name: "rabbitmq",
                tags: new[] { "messaging" });

        return services;
    }
}
