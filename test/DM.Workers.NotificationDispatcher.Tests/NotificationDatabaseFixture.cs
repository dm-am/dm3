using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
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
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("dm3_notifications")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private string _connectionString = string.Empty;

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
}

/// <summary>
/// One container for the whole assembly: tests read the seed or write under identifiers of their own.
/// </summary>
[CollectionDefinition(Name)]
public class NotificationDatabaseCollection : ICollectionFixture<NotificationDatabaseFixture>
{
    public const string Name = "NotificationDatabase";
}
