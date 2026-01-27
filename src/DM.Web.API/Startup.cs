using Autofac;
using DM.Services.Common;
using DM.Services.Community;
using DM.Services.Authentication.Configuration;
using DM.Services.Core.Configuration;
using DM.Services.Core.Extensions;
using DM.Services.Core.Logging;
using DM.Services.Core.Parsing;
using DM.Services.DataAccess;
using DM.Services.Forum;
using DM.Services.Gaming;
using DM.Services.MessageQueuing;
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
using OpenIddict.Validation.AspNetCore;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;

namespace DM.Web.API;

/// <summary>
/// Application
/// </summary>
internal class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    private readonly IWebHostEnvironment _environment = environment;
    private IHttpContextAccessor httpContextAccessor;
    private IBbParserProvider bbParserProvider;
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
            .AddDmLogging("DM.API", configuration);

        services
            .AddAutoMapper(config => config.AllowNullCollections = true)
            .AddMemoryCache()
            .AddDbContextPool<DmDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString(nameof(ConnectionStrings.Rdb)));
                options.UseOpenIddict();
            });

        // Configure OpenIddict
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<DmDbContext>();
            })
            .AddServer(options =>
            {
                // Enable the password and refresh token flows
                options.AllowPasswordFlow()
                    .AllowRefreshTokenFlow();

                // Set the token endpoint
                options.SetTokenEndpointUris("/connect/token")
                    .SetUserinfoEndpointUris("/connect/userinfo");

                // Accept anonymous clients (no client authentication required for password flow)
                options.AcceptAnonymousClients();

                // Register the signing and encryption credentials
                if (_environment.IsDevelopment())
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    var certPath = configuration["OpenIddict:CertificatePath"];
                    var certPassword = configuration["OpenIddict:CertificatePassword"];
                    if (!string.IsNullOrEmpty(certPath))
                    {
                        var cert = new X509Certificate2(certPath, certPassword);
                        options.AddEncryptionCertificate(cert)
                            .AddSigningCertificate(cert);
                    }
                    else
                    {
                        // Fallback to development certs with warning (should not happen in production)
                        options.AddDevelopmentEncryptionCertificate()
                            .AddDevelopmentSigningCertificate();
                    }
                }

                // Register the ASP.NET Core host and configure the ASP.NET Core-specific options
                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserinfoEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        // Configure OpenIddict authentication (Bearer tokens only)
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            })
            .AddDiscord("Discord", options =>
            {
                options.ClientId = configuration["Discord:ClientId"] ?? "";
                options.ClientSecret = configuration["Discord:ClientSecret"] ?? "";
                options.Scope.Add("identify");
                options.Scope.Add("email");
                options.SaveTokens = true;
            });

        services.AddJamqClient(config => config.UseRabbit());
        services.AddHostedService<RealtimeNotificationConsumer>();
        services.AddHostedService<WarmupService>();

        services.AddHealthChecks();

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
            }
            else
            {
                // No-op limiters for tests (unlimited)
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
                options.AddPolicy("auth", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
            }

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        httpContextAccessor = new HttpContextAccessor();
        bbParserProvider = new BbParserProvider();

        services.AddSignalR();

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

        builder.RegisterModuleOnce<CommonModule>();
        builder.RegisterModuleOnce<UploadingModule>();
        builder.RegisterModuleOnce<DataAccessModule>();

        builder.RegisterModuleOnce<CommunityModule>();
        builder.RegisterModuleOnce<ForumModule>();
        builder.RegisterModuleOnce<GamingModule>();
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
        
        appBuilder
            .UseSwagger(c => c.Configure())
            .UseSwaggerUI(c => c.ConfigureUi())
            .UseMiddleware<CorrelationMiddleware>()
            .UseMiddleware<ErrorHandlingMiddleware>()
            .UseCors(b => b
                .WithOrigins(integrationOptions.Value.CorsUrls)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials())
            .UseMiddleware<CsrfProtectionMiddleware>()
            .UseRateLimiter()
            .UseRouting()
            .UseAuthentication()
            .UseMiddleware<OpenIddictIdentityMiddleware>()
            .UseAuthorization()
            .UseHealthChecks("/_health")
            .UseEndpoints(c =>
            {
                c.MapControllers();
                c.MapHub<NotificationHub>("/whatsup");
                c.MapPrometheusScrapingEndpoint("/metrics");
            });
    }
}