using System;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A PostgreSQL built by the migration, shared by every test in the tier.
/// </summary>
/// <remarks>MigrateAsync rather than EnsureCreated: the migration is the database the site starts from.</remarks>
public class NotificationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("dm3_notifications")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private string _connectionString = string.Empty;

    /// <summary>The database as a client outside the data layer reaches it.</summary>
    public string ConnectionString => _connectionString;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await NotificationSeed.ApplyAsync(context);
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    public DmDbContext CreateContext() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseNpgsql(_connectionString)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
        .Options);

    /// <summary>An outbox row as it stands in the table now, untracked.</summary>
    public async Task<OutboxEvent> OutboxRow(long id)
    {
        await using var context = CreateContext();
        return await context.OutboxEvents.AsNoTracking().SingleAsync(row => row.Id == id);
    }

    /// <summary>Polls the condition every 50 ms until it holds, or fails after the given time.</summary>
    public static async Task WaitUntil(Func<Task<bool>> condition, TimeSpan within)
    {
        var deadline = DateTime.UtcNow + within;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Not reached within {within}");
    }
}

/// <summary>
/// One container for the whole assembly: tests read the seed or write under identifiers of their own.
/// </summary>
[CollectionDefinition(Name)]
public class NotificationDatabaseCollection : ICollectionFixture<NotificationDatabaseFixture>
{
    public const string Name = "NotificationDatabase";
}
