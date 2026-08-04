using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Mail.Rendering;

/// <summary>
/// Builds the renderer the email templates are rendered by.
/// </summary>
/// <remarks>
/// One place because there are two callers — the Autofac module and the test that
/// guards the templates — and a test rendering through a differently configured
/// renderer guards nothing.
/// </remarks>
internal static class EmailRendering
{
    /// <summary>
    /// A renderer with its own container: the templates need nothing from the
    /// application's services, and a Blazor renderer holding the application
    /// scope would outlive it.
    /// </summary>
    /// <param name="loggerFactory">Logger factory of the host</param>
    public static HtmlRenderer CreateHtmlRenderer(ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The default encoder escapes everything outside Basic Latin, which is
        // every letter of a Russian name: "Аллигатор" left as &#x410;&#x43B;… —
        // correct, three times longer, and unlike the literal text of the template
        // beside it. All of Unicode is allowed through; angle brackets, ampersand
        // and quotes are still escaped, and that is what keeps markup in a username
        // from reaching the letter.
        services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

        return new HtmlRenderer(services.BuildServiceProvider(), loggerFactory);
    }
}
