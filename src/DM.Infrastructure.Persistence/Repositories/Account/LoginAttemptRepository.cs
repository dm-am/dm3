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

    private static string NormalizeEmail(string email) => email.ToLowerInvariant();

    /// <inheritdoc />
    public async Task<int> GetFailedAttemptCount(string email)
    {
        var record = await Collection
            .Find(Filter.Eq(x => x.Id, NormalizeEmail(email)))
            .FirstOrDefaultAsync();
        return record?.FailedAttempts ?? 0;
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLockoutStart(string email)
    {
        var record = await Collection
            .Find(Filter.Eq(x => x.Id, NormalizeEmail(email)))
            .FirstOrDefaultAsync();
        return record?.LockoutStartUtc;
    }

    /// <inheritdoc />
    public async Task<int> RecordFailedAttempt(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var now = _dateTimeProvider.Now.UtcDateTime;

        var result = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(x => x.Id, normalizedEmail),
            Update
                .Inc(x => x.FailedAttempts, 1)
                .Set(x => x.LastAttemptUtc, now),
            new FindOneAndUpdateOptions<LoginAttempt>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            });

        return result?.FailedAttempts ?? 1;
    }

    /// <inheritdoc />
    public Task SetLockout(string email, DateTime lockoutStart)
    {
        return Collection.UpdateOneAsync(
            Filter.Eq(x => x.Id, NormalizeEmail(email)),
            Update.Set(x => x.LockoutStartUtc, lockoutStart),
            new UpdateOptions { IsUpsert = true });
    }

    /// <inheritdoc />
    public Task ResetAttempts(string email)
    {
        return Collection.DeleteOneAsync(
            Filter.Eq(x => x.Id, NormalizeEmail(email)));
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
