using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Mail.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Services of the mail module.
/// </summary>
public static class MailRegistrationExtensions
{
    /// <summary>
    /// Registers the mail services and the assembly's default types.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmMail(this IServiceCollection services)
    {
        // One sender per scope: the channel is opened on the first send and
        // closed only in Dispose, and per-dependency there was nobody to close
        // it. Not a singleton - one channel may not be published to from
        // several threads at once.
        services.TryAddScoped<IMailSender, MailSender>();

        // One renderer per process. The template map is built by reflection over the
        // assembly and does not change, and the HtmlRenderer under it is a singleton
        // anyway: its own Dispatcher, which every call goes through, serializes the
        // renders.
        services.TryAddSingleton<HtmlRenderer>(provider => EmailRendering.CreateHtmlRenderer(
            provider.GetRequiredService<ILoggerFactory>(),
            provider.GetRequiredService<IOptions<SiteAddressConfiguration>>().Value));
        services.TryAddSingleton<ITemplateRenderer, TemplateRenderer>();

        services.AddDefaultTypes(typeof(MailRegistrationExtensions).Assembly);

        return services;
    }
}
