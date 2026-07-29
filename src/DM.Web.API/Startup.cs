using Autofac;
using DM.Domain.Account;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Blog.Authorization;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
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
using DM.Web.API.Shared.RateLimiting;
using DM.Web.API.Middleware;
using DM.Web.API.Realtime;
using DM.Web.API.Swagger;
using DM.Web.API.HostedServices;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;

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

        // Each module binds and validates the options it reads. Restated here
        // per host, four of these types were bound twice and two of them three
        // times, and the workers bound a subset — which is how a worker came to
        // start with no broker endpoint and report itself healthy.
        services
            .AddOptions()
            .AddDmCoreConfiguration(configuration)
            .AddDmMessageQueuing(configuration)
            .AddDmMailConfiguration(configuration)
            .AddDmAccountConfiguration(configuration)
            .Configure<MessagingConfiguration>(configuration.GetSection(nameof(MessagingConfiguration)).Bind)
            .Configure<ProbationConfiguration>(configuration.GetSection(nameof(ProbationConfiguration)).Bind)
            .AddDmLogging("DM.API", configuration);

        // CORS is an API concern and no other host has an opinion on it, so this
        // one stays with the host rather than moving into the core extension.
        services.AddOptions<IntegrationSettings>()
            .Bind(configuration.GetSection(nameof(IntegrationSettings)))
            .Validate(s => s.CorsUrls?.Length > 0, "IntegrationSettings:CorsUrls is required")
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

        if (!_migrateOnStart)
        {
            services.AddDmHostedServices();
        }

        services.AddDmHealthChecks(configuration);

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

        services.AddDmRateLimiting(configuration);

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

        // Lifetimes that differ from the scan default are declared by the
        // assembly that owns the types, not restated here per host.
        builder.RegisterModuleOnce<DM.Domain.Account.AccountModule>();
        builder.RegisterModuleOnce<DM.Domain.Moderation.ModerationModule>();
    }

}