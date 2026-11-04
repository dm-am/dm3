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
/// Asserted at startup so the migration stays entirely generated. Never drops anything.
/// CREATE INDEX IF NOT EXISTS matches on the name alone, so the definition is read back
/// and a disagreement is logged rather than repaired.
/// </remarks>
public class ExpressionIndexInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpressionIndexInitializer> _logger;

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

        foreach (var index in Declared)
        {
            await Assert(dbContext, index, cancellationToken);
        }
    }

    /// <summary>
    /// One index of the set: what to run, and what the catalogue has to answer afterwards.
    /// </summary>
    /// <param name="Name">Index name, which is all Postgres compares.</param>
    /// <param name="Statement">Statement that creates it.</param>
    /// <param name="Definition">
    /// The definition Postgres reports for it, verbatim. Written out rather than derived
    /// from the statement: the server normalises what it was given — schema qualification,
    /// the access method, the cast of the argument — so the only text that can be compared
    /// is the text the server produces.
    /// </param>
    internal sealed record ExpressionIndex(string Name, string Statement, string Definition);

    /// <summary>
    /// Login and registration compare lower(column) to lower(value). A b-tree over the raw
    /// column cannot serve that predicate and the lookup seq-scans Users on every attempt.
    /// Unique over the expression also carries the invariant the product needs: two accounts
    /// must not differ by letter case alone.
    /// </summary>
    /// <remarks>
    /// Deliberately without an IsRemoved predicate: an account holds its address and its
    /// login for as long as its row exists, deactivation included. Shared.Users.
    /// AccountReservation says why, and every check that answers "is this free" reads the
    /// table the same way. A predicate here would free the pair in the schema alone.
    /// </remarks>
    internal static readonly ExpressionIndex[] Declared =
    [
        new("IX_Users_Email_Lower",
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email_Lower" ON "Users" (lower("Email"))""",
            """CREATE UNIQUE INDEX "IX_Users_Email_Lower" ON public."Users" USING btree (lower(("Email")::text))"""),
        new("IX_Users_Username_Lower",
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username_Lower" ON "Users" (lower("Username"))""",
            """CREATE UNIQUE INDEX "IX_Users_Username_Lower" ON public."Users" USING btree (lower(("Username")::text))"""),
    ];

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task Assert(DmDbContext dbContext, ExpressionIndex index, CancellationToken ct)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(index.Statement, ct);

            // AS "Value": a composed scalar SqlQuery selects the column by that name.
            var actual = await dbContext.Database
                .SqlQuery<string>($"""SELECT indexdef AS "Value" FROM pg_indexes WHERE indexname = {index.Name}""")
                .SingleOrDefaultAsync(ct);

            if (actual == index.Definition)
            {
                return;
            }

            // An index of that name built another way leaves case-insensitive uniqueness unenforced.
            _logger.LogError(
                "Expression index {IndexName} exists with a different definition: {Definition}. " +
                "Case-insensitive uniqueness of the login and the address is not enforced",
                index.Name, actual ?? "absent");
        }
        catch (Exception exception)
        {
            // Serving with the invariant unenforced and saying so beats taking the site down.
            _logger.LogError(exception, "Could not assert expression index {IndexName}", index.Name);
        }
    }
}
