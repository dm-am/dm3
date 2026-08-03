using System;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Authentication;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc cref="ILoginAttemptRepository"/>
internal class LoginAttemptRepository : MongoCollectionRepository<LoginAttempt>, ILoginAttemptRepository
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public LoginAttemptRepository(
        DmMongoClient client,
        IDateTimeProvider dateTimeProvider) : base(client)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<int> GetFailedAttemptCount(LoginAttemptOrigin origin)
    {
        var record = await Collection
            .Find(Filter.Eq(x => x.Id, origin.Key))
            .FirstOrDefaultAsync();
        return record?.FailedAttempts ?? 0;
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLockoutStart(LoginAttemptOrigin origin)
    {
        var record = await Collection
            .Find(Filter.Eq(x => x.Id, origin.Key))
            .FirstOrDefaultAsync();
        return record?.LockoutStartUtc;
    }

    /// <inheritdoc />
    public async Task<int> RecordFailedAttempt(LoginAttemptOrigin origin)
    {
        var now = _dateTimeProvider.Now.UtcDateTime;

        var result = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(x => x.Id, origin.Key),
            Update
                .Inc(x => x.FailedAttempts, 1)
                .Set(x => x.LastAttemptUtc, now)
                // Denormalized out of the composite id so a successful login can
                // clear every address at once, and so the record stays readable.
                .SetOnInsert(x => x.Email, origin.NormalizedEmail)
                .SetOnInsert(x => x.IpAddress, origin.IpAddress),
            new FindOneAndUpdateOptions<LoginAttempt>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            });

        return result?.FailedAttempts ?? 1;
    }

    /// <inheritdoc />
    public Task SetLockout(LoginAttemptOrigin origin, DateTime lockoutStart)
    {
        return Collection.UpdateOneAsync(
            Filter.Eq(x => x.Id, origin.Key),
            Update
                .Set(x => x.LockoutStartUtc, lockoutStart)
                .SetOnInsert(x => x.Email, origin.NormalizedEmail)
                .SetOnInsert(x => x.IpAddress, origin.IpAddress),
            new UpdateOptions { IsUpsert = true });
    }

    /// <inheritdoc />
    public Task ResetAttempts(LoginAttemptOrigin origin) =>
        Collection.DeleteOneAsync(Filter.Eq(x => x.Id, origin.Key));

    /// <inheritdoc />
    public Task ResetAttempts(string email)
    {
        // Every address, not only the one that succeeded: proving you know the
        // password clears the account's whole history of failed attempts.
        return Collection.DeleteManyAsync(
            Filter.Eq(x => x.Email, email.ToLowerInvariant()));
    }

    /// <inheritdoc />
    public Task CleanupExpiredRecords(int expirationHours)
    {
        var cutoff = _dateTimeProvider.Now.UtcDateTime.AddHours(-expirationHours);
        return Collection.DeleteManyAsync(
            Filter.Lt(x => x.LastAttemptUtc, cutoff) &
            Filter.Eq(x => x.LockoutStartUtc, null));
    }
}
