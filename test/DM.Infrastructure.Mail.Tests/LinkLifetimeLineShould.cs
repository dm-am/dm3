using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail.ViewModels;
using DM.Infrastructure.Mail.Rendering;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

// A letter that mails a link says how long it lives in the hours it is handed: the hours the server
// holds the link to, out of the same setting.
public class LinkLifetimeLineShould : IAsyncDisposable
{
    private readonly HtmlRenderer _htmlRenderer = EmailRendering.CreateHtmlRenderer(
        NullLoggerFactory.Instance, new SiteAddressConfiguration { PublicUrl = "https://dm.am" });

    // Hours other than the defaults, each with the noun its number takes.
    public static TheoryData<object, string> Letters() => new()
    {
        { new RegistrationConfirmationViewModel("https://dm.am/activate/a", 21), "Ссылка для выбора имени и завершения регистрации действительна 21 час" },
        { new PasswordResetConfirmationViewModel("https://dm.am/reset/b", 24), "Ссылка для изменения пароля действительна 24 часа" },
        { new UsernameChangeApprovalViewModel("https://dm.am/username/d", 12), "Ссылка для изменения имени действительна 12 часов" },
    };

    [Theory]
    [MemberData(nameof(Letters))]
    public async Task SayTheHoursTheLetterIsHanded(object model, string sentence)
    {
        var html = await Render(model);

        html.Should().Contain(sentence);
        html.Should().NotContain("48", "the number comes with the model, not with the template");
    }

    private Task<string> Render(object model)
    {
        // The template is looked up by the type RenderAsync is called with, so it is called with the model's own.
        var method = typeof(TemplateRenderer).GetMethod(nameof(TemplateRenderer.RenderAsync))!
            .MakeGenericMethod(model.GetType());
        return (Task<string>)method.Invoke(new TemplateRenderer(_htmlRenderer), [model])!;
    }

    public async ValueTask DisposeAsync()
    {
        await _htmlRenderer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
