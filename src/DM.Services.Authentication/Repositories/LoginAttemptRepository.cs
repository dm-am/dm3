using System;
using System.Threading.Tasks;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;

namespace DM.Services.Authentication.Repositories;

/// <inheritdoc cref="ILoginAttemptRepository"/>
internal class LoginAttemptRepository : MongoCollectionRepository<LoginAttempts>, ILoginAttemptRepository
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public LoginAttemptRepository(
        DmMongoClient client,
        IDateTimeProvider dateTimeProvider) : base(client)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    private static string NormalizeLogin(string login) => login.ToLowerInvariant();

    /// <inheritdoc />
    public async Task<int> GetFailedAttemptCount(string login)
    {
        var record = await Collection
            .Find(Filter.Eq(x => x.Id, NormalizeLogin(login)))
            .FirstOrDefaultAsync();
        return record?.FailedAttempts ?? 0;
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLockoutStart(string login)
    {
        var record = await Collection
            .Find(Filter.Eq(x => x.Id, NormalizeLogin(login)))
            .FirstOrDefaultAsync();
        return record?.LockoutStartUtc;
    }

    /// <inheritdoc />
    public async Task<int> RecordFailedAttempt(string login)
    {
        var normalizedLogin = NormalizeLogin(login);
        var now = _dateTimeProvider.Now.UtcDateTime;

        var result = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(x => x.Id, normalizedLogin),
            Update
                .Inc(x => x.FailedAttempts, 1)
                .Set(x => x.LastAttemptUtc, now),
            new FindOneAndUpdateOptions<LoginAttempts>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            });

        return result?.FailedAttempts ?? 1;
    }

    /// <inheritdoc />
    public Task SetLockout(string login, DateTime lockoutStart)
    {
        return Collection.UpdateOneAsync(
            Filter.Eq(x => x.Id, NormalizeLogin(login)),
            Update.Set(x => x.LockoutStartUtc, lockoutStart),
            new UpdateOptions { IsUpsert = true });
    }

    /// <inheritdoc />
    public Task ResetAttempts(string login)
    {
        return Collection.DeleteOneAsync(
            Filter.Eq(x => x.Id, NormalizeLogin(login)));
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
