using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.MongoIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically cleans up expired sessions from MongoDB
/// </summary>
internal class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    public SessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Session Cleanup] Service started. Will run every {Interval} hour(s)", _cleanupInterval.TotalHours);

        using var timer = new PeriodicTimer(_cleanupInterval);

        // Run initial cleanup on startup
        await CleanupSessions(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await CleanupSessions(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping, this is expected
                _logger.LogInformation("[Session Cleanup] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Session Cleanup] Unexpected error in cleanup loop");
                // Continue running despite errors
            }
        }
    }

    private async Task CleanupSessions(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Session Cleanup] Starting session cleanup");

            using var scope = _serviceProvider.CreateScope();
            var mongoClient = scope.ServiceProvider.GetRequiredService<DmMongoClient>();
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
        catch (OperationCanceledException)
        {
            throw; // Re-throw to be caught by outer handler
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Session Cleanup] Error during session cleanup");
            // Don't throw - we want the service to continue running
        }
    }
}
