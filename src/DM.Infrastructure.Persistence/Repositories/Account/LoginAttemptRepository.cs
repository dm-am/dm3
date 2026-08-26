using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Authentication;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc cref="ILoginAttemptRepository"/>
internal class LoginAttemptRepository : ILoginAttemptRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public LoginAttemptRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<int> GetFailedAttemptCount(LoginAttemptOrigin origin)
    {
        var record = await _dbContext.LoginAttempts
            .TagWith("DM.Authentication.FailedAttemptCount")
            .FirstOrDefaultAsync(x => x.Key == origin.Key);
        return record?.FailedAttempts ?? 0;
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLockoutStart(LoginAttemptOrigin origin)
    {
        var record = await _dbContext.LoginAttempts
            .TagWith("DM.Authentication.LockoutStart")
            .FirstOrDefaultAsync(x => x.Key == origin.Key);
        return record?.LockoutStartUtc;
    }

    /// <inheritdoc />
    /// <remarks>
    /// One atomic statement, as FindOneAndUpdate was: two requests racing each
    /// other both land, the server serializes the increments, and each caller
    /// reads back the count its own attempt produced.
    /// </remarks>
    public async Task<int> RecordFailedAttempt(LoginAttemptOrigin origin)
    {
        var now = _dateTimeProvider.Now.UtcDateTime;

        // Email and address are only written on the insert that creates the row,
        // like SetOnInsert before: denormalized out of the composite key so a
        // successful login can clear every address at once.
        //
        // ToListAsync, not SingleAsync: an operator composed over SqlQuery wraps
        // the statement into a subquery, and INSERT ... RETURNING is not valid
        // inside one. Uncomposed, the statement runs as written and RETURNING
        // is the result set.
        var counts = await _dbContext.Database.SqlQuery<int>($"""
            INSERT INTO "LoginAttempts" ("Key", "Email", "IpAddress", "FailedAttempts", "LastAttemptUtc")
            VALUES ({origin.Key}, {origin.NormalizedEmail}, {origin.IpAddress}, 1, {now})
            ON CONFLICT ("Key") DO UPDATE SET
                "FailedAttempts" = "LoginAttempts"."FailedAttempts" + 1,
                "LastAttemptUtc" = {now}
            RETURNING "FailedAttempts" AS "Value"
            """).ToListAsync();
        return counts.Single();
    }

    /// <inheritdoc />
    public Task SetLockout(LoginAttemptOrigin origin, DateTime lockoutStart)
    {
        return _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LoginAttempts" ("Key", "Email", "IpAddress", "FailedAttempts", "LastAttemptUtc", "LockoutStartUtc")
            VALUES ({origin.Key}, {origin.NormalizedEmail}, {origin.IpAddress}, 0, {lockoutStart}, {lockoutStart})
            ON CONFLICT ("Key") DO UPDATE SET
                "LockoutStartUtc" = {lockoutStart}
            """);
    }

    /// <inheritdoc />
    public Task ResetAttempts(LoginAttemptOrigin origin) =>
        _dbContext.LoginAttempts
            .Where(x => x.Key == origin.Key)
            .ExecuteDeleteAsync();

    /// <inheritdoc />
    public Task ResetAttempts(string email)
    {
        // Every address, not only the one that succeeded: proving you know the
        // password clears the account's whole history of failed attempts.
        var normalized = email.ToLowerInvariant();
        return _dbContext.LoginAttempts
            .Where(x => x.Email == normalized)
            .ExecuteDeleteAsync();
    }
}
