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
            config.AddInMemoryCollection(TestConfiguration()));

        builder.ConfigureServices(services =>
        {
            // Register test authentication middleware via IStartupFilter
            // This runs BEFORE Startup.Configure(), so our middleware is added first
            services.AddSingleton<IStartupFilter, TestAuthenticationStartupFilter>();

            RemoveBackgroundServices(services);

            // Override HIBP password checker with a test stub
            // Remove the real HttpClient-based implementation
            var hibpDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(ICompromisedPasswordChecker));
            if (hibpDescriptor != null)
            {
                services.Remove(hibpDescriptor);
            }
            services.AddSingleton<ICompromisedPasswordChecker, TestCompromisedPasswordChecker>();

            ReplaceStores(services);
            RecordMail(services);
        });
    }

    private Dictionary<string, string?> TestConfiguration() => new()
    {
        // Point RabbitMQ to the test container, as its user: the development settings carry the stand's password.
        ["RabbitMqConfiguration:Endpoint"] = _databaseFixture.RabbitMqConnectionString,
        ["RabbitMqConfiguration:Username"] = DatabaseFixture.BrokerUsername,
        ["RabbitMqConfiguration:Password"] = DatabaseFixture.BrokerPassword,
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
    };

    // Remove background services that cause resource leaks or external connections in tests
    private static void RemoveBackgroundServices(IServiceCollection services)
    {
        var backgroundServicesToRemove = new[]
        {
            typeof(RealtimeNotificationConsumer), // RabbitMQ connection attempts
            typeof(WarmupService), // background warmup competes with tests for the pool
            typeof(TokenCleanupService), // Token cleanup uses DB — avoid race conditions
            typeof(SessionCleanupService), // periodic session purge — avoid race conditions
            typeof(RetentionSweepService), // periodic retention sweep — tests call the processor directly
            typeof(OutboxRelayService), // outbox relay — tests drive the processor directly, a live loop would claim their rows
            typeof(PendingRegistrationCleanupService), // DB cleanup — avoid race conditions
            typeof(UsernameChangeCleanupService), // DB cleanup — avoid race conditions
            // Period digests: the tests drive the processor directly, and a live loop would claim their periods.
            typeof(PeriodDigestService)
        };

        foreach (var serviceType in backgroundServicesToRemove)
        {
            var descriptor = services.FirstOrDefault(d => d.ImplementationType == serviceType);
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
        }
    }

    private void ReplaceStores(IServiceCollection services)
    {
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

        // The bucket, as a dictionary (see InMemoryObjectStorage): registered last, and RemoveAll on top.
        services.RemoveAll<DM.Domain.Core.Uploads.IObjectStorage>();
        services.AddSingleton<DM.Domain.Core.Uploads.IObjectStorage, InMemoryObjectStorage>();
    }

    // Every letter copied where a test can read it (see RecordingMailSender); the real sender keeps working underneath.
    private static void RecordMail(IServiceCollection services)
    {
        var mail = services.Last(d => d.ServiceType == typeof(DM.Domain.Core.Mail.IMailSender));
        services.Remove(mail);
        services.Add(ServiceDescriptor.Describe(
            typeof(DM.Domain.Core.Mail.IMailSender),
            provider => new RecordingMailSender(
                (DM.Domain.Core.Mail.IMailSender)ActivatorUtilities.CreateInstance(
                    provider, mail.ImplementationType!)),
            mail.Lifetime));
    }

    public static GeneralUser CreateTestUser(
        string username = TestConstants.TestUserLogin,
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

    public static GeneralUser CreateAdminUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.AdminUserId,
            Username = TestConstants.AdminUserLogin,
            Role = UserRole.Admin,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    public static GeneralUser CreateSecondUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.SecondUserId,
            Username = TestConstants.SecondUserLogin,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    public static GeneralUser CreateModeratorUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.ModeratorUserId,
            Username = TestConstants.ModeratorUserLogin,
            Role = UserRole.Moderator,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    // Create senior moderator user: the rank that removes and restores accounts
    public static GeneralUser CreateSeniorModeratorUser()
    {
        return new GeneralUser
        {
            UserId = TestConstants.SeniorModeratorUserId,
            Username = TestConstants.SeniorModeratorUserLogin,
            Role = UserRole.SeniorModerator,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }
}
