using Autofac;
using DM.Domain.Account;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Blog.Authorization;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Search;
using DM.Domain.Forum.Authorization;
using DM.Domain.Game.Authorization;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Configuration;
using DM.Domain.Moderation.Authorization;
using DM.Domain.Moderation.Configuration;
using DM.Domain.Personal.Authorization;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Mail.Configuration;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Persistence;
using DM.Web.API.Shared.Binding;
using DM.Web.API.Shared.Configuration;
using DM.Web.API.Middleware;
using DM.Web.API.Realtime;
using DM.Web.API.Swagger;
using DM.Web.API.HostedServices;
using DM.Workers.SearchIndexer.Grpc;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
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
    private IHttpContextAccessor _httpContextAccessor = null!;
    private IBbParserProvider _bbParserProvider = null!;
    private bool _migrateOnStart;

    /// <summary>
    /// Configure application services
    /// </summary>
    /// <param name="services">Service collection</param>
    public void ConfigureServices(IServiceCollection services)
    {
        _migrateOnStart = configuration.GetValue<bool>("MigrateOnStart");

        services
            .AddOptions()
            .Configure<ConnectionStrings>(configuration.GetSection(nameof(ConnectionStrings)).Bind)
            .Configure<IntegrationSettings>(configuration.GetSection(nameof(IntegrationSettings)).Bind)
            .Configure<EmailConfiguration>(configuration.GetSection(nameof(EmailConfiguration)).Bind)
            .Configure<CdnConfiguration>(configuration.GetSection(nameof(CdnConfiguration)).Bind)
            .Configure<ImageProxyConfiguration>(configuration.GetSection(nameof(ImageProxyConfiguration)).Bind)
            .Configure<RabbitMqConfiguration>(configuration.GetSection(nameof(RabbitMqConfiguration)).Bind)
            .Configure<SearchServiceConfiguration>(configuration.GetSection(nameof(SearchServiceConfiguration)).Bind)
            .Configure<CryptoConfiguration>(configuration.GetSection(nameof(CryptoConfiguration)).Bind)
            .Configure<AuthenticationConfiguration>(configuration.GetSection(nameof(AuthenticationConfiguration)).Bind)
            .Configure<MessagingConfiguration>(configuration.GetSection(nameof(MessagingConfiguration)).Bind)
            .Configure<PasswordPolicyConfiguration>(configuration.GetSection(nameof(PasswordPolicyConfiguration)).Bind)
            .Configure<TokenConfiguration>(configuration.GetSection(nameof(TokenConfiguration)).Bind)
            .Configure<BotConfiguration>(configuration.GetSection(nameof(BotConfiguration)).Bind)
            .Configure<ProbationConfiguration>(configuration.GetSection(nameof(ProbationConfiguration)).Bind)
            .Configure<MirrorConfiguration>(configuration.GetSection(nameof(MirrorConfiguration)).Bind)
            .AddDmLogging("DM.API", configuration);

        // Validate critical configuration on startup � fail fast if misconfigured
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

        // X-Forwarded-* is honoured for the configured proxy networks only.
        // Without this every caller could name its own address, and that address
        // is what the login journal, the security audit and the suspicious-login
        // detector record.
        services.AddReverseProxySupport(configuration);

        // Register IProbationConfiguration interface for Domain modules
        services.AddSingleton<IProbationConfiguration>(sp =>
            sp.GetRequiredService<IOptions<ProbationConfiguration>>().Value);

        services
            // No AddAutoMapper here: the mapper is owned by the Autofac
            // registration in RegisterMapper, which is applied after Populate and
            // therefore always won. This call registered a second mapper — with an
            // empty profile set, because it was given no assemblies — and its
            // AllowNullCollections never took effect. The setting now lives in the
            // configuration that is actually built.
            .AddMemoryCache()
            .AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = System.Linq.Enumerable.Concat(
                    ResponseCompressionDefaults.MimeTypes,
                    new[] { "application/json", "application/problem+json" });
            })
            .Configure<BrotliCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest)
            .Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest)
            .AddResponseCaching()
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

        // HIBP (HaveIBeenPwned) password checker - NIST SP 800-63B compliance
        services.AddHttpClient<ICompromisedPasswordChecker, HibpPasswordChecker>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "DM3-PasswordChecker/1.0");
            client.DefaultRequestHeaders.Add("Add-Padding", "true"); // Enhanced privacy
            client.Timeout = TimeSpan.FromSeconds(5); // Don't block registration on slow API
        });

        services.AddJamqClient(config => config.UseRabbit());

        // Only register hosted services when NOT in migration mode
        // Migration mode runs migrations and exits - no need for cleanup services
        if (!_migrateOnStart)
        {
            services.AddHostedService<RealtimeNotificationConsumer>();
            services.AddHostedService<WarmupService>();

            // Asserts the Mongo index set. docker/mongo-init.js only runs on the first start
            // of an empty volume, so this is what keeps deployed databases indexed.
            services.AddHostedService<DM.Infrastructure.Persistence.MongoIntegration.MongoIndexInitializer>();

            services.AddHostedService<HostedServices.TokenCleanupService>();
            services.AddHostedService<HostedServices.SessionCleanupService>();
            services.AddHostedService<HostedServices.PendingRegistrationCleanupService>();
            services.AddHostedService<HostedServices.PeriodDigestService>();
            services.AddHostedService<HostedServices.UsernameChangeCleanupService>();
            services.AddHostedService<HostedServices.PendencyReminderService>();
            services.AddHostedService<HostedServices.GameInactivityService>();
            services.AddHostedService<HostedServices.PopularityScoreService>();
            services.AddHostedService<HostedServices.UploadOrphanCleanupService>();

            // The bucket initializer would run in migration mode too — so we register it
            // only in normal mode, because the migration container has no
            // S3 access (it depends only on postgres).
            services.AddHostedService<DM.Infrastructure.Core.Storage.StorageBucketInitializer>();
        }

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

                // Rate limit for username availability check: 20 requests per minute
                options.AddPolicy("username-check", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                // Rate limit for email availability check: 10 requests per minute
                options.AddPolicy("email-check", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                // Upload endpoint: 10 uploads per minute per authenticated user
                // (fallback to IP for guests; normally upload requires auth).
                // Anti-flood protection on top of the 10 MB size limit: even if
                // an attacker sends valid small files — no more than 10/min.
                options.AddPolicy("uploads", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User.Identity?.Name
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anon",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }));

                // Sliding window for expensive read endpoints (chat availability,
                // full-text search): 30 requests per minute per user/IP. Search
                // runs heavy tsvector scans against the primary OLTP database, so
                // it is a more realistic cost vector than the other read paths.
                options.AddPolicy("sliding", context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: context.User.Identity?.Name
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anon",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                        }));

                // Ordinary authenticated CRUD (preferences, subscriptions,
                // invitations, blacklists): 60 per minute per user, falling back
                // to IP. Tighter than the global 100/min because these are
                // per-account write paths, and partitioned by user so one noisy
                // account cannot consume a shared IP's budget.
                options.AddPolicy("default", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User.Identity?.Name
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anon",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }));
            }
            else
            {
                // No-op limiters for tests (unlimited)
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("auth", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("username-check", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("email-check", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("uploads", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("sliding", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("default", _ =>
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

        _httpContextAccessor = new HttpContextAccessor();
        _bbParserProvider = new BbParserProvider();

        services.AddSignalR();

        // Notification settings repository (for user notification preferences)
        services.AddSingleton<DM.Web.API.Notifications.UserSettingsRepository>();
        services.AddSingleton<DM.Web.API.Notifications.INotificationSettingsRepository>(sp =>
            sp.GetRequiredService<DM.Web.API.Notifications.UserSettingsRepository>());

        services
            .AddSwaggerGen(c => c.ConfigureGen())
            .AddMvc(config => config.ModelBinderProviders.Insert(0, new ReadableGuidBinderProvider()))
            .AddJsonOptions(config => config.Setup(_httpContextAccessor, _bbParserProvider));
    }

    /// <summary>
    /// Configure application container
    /// </summary>
    /// <param name="builder">Container builder</param>
    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();
        builder.RegisterMapper();

        builder.RegisterInstance(_httpContextAccessor)
            .AsSelf()
            .AsImplementedInterfaces();
        builder.RegisterInstance(_bbParserProvider)
            .AsSelf()
            .AsImplementedInterfaces();

        builder.RegisterModuleOnce<MessageQueuingModule>();

        builder.RegisterModuleOnce<PersistenceModule>();
        builder.RegisterModuleOnce<MailModule>();
        builder.RegisterModuleOnce<CoreModule>();

        // Register all Domain services centrally (replaces individual Module.cs files)
        RegisterDomainServices(builder);

        // Singleton for SignalR user connection tracking
        builder.RegisterType<UserConnectionService>()
            .AsImplementedInterfaces()
            .SingleInstance();

        // Unified comment service (routes to domain-specific implementations)
        builder.RegisterType<Shared.Comments.CommentService>()
            .As<DM.Domain.Core.Comments.ICommentService>()
            .InstancePerLifetimeScope();
    }

    /// <summary>
    /// Configure application
    /// </summary>
    /// <param name="appBuilder"></param>
    /// <param name="integrationOptions"></param>
    /// <param name="logger"></param>
    public void Configure(IApplicationBuilder appBuilder,
        IOptions<IntegrationSettings> integrationOptions,
        ILogger<Startup> logger)
    {
        if (_migrateOnStart)
        {
            // In a scope, not as a Configure parameter: the context is pooled, and
            // a parameter here leases one from the root container for the whole
            // process — never returned, so the pool runs one lease short forever.
            using var migrationScope = appBuilder.ApplicationServices.CreateScope();
            migrationScope.ServiceProvider.GetRequiredService<DmDbContext>().Database.Migrate();
            Environment.Exit(0);
        }
        
        // First in the pipeline: everything downstream — the rate limiter
        // partitions, the login journal, the security audit — reads the peer
        // address, so it has to be the client's before any of them run.
        appBuilder.UseForwardedHeaders();

        // Ahead of anything that writes a response: a security header is only
        // worth anything if it is on EVERY response. Registered after
        // UseSwaggerUI it was skipped for every statically served Swagger asset,
        // which is how the OWASP baseline scan found /swagger-ui.css with no
        // headers at all.
        appBuilder.UseMiddleware<SecurityHeadersMiddleware>();

        // Swagger only in development (security: hide API documentation in production)
        if (_environment.IsDevelopment())
        {
            appBuilder
                .UseSwagger(c => c.Configure())
                .UseSwaggerUI(c => c.ConfigureUi());
        }

        appBuilder
            .UseResponseCompression()
            .UseResponseCaching()
            .UseMiddleware<CorrelationMiddleware>()
            .UseMiddleware<ErrorHandlingMiddleware>()
            .UseCors(b => b
                .WithOrigins(integrationOptions.Value.CorsUrls)
                .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "X-Dm-Correlation-Token", "Cache-Control", "X-Dm-Audience", "x-signalr-user-agent")
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromHours(1)))
            .UseMiddleware<CsrfProtectionMiddleware>()
            .UseRouting()
            // After UseRouting on purpose: the limiter resolves its policy from
            // endpoint metadata, which routing is what populates. Registered
            // before it, every [EnableRateLimiting] attribute was inert and only
            // the global limiter ever ran.
            .UseRateLimiter()
            .UseAuthentication()
            .UseMiddleware<AuthenticationMiddleware>()
            .UseAuthorization()
            .UseEndpoints(c =>
            {
                c.MapControllers();
                c.MapHub<Notifications.NotificationHub>("/whatsup");
                c.MapPrometheusScrapingEndpoint("/metrics");

                // Liveness � Docker health check (no dependency checks)
                c.MapHealthChecks("/_health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = _ => false,
                    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
                });

                // Readiness � all "ready" dependencies (for load balancer)
                c.MapHealthChecks("/_ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready"),
                    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
                });

                // Detail � all checks (for monitoring dashboard)
                c.MapHealthChecks("/_health/detail", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
                });
            });
    }

    /// <summary>
    /// Register all Domain layer services centrally.
    /// This replaces individual Module.cs files in Domain.* projects.
    /// </summary>
    private static void RegisterDomainServices(ContainerBuilder builder)
    {
        // Domain assemblies to scan for services and AutoMapper profiles
        // Using Module classes as assembly markers (they are public)
        var accountAssembly = typeof(DM.Domain.Account.Authorization.AccountIntention).Assembly;
        var personalAssembly = typeof(UserIntention).Assembly;
        var communityAssembly = typeof(PollIntention).Assembly;
        var moderationAssembly = typeof(ModerationIntention).Assembly;
        var messagingAssembly = typeof(ChatIntention).Assembly;
        var forumAssembly = typeof(ForumIntention).Assembly;
        var blogAssembly = typeof(BlogIntention).Assembly;
        var gameAssembly = typeof(GameIntention).Assembly;

        var domainAssemblies = new[]
        {
            accountAssembly, personalAssembly, communityAssembly, moderationAssembly,
            messagingAssembly, forumAssembly, blogAssembly, gameAssembly
        };

        // Register types and AutoMapper profiles from all Domain assemblies
        foreach (var assembly in domainAssemblies)
        {
            builder.RegisterDefaultTypes(assembly);
            builder.RegisterMapper(assembly);
        }

        // Account-specific registrations (from AccountModule)
        // Classes are internal, so we use reflection to get types
        var identityProviderType = accountAssembly.GetType("DM.Domain.Account.Features.Identity.IdentityProvider")!;
        var loginAttemptTrackerType = accountAssembly.GetType("DM.Domain.Account.Features.Authentication.LoginAttemptTracker")!;
        var tokenFactoryType = accountAssembly.GetType("DM.Domain.Account.Features.Tokens.TokenFactory")!;

        builder.RegisterType(identityProviderType)
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterType(loginAttemptTrackerType)
            .AsImplementedInterfaces()
            .InstancePerDependency();

        builder.RegisterType(tokenFactoryType)
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // Moderation-specific registrations (from ModerationModule)
        var moderationIntentionResolverType = moderationAssembly.GetType("DM.Domain.Moderation.Authorization.ModerationIntentionResolver")!;
        builder.RegisterType(moderationIntentionResolverType)
            .As(typeof(IIntentionResolver<ModerationIntention>))
            .InstancePerLifetimeScope();
    }
}