using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.MongoIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically cleans up expired sessions from MongoDB
/// </summary>
internal class SessionCleanupService : PeriodicHostedService
{
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Session Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Session Cleanup] Starting session cleanup");

        var mongoClient = scope.GetRequiredService<DmMongoClient>();
        var collection = mongoClient.GetCollection<UserSession>();

        var now = DateTime.UtcNow;

        // Remove expired sessions from the Sessions array using $pull
        var pullFilter = Builders<UserSession>.Filter.Empty;
        var pullUpdate = Builders<UserSession>.Update.PullFilter(
            s => s.Sessions,
            session => session.ExpirationUtc < now);

        var pullResult = await collection.UpdateManyAsync(
            pullFilter,
            pullUpdate,
            cancellationToken: cancellationToken);

        if (pullResult.ModifiedCount > 0)
        {
            _logger.LogInformation("[Session Cleanup] Removed expired sessions from {Count} user(s)",
                pullResult.ModifiedCount);
        }

        // Remove UserSession documents with empty Sessions arrays
        var emptyFilter = Builders<UserSession>.Filter.Or(
            Builders<UserSession>.Filter.Eq(u => u.Sessions, null),
            Builders<UserSession>.Filter.Size(u => u.Sessions, 0));

        var deleteResult = await collection.DeleteManyAsync(
            emptyFilter,
            cancellationToken: cancellationToken);

        if (deleteResult.DeletedCount > 0)
        {
            _logger.LogInformation("[Session Cleanup] Deleted {Count} UserSession document(s) with no active sessions",
                deleteResult.DeletedCount);
        }

        if (pullResult.ModifiedCount == 0 && deleteResult.DeletedCount == 0)
        {
            _logger.LogDebug("[Session Cleanup] No sessions to clean up");
        }
    }
}
