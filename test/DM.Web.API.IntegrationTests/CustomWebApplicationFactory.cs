using System.Security.Claims;
using System.Text.Encodings.Web;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Web.API.HostedServices;
using DM.Web.API.Realtime;
using Microsoft.AspNetCore.Authentication;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
/// Test identity provider that returns a pre-configured identity
/// </summary>
internal class TestIdentityProvider : IIdentityProvider, IIdentitySetter
{
    private IIdentity _identity;

    public TestIdentityProvider(IIdentity identity)
    {
        _identity = identity;
    }

    public IIdentity Current
    {
        get => _identity;
        set => _identity = value;
    }

    public void Refresh()
    {
        // No-op for tests
    }
}

/// <summary>
/// Options for test authentication handler
/// </summary>
internal class TestAuthOptions : AuthenticationSchemeOptions
{
    public GeneralUser? TestUser { get; set; }
}

/// <summary>
/// Test authentication handler that creates claims from TestUser
/// </summary>
internal class TestAuthHandler : AuthenticationHandler<TestAuthOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<TestAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var testUser = Options.TestUser;
        if (testUser == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("No test user configured"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, testUser.UserId.ToString()),
            new Claim(ClaimTypes.Name, testUser.Username),
            new Claim(ClaimTypes.Role, testUser.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
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

    /// <summary>
    /// Test authentication token - used to authenticate test requests (when infrastructure is working)
    /// </summary>
    internal const string TestAuthToken = "test-auth-token-for-integration-tests";

    /// <summary>
    /// Optional: Test user to authenticate as.
    /// Note: Due to Autofac registration order issues, authenticated tests are currently skipped.
    /// </summary>
    public GeneralUser? TestUser { get; set; }

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
                // Point MongoDB to the test container
                ["ConnectionStrings:Mongo"] = _databaseFixture.MongoConnectionString,
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
                typeof(WarmupService), // MongoDB warmup connection
                typeof(TokenCleanupService), // Token cleanup uses DB — avoid race conditions
                typeof(SessionCleanupService), // Session cleanup uses MongoDB
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

            // Remove all DbContext-related registrations from the real app
            var dbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<DmDbContext>) ||
                            d.ServiceType == typeof(DmDbContext) ||
                            d.ServiceType.FullName?.Contains("DbContextPool") == true ||
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

            // Register DbContext with PostgreSQL connection string from Testcontainers
            services.AddDbContext<DmDbContext>(options =>
            {
                options.UseNpgsql(_databaseFixture.ConnectionString)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        });

        // This overrides the authentication scheme for authenticated tests
        if (TestUser != null)
        {
            var testUser = TestUser; // Capture for closure
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                        options.DefaultScheme = "Test";
                    })
                    .AddScheme<TestAuthOptions, TestAuthHandler>("Test", opts =>
                    {
                        opts.TestUser = testUser;
                    });
            });
        }
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Wrap with Autofac and configure our test overrides
        // This must be called first to ensure the app uses Autofac
        builder.UseServiceProviderFactory(new AutofacServiceProviderFactory(containerBuilder =>
        {
            // This runs AFTER all ConfigureContainer callbacks, as the final step before building
            ConfigureTestContainer(containerBuilder);
        }));

        return base.CreateHost(builder);
    }

    private void ConfigureTestContainer(ContainerBuilder containerBuilder)
    {
        // Override DmMongoClient to use the test container connection string
            var mongoConnectionString = _databaseFixture.MongoConnectionString;
            var mongoUrl = MongoUrl.Create(mongoConnectionString);
            var mongoSettings = MongoClientSettings.FromUrl(mongoUrl);
            mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            mongoSettings.ConnectTimeout = TimeSpan.FromSeconds(10);
            mongoSettings.RetryWrites = true;
            mongoSettings.RetryReads = true;
            mongoSettings.ClusterConfigurator = cb => cb.Subscribe(
                new DiagnosticsActivityEventSubscriber(new InstrumentationOptions { CaptureCommandText = true }));
            containerBuilder.RegisterInstance(new DmMongoClient(mongoSettings, mongoUrl))
                .AsSelf()
                .AsImplementedInterfaces();

            // If TestUser is set, override authentication services in Autofac
            // TODO: This approach doesn't work reliably due to Autofac registration order issues.
            // The production IdentityProvider registration from AuthenticationModule runs AFTER
            // this ConfigureContainer callback, overwriting our test registration.
            // Consider implementing one of these alternatives:
            // 1. Use real login flow: create test user in DB, call login endpoint, use returned session
            // 2. Add a test-only middleware that bypasses authentication for tests
            // 3. Use Microsoft DI overrides instead of Autofac
            if (TestUser != null)
            {
                var authenticatedUser = new AuthenticatedUser
                {
                    UserId = TestUser.UserId,
                    Username = TestUser.Username,
                    Role = TestUser.Role,
                    AccessPolicy = TestUser.AccessPolicy,
                    Salt = "fakesalt",
                    PasswordHash = "fakehash",
                    PasswordHashVersion = 2
                };

                var identity = Identity.Success(
                    authenticatedUser,
                    new Session { Id = Guid.NewGuid() },
                    UserSettings.Default,
                    TestAuthToken);

                var testIdentityProvider = new TestIdentityProvider(identity);

                // This registration is overwritten by AuthenticationModule.
                // Keeping for documentation purposes.
                containerBuilder.RegisterInstance(testIdentityProvider)
                    .As<IIdentityProvider>()
                    .As<IIdentitySetter>()
                    .SingleInstance();
            }
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
