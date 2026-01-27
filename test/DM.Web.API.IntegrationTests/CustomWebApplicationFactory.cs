using System.Security.Claims;
using System.Text.Encodings.Web;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Web.API.Notifications;
using DM.Web.API.Warmup;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DM.Web.API.IntegrationTests;

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
            new Claim(ClaimTypes.Name, testUser.Login),
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
    /// Optional: Test user to authenticate as
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
                // Discord OAuth test configuration (required by Startup)
                ["Discord:ClientId"] = "test-client-id",
                ["Discord:ClientSecret"] = "test-client-secret",
                // Disable RabbitMQ to avoid connection errors
                ["RabbitMqConfiguration:HostName"] = "localhost",
                // Disable rate limiting in tests
                ["RateLimiting:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove background services that cause resource leaks or external connections in tests
            var backgroundServicesToRemove = new[]
            {
                typeof(RealtimeNotificationConsumer), // RabbitMQ connection attempts
                typeof(WarmupService) // MongoDB warmup connection
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

            // Register DbContext with PostgreSQL connection string from Testcontainers
            services.AddDbContext<DmDbContext>(options =>
            {
                options.UseNpgsql(_databaseFixture.ConnectionString);
            }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);

        });

        // ConfigureTestServices runs AFTER all other configuration
        // This overrides the authentication scheme to bypass OpenIddict
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
        // Configure Autofac
        builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());

        // This callback runs AFTER Startup.ConfigureContainer, so our registrations win
        builder.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            // Mock event producer (RabbitMQ)
            var mockEventProducer = new Mock<IInvokedEventProducer>();
            mockEventProducer
                .Setup(p => p.Send(It.IsAny<EventType>(), It.IsAny<Guid>()))
                .Returns(Task.CompletedTask);
            containerBuilder.RegisterInstance(mockEventProducer.Object).As<IInvokedEventProducer>();

            // Mock consumer builder to prevent any RabbitMQ connection attempts
            var mockConsumerBuilder = new Mock<IConsumerBuilder>();
            containerBuilder.RegisterInstance(mockConsumerBuilder.Object).As<IConsumerBuilder>();

            // If TestUser is set, override identity provider in Autofac
            if (TestUser != null)
            {
                var authenticatedUser = new AuthenticatedUser
                {
                    UserId = TestUser.UserId,
                    Login = TestUser.Login,
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
                    "test-token");

                var testIdentityProvider = new TestIdentityProvider(identity);

                // Register as scoped to match original registration, but always return same identity
                containerBuilder.Register(_ => testIdentityProvider)
                    .As<IIdentityProvider>()
                    .As<IIdentitySetter>()
                    .InstancePerLifetimeScope();
            }
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Create a test user for authenticated requests
    /// </summary>
    public static GeneralUser CreateTestUser(
        string login = TestConstants.TestUserLogin,
        UserRole role = UserRole.RegularUser)
    {
        return new GeneralUser
        {
            UserId = TestConstants.TestUserId,
            Login = login,
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
            Login = TestConstants.AdminUserLogin,
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
            Login = TestConstants.SecondUserLogin,
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
            Login = TestConstants.ModeratorUserLogin,
            Role = UserRole.Moderator,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }
}
