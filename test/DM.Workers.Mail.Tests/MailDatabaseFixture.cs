using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// A PostgreSQL built by the migration, for the tests of the mail queue.
/// </summary>
/// <remarks>MigrateAsync rather than EnsureCreated: the migration is the database the site starts from.</remarks>
public class MailDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("dm3_mail")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    /// <summary>The database as a client outside the data layer reaches it.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    public DmDbContext CreateContext() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseNpgsql(ConnectionString)
        .Options);
}

/// <summary>
/// One container for the assembly. The mail sender writes every letter to the one mail queue, so
/// the tests that share it run one after another in this collection.
/// </summary>
[CollectionDefinition(Name)]
public class MailDatabaseCollection : ICollectionFixture<MailDatabaseFixture>
{
    public const string Name = "MailDatabase";
}
