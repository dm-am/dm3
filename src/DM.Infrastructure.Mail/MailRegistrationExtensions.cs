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
    public static IServiceCollection AddDmMail(this IServiceCollection services)
    {
        // Per scope: the scope disposes the channel, and a channel is not shared between threads.
        services.TryAddScoped<IMailSender, MailSender>();

        // One renderer per process: the Dispatcher of HtmlRenderer serializes the renders.
        services.TryAddSingleton<HtmlRenderer>(provider => EmailRendering.CreateHtmlRenderer(
            provider.GetRequiredService<ILoggerFactory>(),
            provider.GetRequiredService<IOptions<SiteAddressConfiguration>>().Value));
        services.TryAddSingleton<ITemplateRenderer, TemplateRenderer>();

        services.AddDefaultTypes(typeof(MailRegistrationExtensions).Assembly);

        return services;
    }
}
