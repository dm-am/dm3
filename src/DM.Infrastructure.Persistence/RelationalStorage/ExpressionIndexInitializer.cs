using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Declares and asserts the relational indexes EF Core cannot express in the model.
/// </summary>
/// <remarks>
/// EF has no model-level expression index, so an index over <c>lower(column)</c> can only
/// reach the database as raw SQL. Written into the migration by hand it becomes content the
/// migration generator does not know about, and regenerating the migration — the documented
/// way to change the schema — drops it silently. Asserting it here instead keeps the
/// migration entirely generated, which is what makes regenerating it safe.
///
/// Same contract as <see cref="MongoIntegration.MongoIndexInitializer" />: this class is the
/// authority for the index set it declares, it re-asserts on every start and therefore heals
/// databases that already exist, and it never drops anything — dropping is destructive and
/// belongs to an explicit operation, not to a startup hook.
///
/// CREATE INDEX IF NOT EXISTS is idempotent, so a restart with everything in place costs one
/// catalogue lookup per index.
/// </remarks>
public class ExpressionIndexInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpressionIndexInitializer> _logger;

    /// <inheritdoc />
    public ExpressionIndexInitializer(
        IServiceProvider serviceProvider,
        ILogger<ExpressionIndexInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // Login and registration compare lower(column) to lower(value). A b-tree over the
        // raw column cannot serve that predicate and the lookup seq-scans Users on every
        // attempt. Unique over the expression also carries the invariant the product needs:
        // two accounts must not differ by letter case alone.
        await Assert(dbContext,
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email_Lower" ON "Users" (lower("Email"))""",
            "IX_Users_Email_Lower", cancellationToken);
        await Assert(dbContext,
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username_Lower" ON "Users" (lower("Username"))""",
            "IX_Users_Username_Lower", cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task Assert(DmDbContext dbContext, string statement, string indexName, CancellationToken ct)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, ct);
            _logger.LogDebug("Expression index asserted: {IndexName}", indexName);
        }
        catch (Exception exception)
        {
            // A missing index degrades a query; a failed startup takes the whole site down.
            // The degradation is loud in the log and visible in query plans, so it is the
            // better of the two.
            _logger.LogError(exception, "Could not assert expression index {IndexName}", indexName);
        }
    }
}
