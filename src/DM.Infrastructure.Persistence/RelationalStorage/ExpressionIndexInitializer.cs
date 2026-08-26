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
/// This class is the authority for the index set it declares, re-asserts on every start and
/// therefore heals databases that already exist, and never drops anything — dropping is
/// destructive and belongs to an explicit operation, not to a startup hook.
///
/// Postgres compares the name and nothing else, so CREATE INDEX IF NOT EXISTS succeeds
/// against an index that is not unique, or not over lower(), or over another column
/// entirely, and reports success either way. So the definition is read back and compared
/// rather than inferred from the statement having run: healing reaches existence, and a
/// disagreement is reported instead — recreating an index under load is not a thing a
/// startup hook does behind anybody's back.
///
/// CREATE INDEX IF NOT EXISTS is idempotent, so a restart with everything in place costs one
/// catalogue lookup per index, plus one read of pg_indexes.
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
        // One name change request may await a moderator per account. The service
        // reads before it writes, and two requests sent at once both read nothing:
        // the queue would then hold two rows for one asking, and approving both
        // renames the account twice. Only Pending is constrained - an approval
        // stops being in flight when its link runs out rather than when a status
        // changes, and no index predicate can be written against the clock.
        new("IX_UsernameChangeRequests_UserId_Pending",
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_UsernameChangeRequests_UserId_Pending" ON "UsernameChangeRequests" ("UserId") WHERE "Status" = 0""",
            """CREATE UNIQUE INDEX "IX_UsernameChangeRequests_UserId_Pending" ON public."UsernameChangeRequests" USING btree ("UserId") WHERE ("Status" = 0)"""),
    ];

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task Assert(DmDbContext dbContext, ExpressionIndex index, CancellationToken ct)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(index.Statement, ct);

            // AS "Value": SingleOrDefault composes over the raw SQL, and a
            // composed scalar SqlQuery selects the column by exactly that name —
            // without the alias every assertion of this pass fell into the catch
            // below on every start, and nobody read what it logged.
            var actual = await dbContext.Database
                .SqlQuery<string>($"""SELECT indexdef AS "Value" FROM pg_indexes WHERE indexname = {index.Name}""")
                .SingleOrDefaultAsync(ct);

            if (actual == index.Definition)
            {
                _logger.LogDebug("Expression index asserted: {IndexName}", index.Name);
                return;
            }

            // The statement above said nothing about this: it matches on the name alone, so
            // an index of that name built any other way is reported as already there. What
            // is lost then is not speed but the invariant — two accounts differing only by
            // letter case can be registered in a race, and no later repair can decide which
            // of them owns the address.
            _logger.LogError(
                "Expression index {IndexName} exists with a different definition: {Definition}. " +
                "Case-insensitive uniqueness of the login and the address is not enforced",
                index.Name, actual ?? "absent");
        }
        catch (Exception exception)
        {
            // A failed startup takes the whole site down; this leaves it serving with the
            // invariant unenforced and says so loudly. Neither is good, and the second is
            // the recoverable one — but it is recovered by rebuilding the index, not by
            // waiting, because what slips through meanwhile cannot be undone afterwards.
            _logger.LogError(exception, "Could not assert expression index {IndexName}", index.Name);
        }
    }
}
