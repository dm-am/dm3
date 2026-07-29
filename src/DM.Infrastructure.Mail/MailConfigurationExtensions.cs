using DM.Infrastructure.Mail.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Configuration the mail module reads.
/// </summary>
public static class MailConfigurationExtensions
{
    /// <summary>
    /// Binds the SMTP settings.
    /// </summary>
    /// <remarks>
    /// Deliberately not validated on start. A host that cannot send mail still
    /// serves every page; the delivery failure belongs in the log of the send
    /// that failed, not in a refusal to boot.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the section from.</param>
    public static IServiceCollection AddDmMailConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailConfiguration>(
            configuration.GetSection(nameof(EmailConfiguration)).Bind);

        return services;
    }
}
