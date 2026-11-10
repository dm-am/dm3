using DM.Infrastructure.Mail.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Configuration the mail module reads.
/// </summary>
public static class MailConfigurationExtensions
{
    /// <remarks>
    /// Not validated on start: a host that cannot send mail still serves every page.
    /// </remarks>
    public static IServiceCollection AddDmMailConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailConfiguration>(
            configuration.GetSection(nameof(EmailConfiguration)).Bind);

        return services;
    }
}
