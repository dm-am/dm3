using DM.Domain.Account;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Configuration;
using DM.Domain.Messaging.Configuration;
using DM.Domain.Moderation;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Persistence;
using DM.Web.API.Features.General.Upload;
using DM.Web.API.Features.Personal.Webhooks;
using DM.Web.API.Notifications;
using DM.Web.API.Shared.Binding;
using DM.Web.API.Shared.Configuration;
using DM.Web.API.Shared.Http;
using DM.Web.API.Shared.RateLimiting;
using DM.Web.API.Shared.BotTrap;
using DM.Web.API.Shared.Sorting;
using DM.Web.API.Middleware;
using DM.Web.API.Realtime;
using DM.Web.API.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.AspNetCore;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DM.Web.API;

/// <summary>
/// Application
/// </summary>
internal class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    /// <summary>Request body limit, uploads included.</summary>
    private const long MaxRequestBodySize = 30 * 1024 * 1024;

    private bool _migrateOnStart;

    /// <summary>
    /// Response writer of every health endpoint, chosen once because the choice is
    /// a security one.
    /// </summary>
    /// <remarks>
    /// The endpoints carry no authentication, and the plain writer copies driver
    /// messages naming host, port, database and user into the body. Internal so a
    /// test can hold the writer to that.
    /// </remarks>
    internal static readonly Func<HttpContext, HealthReport, Task> HealthReportWriter =
        HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponseNoExceptionDetails;

    /// <summary>
    /// Configure application services
    /// </summary>
    /// <param name="services">Service collection</param>
    public void ConfigureServices(IServiceCollection services)
    {
        _migrateOnStart = configuration.GetValue<bool>("MigrateOnStart");

        // Each module binds and validates the options it reads.
        services
            .AddOptions()
            .AddDmCoreConfiguration(configuration)
            .AddDmMailConfiguration(configuration)
            .AddDmAccountConfiguration(configuration)
            .Configure<MessagingConfiguration>(configuration.GetSection(nameof(MessagingConfiguration)).Bind)
            .AddDmLogging("DM.API", configuration, environment)
            .RequireGeneratedLinks()
            .RequireRelationalStorage()
            .RequireObjectStorage()
            .ValidateDiscordPublicKey();

        AddSiteAddresses(services);

        // X-Forwarded-* only from the configured proxy networks: the address it names
        // is what the login journal and the security audit record.
        services.AddReverseProxySupport(configuration);

        services.Configure<SessionCookieConfiguration>(
            configuration.GetSection(nameof(SessionCookieConfiguration)).Bind);

        services.AddMemoryCache();
        AddResponseCompressionPolicy(services);
        services.AddResponseCaching();
        AddDatabase(services, _migrateOnStart);

        // BFF Pattern: Cookie-based authentication via HttpOnly cookies
        // No Bearer tokens - sessions managed server-side
        services.AddAuthentication();

        AddCompromisedPasswordChecker(services);
        AddBackgroundWork(services);

        services.AddDmHealthChecks();
        AddRequestBodyLimits(services);
        services.AddDmRateLimiting(configuration);
        services.AddDmSignalR();
        AddApiControllers(services);

        services.AddSingleton<IUserConnectionService, UserConnectionService>();

        // After everything wired explicitly: the scans only fill gaps. Lifetimes
        // that differ from the scan default are declared by the owning assembly.
        services
            .AddDmMail()
            .AddDmAccount()
            .AddDmModeration()
            .AddDmCore()
            .AddDmPersistence()
            .AddDefaultTypes(typeof(Startup).Assembly);

        RegisterDomainServices(services);
    }

    private void AddSiteAddresses(IServiceCollection services)
    {
        // CORS is an API concern and no other host has an opinion on it, so this
        // one stays with the host rather than moving into the core extension.
        services.AddOptions<SiteAddressConfiguration>()
            .Bind(configuration.GetSection(nameof(SiteAddressConfiguration)))
            // An empty list would refuse every browser at the door.
            .Validate(s => s.BrowserOrigins().Count > 0,
                "SiteAddressConfiguration:PublicUrl or SiteAddressConfiguration:Addresses must name at least one address")
            // A typo drops an address out of the derived list silently.
            .Validate(
                s => s.AdditionalOrigins.Prepend(s.PublicUrl)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .All(a => SiteAddressConfiguration.OriginOf(a) != null),
                "SiteAddressConfiguration: every declared address must be an absolute http or https URL")
            .ValidateOnStart();

        // The development server of the client, and only where one exists.
        if (environment.IsDevelopment())
        {
            services.PostConfigure<SiteAddressConfiguration>(s =>
                s.AdditionalOrigins = [.. s.AdditionalOrigins, "http://localhost:5173", "http://localhost:5174"]);
        }
    }

    private static void AddResponseCompressionPolicy(IServiceCollection services) => services
        .AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes
                .Concat([ApiContentTypes.Json, ApiContentTypes.ProblemJson]);
        })
        .Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest)
        .Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

    internal static void AddDatabase(IServiceCollection services, bool migrationMode) =>
        services.AddDmConnectionSource().AddDbContextPool<DmDbContext>((provider, options) =>
        {
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(),
                npgsqlOptions =>
                {
                    if (migrationMode)
                    {
                        // A migration step cut off or replayed is left half applied. Zero is
                        // set outright: left unset, Npgsql falls back to its own 30 seconds.
                        npgsqlOptions.CommandTimeout(0);
                        return;
                    }

                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                });
        });

    // HIBP (HaveIBeenPwned) password checker - NIST SP 800-63B compliance
    private static void AddCompromisedPasswordChecker(IServiceCollection services) =>
        services.AddHttpClient<ICompromisedPasswordChecker, HibpPasswordChecker>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "DM3-PasswordChecker/1.0");
            client.DefaultRequestHeaders.Add("Add-Padding", "true"); // Enhanced privacy
            client.Timeout = TimeSpan.FromSeconds(5); // Don't block registration on slow API
        });

    private void AddBackgroundWork(IServiceCollection services)
    {
        if (!_migrateOnStart)
        {
            services.AddDmHostedServices(HostedServiceExtensions.PeriodicServicesEnabled(configuration));
        }
    }

    private static void AddRequestBodyLimits(IServiceCollection services)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = MaxRequestBodySize;
        });
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = MaxRequestBodySize;
        });
    }

    private static void AddApiControllers(IServiceCollection services)
    {
        var httpContextAccessor = new HttpContextAccessor();
        var bbParserProvider = new BbParserProvider();

        services
            .AddSwaggerGen(c => c.ConfigureGen())
            .AddMvc(config =>
            {
                config.ModelBinderProviders.Insert(0, new ReadableGuidBinderProvider());
                // Without it an unknown ?sortBy= answers 200 in default order.
                config.Filters.Add<SortVocabularyFilter>();
                // The trap field of the forms open to guests (IHoneypotForm).
                config.Filters.Add<HoneypotFilter>();
            })
            .AddJsonOptions(config => config.Setup(httpContextAccessor, bbParserProvider));

        services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
        services.AddSingleton<IBbParserProvider>(bbParserProvider);
    }

    /// <summary>
    /// Configure application
    /// </summary>
    /// <param name="appBuilder">Application pipeline being assembled</param>
    /// <param name="siteAddresses">Addresses the site answers on, source of the CORS policy</param>
    /// <param name="logger">Logger of the startup itself</param>
    public void Configure(IApplicationBuilder appBuilder,
        IOptions<SiteAddressConfiguration> siteAddresses,
        ILogger<Startup> logger)
    {
        if (_migrateOnStart)
        {
            MigrateAndExit(appBuilder);
        }

        WarnWhenRateLimitingIsOff(logger);

        // First: everything downstream reads the peer address.
        appBuilder.UseForwardedHeaders();

        // Ahead of anything that writes a response, Swagger's static assets included.
        appBuilder.UseMiddleware<SecurityHeadersMiddleware>();

        UseSwaggerInDevelopment(appBuilder);

        appBuilder
            .UseResponseCompression()
            .UseMiddleware<CorrelationMiddleware>()
            // The one line per request: after the token, so it is on the line, and
            // above the error handler, so a failure is not logged twice.
            .UseSerilogRequestLogging(EnrichRequestLine)
            .UseMiddleware<ErrorHandlingMiddleware>()
            // Before the response cache, as ASP.NET requires: a cached entry stored
            // before CORS ran lacks Access-Control-Allow-Origin. Named origins give
            // Vary: Origin, so the cache keys entries by it.
            .UseCors(policy => SiteAddressCors(policy, siteAddresses.Value.BrowserOrigins()))
            .UseResponseCaching()
            .UseMiddleware<CsrfProtectionMiddleware>()
            .UseRouting()
            // The account the limiter counts by, read from the cookie: the
            // partitioner is synchronous and runs before any identity exists.
            .UseMiddleware<RateLimitAccountMiddleware>()
            // After routing, which populates the endpoint metadata the policies come
            // from; before authentication, so a flood costs no session lookup.
            .UseRateLimiter()
            .UseAuthentication()
            .UseMiddleware<AuthenticationMiddleware>()
            .UseAuthorization()
            .UseEndpoints(MapEndpoints);
    }

    private static void MigrateAndExit(IApplicationBuilder appBuilder)
    {
        // In a scope: a Configure parameter would lease a pooled context forever.
        using var migrationScope = appBuilder.ApplicationServices.CreateScope();
        migrationScope.ServiceProvider.GetRequiredService<DmDbContext>().Database.Migrate();

        // Environment.Exit runs no finally block, so the flush in Main never happens here.
        Serilog.Log.CloseAndFlush();
        Environment.Exit(0);
    }

    private void WarnWhenRateLimitingIsOff(ILogger<Startup> logger)
    {
        // The only trace of a deployment running without limits.
        if (!RateLimitingExtensions.IsEnabled(configuration))
        {
            logger.LogWarning(
                "Rate limiting is off: every policy is registered as a no-op limiter");
        }
    }

    // Swagger only in development (security: hide API documentation in production)
    private void UseSwaggerInDevelopment(IApplicationBuilder appBuilder)
    {
        if (environment.IsDevelopment())
        {
            appBuilder
                .UseSwagger(c => c.Configure())
                .UseSwaggerUI(c => c.ConfigureUi());
        }
    }

    private static void EnrichRequestLine(RequestLoggingOptions options) =>
        options.EnrichDiagnosticContext = (diagnostic, context) =>
        {
            diagnostic.Set("Host", context.Request.Host.Value);
            diagnostic.Set("Address", context.GetClientAddress());
        };

    private static void SiteAddressCors(CorsPolicyBuilder policy, IReadOnlyList<string> origins) => policy
        .WithOrigins([.. origins])
        // Both lists fail silently: an unlisted request header fails the preflight,
        // an unlisted response header stays unreadable to the page.
        .WithHeaders("Content-Type", "Authorization",
            CorrelationMiddleware.CorrelationTokenHeader, "Cache-Control", BbAudienceHeader.HeaderName,
            UploadApiService.IdempotencyHeader,
            // The SignalR client puts both on the negotiate call of the hub.
            "X-Requested-With", "x-signalr-user-agent",
            TokenHeaders.Account, TokenHeaders.Ticket)
        .WithExposedHeaders("Location", "Retry-After")
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .AllowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromHours(1));

    private static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        endpoints.MapHub<NotificationHub>("/hubs/notifications");
        endpoints.MapPrometheusScrapingEndpoint("/metrics");

        // Liveness - Docker health check (no dependency checks)
        endpoints.MapHealthChecks("/_health", HealthEndpoint(_ => false));

        // Readiness - all "ready" dependencies (for load balancer)
        endpoints.MapHealthChecks("/_ready",
            HealthEndpoint(check => check.Tags.Contains(HealthCheckExtensions.ReadyTag)));

        // Detail - all checks (for monitoring dashboard)
        endpoints.MapHealthChecks("/_health/detail", HealthEndpoint(null));
    }

    private static HealthCheckOptions HealthEndpoint(Func<HealthCheckRegistration, bool>? predicate) => new()
    {
        Predicate = predicate,
        ResponseWriter = HealthReportWriter
    };

    /// <summary>
    /// Register all Domain layer services centrally.
    /// </summary>
    private static void RegisterDomainServices(IServiceCollection services)
    {
        // Written out in full, so the list names every module it composes: a module
        // left out fails resolution only on the first request to it. The Intention
        // enums are the marker every Domain.* project is guaranteed to have.
        var accountAssembly = typeof(DM.Domain.Account.Authorization.AccountIntention).Assembly;
        var personalAssembly = typeof(DM.Domain.Personal.Authorization.UserIntention).Assembly;
        var communityAssembly = typeof(DM.Domain.Community.Authorization.PollIntention).Assembly;
        var moderationAssembly = typeof(DM.Domain.Moderation.Authorization.ModerationIntention).Assembly;
        var messagingAssembly = typeof(DM.Domain.Messaging.Authorization.ChatIntention).Assembly;
        var forumAssembly = typeof(DM.Domain.Forum.Authorization.BoardIntention).Assembly;
        var blogAssembly = typeof(DM.Domain.Blog.Authorization.BlogIntention).Assembly;
        var gameAssembly = typeof(DM.Domain.Game.Authorization.GameIntention).Assembly;

        var domainAssemblies = new[]
        {
            accountAssembly, personalAssembly, communityAssembly, moderationAssembly,
            messagingAssembly, forumAssembly, blogAssembly, gameAssembly
        };

        foreach (var assembly in domainAssemblies)
        {
            services.AddDefaultTypes(assembly);
        }
    }
}
