using System.Text.Encodings.Web;
using System.Text.Unicode;
using DM.Domain.Core.Configuration;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Mail.Rendering;

/// <summary>
/// Builds the renderer the email templates are rendered by.
/// </summary>
/// <remarks>
/// Shared with the test that guards the templates, which must render through the same renderer.
/// </remarks>
internal static class EmailRendering
{
    /// <summary>
    /// A renderer with its own container: the templates need almost nothing from
    /// the application's services, and a Blazor renderer holding the application
    /// scope would outlive it.
    /// </summary>
    /// <param name="loggerFactory">Logger factory of the host</param>
    /// <param name="siteAddresses">Addresses of the site, configuration read once at startup.</param>
    public static HtmlRenderer CreateHtmlRenderer(
        ILoggerFactory loggerFactory,
        SiteAddressConfiguration siteAddresses)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(siteAddresses);

        // The default encoder escapes every Cyrillic letter; markup characters are escaped either way.
        services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

        return new HtmlRenderer(services.BuildServiceProvider(), loggerFactory);
    }
}
