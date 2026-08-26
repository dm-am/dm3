using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Web.API.HostedServices;
using DM.Web.API.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once RedundantUsingDirective - used by TestAuthenticationStartupFilter
using IStartupFilter = Microsoft.AspNetCore.Hosting.IStartupFilter;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Test stub for ICompromisedPasswordChecker that always returns false (password is safe).
/// This prevents integration tests from failing due to HIBP API responses.
/// </summary>
internal class TestCompromisedPasswordChecker : ICompromisedPasswordChecker
{
    public Task<bool> IsCompromisedAsync(string password) => Task.FromResult(false);
}

/// <summary>
/// Custom WebApplicationFactory for integration tests.
///
/// Uses PostgreSQL via Testcontainers for 100% compatibility with production.
/// The container is managed by DatabaseFixture and shared across all tests.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DatabaseFixture _databaseFixture;

    public CustomWebApplicationFactory(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add test configuration overrides
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Point RabbitMQ to the test container
                ["RabbitMqConfiguration:Endpoint"] = _databaseFixture.RabbitMqConnectionString,
                // Disable rate limiting in tests
                ["RateLimiting:Enabled"] = "false",
                // The encryption key has no default in the repository, so the host
                // refuses to start without one. A fixed throwaway key keeps the
                // tests deterministic and is never a deployment's key.
                ["CryptoConfiguration:KeyBase64"] = "ZG0zLWludGVncmF0aW9uLXRlc3RzLXRocm93YXdheS0=",
                // Lower lockout threshold and disable progressive delays for faster tests
                ["AuthenticationConfiguration:AccountLockoutThreshold"] = "5",
                ["AuthenticationConfiguration:LoginDelaySchedule:0:0"] = "1000",
                ["AuthenticationConfiguration:LoginDelaySchedule:0:1"] = "0",
                ["AuthenticationConfiguration:LoginDelaySchedule:1:0"] = "1000",
                ["AuthenticationConfiguration:LoginDelaySchedule:1:1"] = "0",
                ["AuthenticationConfiguration:LoginDelaySchedule:2:0"] = "1000",
                ["AuthenticationConfiguration:LoginDelaySchedule:2:1"] = "0"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Register test authentication middleware via IStartupFilter
            // This runs BEFORE Startup.Configure(), so our middleware is added first
            services.AddSingleton<IStartupFilter, TestAuthenticationStartupFilter>();

            // Remove background services that cause resource leaks or external connections in tests
            var backgroundServicesToRemove = new[]
            {
                typeof(RealtimeNotificationConsumer), // RabbitMQ connection attempts
                typeof(WarmupService), // background warmup competes with tests for the pool
                typeof(TokenCleanupService), // Token cleanup uses DB — avoid race conditions
                typeof(SessionCleanupService), // periodic session purge — avoid race conditions
                typeof(RetentionSweepService), // periodic retention sweep — tests call the processor directly
                typeof(OutboxRelayService), // outbox relay — tests drive the processor directly, a live loop would claim their rows
                typeof(PendingRegistrationCleanupService), // DB cleanup — avoid race conditions
                typeof(UsernameChangeCleanupService) // DB cleanup — avoid race conditions
            };

            foreach (var serviceType in backgroundServicesToRemove)
            {
                var descriptor = services.FirstOrDefault(d => d.ImplementationType == serviceType);
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
            }

            // Remove all DbContext-related registrations from the real app. The
            // lease is part of the pool's plumbing: left behind, it asks for the
            // removed pool, which the validating build refuses at startup.
            var dbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<DmDbContext>) ||
                            d.ServiceType == typeof(DmDbContext) ||
                            d.ServiceType.FullName?.Contains("DbContextPool") == true ||
                            d.ServiceType.FullName?.Contains("DbContextLease") == true ||
                            d.ServiceType.FullName?.Contains("DbContextOptions") == true)
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Override HIBP password checker with a test stub
            // Remove the real HttpClient-based implementation
            var hibpDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(ICompromisedPasswordChecker));
            if (hibpDescriptor != null)
            {
                services.Remove(hibpDescriptor);
            }
            services.AddSingleton<ICompromisedPasswordChecker, TestCompromisedPasswordChecker>();

            // Register DbContext with PostgreSQL connection string from Testcontainers.
            // EnableRetryOnFailure mirrors Startup: with a retrying execution strategy
            // EF refuses a user-initiated transaction, so code that opens one without
            // going through CreateExecutionStrategy fails in production and nowhere else.
            services.AddDbContext<DmDbContext>(options =>
            {
                options.UseNpgsql(_databaseFixture.ConnectionString,
                        npgsql => npgsql.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);

            // The bucket, as a dictionary. WebApplicationFactory runs this
            // callback after Startup.ConfigureServices, so the registration here
            // is the last descriptor and the one the host resolves — see
            // InMemoryObjectStorage for why S3 is the one dependency not
            // containerised. RemoveAll on top, so the scanned pairs cannot even
            // sit on the enumerable.
            services.RemoveAll<DM.Domain.Core.Uploads.IObjectStorage>();
            services.AddSingleton<DM.Domain.Core.Uploads.IObjectStorage, InMemoryObjectStorage>();
        });
    }

    /// <summary>
    /// Create a test user for authenticated requests
    /// </summary>
    public static GeneralUser CreateTestUser(
        string username = TestConstants.TestUserUsername,
        UserRole role = UserRole.RegularUser)
    {
        return new GeneralUser
        {
            UserId = TestConstants.TestUserId,
            Username = username,
            Role = role,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    /// <summary>
    /// Create admin user for authenticated requests
    /// </summary>
    public static GeneralUser CreateAdminUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.AdminUserId,
            Username = TestConstants.AdminUserUsername,
            Role = UserRole.Admin,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    /// <summary>
    /// Create second test user for multi-user scenarios
    /// </summary>
    public static GeneralUser CreateSecondUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.SecondUserId,
            Username = TestConstants.SecondUserUsername,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    /// <summary>
    /// Create moderator user for moderation scenarios
    /// </summary>
    public static GeneralUser CreateModeratorUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.ModeratorUserId,
            Username = TestConstants.ModeratorUserUsername,
            Role = UserRole.Moderator,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }
}
