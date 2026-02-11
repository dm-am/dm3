using Autofac;
using DM.Services.Common;
using DM.Services.Authentication.Configuration;
using DM.Services.Community;
using DM.Services.Community.Configuration;
using DM.Services.Core.Configuration;
using DM.Services.Core.Extensions;
using DM.Services.Core.Logging;
using DM.Services.Core.Parsing;
using DM.Services.DataAccess;
using DM.Services.Forum;
using DM.Services.Game;
using DM.Services.MessageQueuing;
using DM.Services.MessageQueuing.Outbox;
using DM.Services.Notifications;
using DM.Services.Uploading;
using DM.Services.Uploading.Configuration;
using DM.Web.API.Binding;
using DM.Web.API.Configuration;
using DM.Web.API.Middleware;
using DM.Web.API.Notifications;
using DM.Web.API.Swagger;
using DM.Web.API.Warmup;
using DM.Web.Core;
using DM.Web.Core.Middleware;
using DM.Services.Search.Grpc;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading.RateLimiting;

namespace DM.Web.API;

/// <summary>
/// Application
/// </summary>
internal class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    private readonly IWebHostEnvironment _environment = environment;
    private IHttpContextAccessor httpContextAccessor = null!;
    private IBbParserProvider bbParserProvider = null!;
    private bool migrateOnStart;

    /// <summary>
    /// Configure application services
    /// </summary>
    /// <param name="services">Service collection</param>
    public void ConfigureServices(IServiceCollection services)
    {
        migrateOnStart = configuration.GetValue<bool>("MigrateOnStart");

        services
            .AddOptions()
            .Configure<ConnectionStrings>(configuration.GetSection(nameof(ConnectionStrings)).Bind)
            .Configure<IntegrationSettings>(configuration.GetSection(nameof(IntegrationSettings)).Bind)
            .Configure<EmailConfiguration>(configuration.GetSection(nameof(EmailConfiguration)).Bind)
            .Configure<CdnConfiguration>(configuration.GetSection(nameof(CdnConfiguration)).Bind)
            .Configure<RabbitMqConfiguration>(configuration.GetSection(nameof(RabbitMqConfiguration)).Bind)
            .Configure<SearchServiceConfiguration>(configuration.GetSection(nameof(SearchServiceConfiguration)).Bind)
            .Configure<CryptoConfiguration>(configuration.GetSection(nameof(CryptoConfiguration)).Bind)
            .Configure<AuthenticationConfiguration>(configuration.GetSection(nameof(AuthenticationConfiguration)).Bind)
            .Configure<MessagingConfiguration>(configuration.GetSection(nameof(MessagingConfiguration)).Bind)
            .Configure<PasswordPolicyConfiguration>(configuration.GetSection(nameof(PasswordPolicyConfiguration)).Bind)
            .Configure<TokenConfiguration>(configuration.GetSection(nameof(TokenConfiguration)).Bind)
            .Configure<BotConfiguration>(configuration.GetSection(nameof(BotConfiguration)).Bind)
            .Configure<ProbationConfiguration>(configuration.GetSection(nameof(ProbationConfiguration)).Bind)
            .Configure<OutboxConfiguration>(configuration.GetSection(nameof(OutboxConfiguration)).Bind)
            .Configure<MirrorConfiguration>(configuration.GetSection(nameof(MirrorConfiguration)).Bind)
            .AddDmLogging("DM.API", configuration);

        // Validate critical configuration on startup — fail fast if misconfigured
        services.AddOptions<ConnectionStrings>()
            .Bind(configuration.GetSection(nameof(ConnectionStrings)))
            .Validate(cs => !string.IsNullOrEmpty(cs.Rdb) && !string.IsNullOrEmpty(cs.Mongo),
                "ConnectionStrings:Rdb and ConnectionStrings:Mongo are required")
            .ValidateOnStart();
        services.AddOptions<IntegrationSettings>()
            .Bind(configuration.GetSection(nameof(IntegrationSettings)))
            .Validate(s => s.CorsUrls?.Length > 0, "IntegrationSettings:CorsUrls is required")
            .ValidateOnStart();
        services.AddOptions<RabbitMqConfiguration>()
            .Bind(configuration.GetSection(nameof(RabbitMqConfiguration)))
            .Validate(r => !string.IsNullOrEmpty(r.Endpoint), "RabbitMqConfiguration:Endpoint is required")
            .ValidateOnStart();

        services
            .AddAutoMapper(config => config.AllowNullCollections = true)
            .AddMemoryCache()
            .AddDbContextPool<DmDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString(nameof(ConnectionStrings.Rdb)),
                    npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null);
                        npgsqlOptions.CommandTimeout(30);
                    });
            });

        // BFF Pattern: Cookie-based authentication via HttpOnly cookies
        // No Bearer tokens - sessions managed server-side
        services.AddAuthentication();

        services.AddJamqClient(config => config.UseRabbit());
        services.AddHostedService<RealtimeNotificationConsumer>();
        services.AddHostedService<WarmupService>();
        services.AddHostedService<Cleanup.TokenCleanupService>();
        services.AddHostedService<Cleanup.SessionCleanupService>();
        services.AddHostedService<Cleanup.PendingRegistrationCleanupService>();

        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);
        var rabbitMqConfig = new RabbitMqConfiguration();
        configuration.GetSection(nameof(RabbitMqConfiguration)).Bind(rabbitMqConfig);

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionStrings.Rdb,
                name: "postgresql",
                tags: new[] { "db", "ready" })
            .AddMongoDb(
                mongodbConnectionString: connectionStrings.Mongo,
                name: "mongodb",
                tags: new[] { "db", "ready" })
            .AddRabbitMQ(
                rabbitConnectionString: new Uri(rabbitMqConfig.Endpoint),
                name: "rabbitmq",
                tags: new[] { "messaging", "ready" });

        // Request size limits to prevent DoS attacks via large payloads
        // Default: 30MB for general requests, files handled separately by upload endpoints
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB
        });
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 30 * 1024 * 1024; // 30 MB for file uploads
        });

        // Rate limiting to prevent abuse (can be disabled via configuration for tests)
        var rateLimitingEnabled = configuration.GetValue("RateLimiting:Enabled", true);
        services.AddRateLimiter(options =>
        {
            if (rateLimitingEnabled)
            {
                // Global rate limit: 100 requests per minute per IP
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                // Strict rate limit for authentication endpoints: 5 requests per minute
                options.AddPolicy("auth", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                // Rate limit for login availability check: 20 requests per minute
                options.AddPolicy("login-check", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            }
            else
            {
                // No-op limiters for tests (unlimited)
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("auth", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("login-check", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
            }

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }
                else
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "Rate limit exceeded. Please retry after the specified time."
                }, ct);
            };
        });

        // gRPC client for search service with connection pooling
        var searchConfig = configuration.GetSection(nameof(SearchServiceConfiguration)).Get<SearchServiceConfiguration>();
        if (!string.IsNullOrEmpty(searchConfig?.GrpcEndpoint))
        {
            services.AddGrpcClient<SearchEngine.SearchEngineClient>(o =>
                o.Address = new Uri(searchConfig.GrpcEndpoint));
        }

        httpContextAccessor = new HttpContextAccessor();
        bbParserProvider = new BbParserProvider();

        services.AddSignalR();

        // Notification settings repository (for user notification preferences)
        services.AddSingleton<DM.Web.API.Notifications.UserSettingsRepository>();
        services.AddSingleton<DM.Web.API.Notifications.INotificationSettingsRepository>(sp =>
            sp.GetRequiredService<DM.Web.API.Notifications.UserSettingsRepository>());

        services
            .AddSwaggerGen(c => c.ConfigureGen())
            .AddMvc(config => config.ModelBinderProviders.Insert(0, new ReadableGuidBinderProvider()))
            .AddJsonOptions(config => config.Setup(httpContextAccessor, bbParserProvider));
    }

    /// <summary>
    /// Configure application container
    /// </summary>
    /// <param name="builder">Container builder</param>
    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();
        builder.RegisterMapper();

        builder.RegisterInstance(httpContextAccessor)
            .AsSelf()
            .AsImplementedInterfaces();
        builder.RegisterInstance(bbParserProvider)
            .AsSelf()
            .AsImplementedInterfaces();

        // Register MessageQueuingModule with OutboxProcessor enabled (requires DbContext)
        builder.RegisterModuleOnce(new MessageQueuingModule(enableOutboxProcessor: true));

        builder.RegisterModuleOnce<CommonModule>();
        builder.RegisterModuleOnce<UploadingModule>();
        builder.RegisterModuleOnce<DataAccessModule>();

        builder.RegisterModuleOnce<CommunityModule>();
        builder.RegisterModuleOnce<ForumModule>();
        builder.RegisterModuleOnce<GameModule>();
        builder.RegisterModuleOnce<NotificationsModule>();

        builder.RegisterModuleOnce<WebCoreModule>();
    }

    /// <summary>
    /// Configure application
    /// </summary>
    /// <param name="appBuilder"></param>
    /// <param name="integrationOptions"></param>
    /// <param name="dbContext"></param>
    /// <param name="logger"></param>
    public void Configure(IApplicationBuilder appBuilder,
        IOptions<IntegrationSettings> integrationOptions,
        DmDbContext dbContext,
        ILogger<Startup> logger)
    {
        if (migrateOnStart)
        {
            dbContext.Database.Migrate();
            Environment.Exit(0);
        }
        
        // Swagger only in development (security: hide API documentation in production)
        if (_environment.IsDevelopment())
        {
            appBuilder
                .UseSwagger(c => c.Configure())
                .UseSwaggerUI(c => c.ConfigureUi());
        }

        appBuilder
            .UseMiddleware<SecurityHeadersMiddleware>()
            .UseMiddleware<CorrelationMiddleware>()
            .UseMiddleware<ErrorHandlingMiddleware>()
            .UseCors(b => b
                .WithOrigins(integrationOptions.Value.CorsUrls)
                .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "X-Dm-Correlation-Token", "X-Bot-Api-Key", "Cache-Control", "x-dm-bb-render-mode", "x-signalr-user-agent")
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromHours(1)))
            .UseMiddleware<CsrfProtectionMiddleware>()
            .UseMiddleware<BotApiKeyMiddleware>()
            .UseRateLimiter()
            .UseRouting()
            .UseAuthentication()
            .UseMiddleware<AuthenticationMiddleware>()
            .UseAuthorization()
            .UseEndpoints(c =>
            {
                c.MapControllers();
                c.MapHub<NotificationHub>("/whatsup");
                c.MapPrometheusScrapingEndpoint("/metrics");

                // Liveness — Docker health check (no dependency checks)
                c.MapHealthChecks("/_health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = _ => false,
                    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
                });

                // Readiness — all "ready" dependencies (for load balancer)
                c.MapHealthChecks("/_ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready"),
                    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
                });

                // Detail — all checks (for monitoring dashboard)
                c.MapHealthChecks("/_health/detail", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
                });
            });
    }
}