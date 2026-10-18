using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using DM.Infrastructure.Mail;
using DM.Workers.Mail.Sending;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using Xunit;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// Every letter leaves in two halves: the HTML as it was, and the same letter
/// in plain text.
/// </summary>
/// <remarks>
/// The letters are the real ones, rendered through the registrations the hosts
/// use: the text half is read off whatever the templates draw, so a reading
/// tested against hand-written markup would pass while a template drew
/// something it does not understand.
///
/// The HTML half is held to the byte. A client that draws HTML draws the last
/// alternative, which is this one, so the letter looks exactly as it did only
/// if the part is exactly what it was.
/// </remarks>
public class LetterBodyShould : IAsyncDisposable
{
    private const string Footnote = "Письмо отправлено автоматически и не требует ответа";

    private readonly ServiceProvider _services;

    public LetterBodyShould()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<SiteAddressConfiguration>>(Options.Create(
            new SiteAddressConfiguration { PublicUrl = "https://dm.am" }));
        services.AddDmMail();
        _services = services.BuildServiceProvider();
    }

    /// <summary>One of every letter the product sends, every optional part filled.</summary>
    public static TheoryData<object> EveryLetter() => new()
    {
        new RegistrationConfirmationViewModel("https://dm.am/activate#token=a"),
        new PasswordResetConfirmationViewModel("https://dm.am/reset#token=b"),
        new EmailChangeConfirmationViewModel("https://dm.am/email#token=c"),
        new PasswordChangeNotificationViewModel("https://dm.am/support"),
        new SuspiciousLoginViewModel("203.0.113.9", "Firefox на Linux", "30.07.2026 15:00", "Россия"),
        new UsernameChangeApprovalViewModel("https://dm.am/username#token=d"),
        new UsernameChangeRejectionViewModel("имя занято"),
        new EmailChangeWarningViewModel("now@dm.am", "next@dm.am"),
        new EmailChangeCompletedViewModel("before@dm.am", "next@dm.am", "https://dm.am/support"),
        new AccountRemovedViewModel("https://dm.am/support"),
        new AccountRestoredViewModel("https://dm.am/login"),
        new TwoFactorRemovalRequestViewModel("https://dm.am/two-factor#token=e"),
        new TwoFactorRemovalScheduledViewModel("13.10.2026 15:00", "https://dm.am/two-factor#token=f"),
    };

    [Fact]
    public void CoverEveryLetterThereIs()
    {
        var models = typeof(RegistrationConfirmationViewModel).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(RegistrationConfirmationViewModel).Namespace
                           && type is { IsClass: true, IsAbstract: false })
            .ToHashSet();

        EveryLetter().Select(row => row.Data.GetType()).ToHashSet().Should().BeEquivalentTo(models,
            "a letter left out here is a letter whose text half nobody looked at");
    }

    [Theory]
    [MemberData(nameof(EveryLetter))]
    public async Task SendTheTextFirstAndTheHtmlUnchangedLast(object model)
    {
        var letter = await Letter(model);

        var body = LetterBody.Build(letter).Should().BeOfType<MultipartAlternative>().Subject;

        body.Should().HaveCount(2);
        body[0].Should().BeOfType<TextPart>().Which.IsPlain.Should().BeTrue();
        Bytes(body[1]).Should().Equal(Bytes(HtmlAsItWas(letter)),
            "the half a client draws is the letter as it was, to the byte");
    }

    [Theory]
    [MemberData(nameof(EveryLetter))]
    public async Task SayEverythingTheHtmlSays(object model)
    {
        var letter = await Letter(model);

        var text = TextOf(letter);

        text.Should().Contain(Match(letter.Body, "<h1[^>]*>(.*?)</h1>").Single(),
            "the title of the letter is its first line");
        text.Should().Contain(Footnote);
        foreach (var address in Match(letter.Body, "href=\"([^\"]+)\""))
        {
            text.Should().Contain(address,
                "a reader without HTML follows the button by its address, the other doors included");
        }

        text.Should().NotContain("<", "no markup reaches the text");
        text.Should().NotContain("cid:", "the logo has no words to say");
    }

    /// <summary>
    /// The whole of one letter, written out: this is what a reader without HTML
    /// reads, and it is read in this order.
    /// </summary>
    [Fact]
    public async Task ReadTheWayTheLetterReads()
    {
        var letter = await Letter(new RegistrationConfirmationViewModel("https://dm.am/activate#token=2f1c"));

        TextOf(letter).Should().Be(
            "Регистрация\n" +
            "\n" +
            "Ссылка для выбора имени и завершения регистрации действительна 48 часов\n" +
            "\n" +
            "Выбрать имя: https://dm.am/activate#token=2f1c\n" +
            "\n" +
            "Ссылка через RU-прокси: https://ru.l.dm.am/activate#token=2f1c\n" +
            "\n" +
            "-- \n" +
            Footnote + "\n");
    }

    /// <summary>A row of the facts reads as a label and its value.</summary>
    [Fact]
    public async Task ReadARowOfFactsAsALabelAndItsValue()
    {
        var letter = await Letter(new SuspiciousLoginViewModel(
            "203.0.113.9", "Firefox на Linux", "30.07.2026 15:00", "Россия"));

        var text = TextOf(letter);

        text.Should().Contain("Когда: 30.07.2026 15:00\nIP-адрес: 203.0.113.9\nСтрана: Россия\nУстройство: Firefox на Linux\n");
        text.Should().EndWith("-- \n" + Footnote + "\n",
            "the country table owes its publisher no line, so the footnote closes this letter too");
    }

    /// <summary>
    /// The moderator's words are text: escaped once for the HTML, and shown as
    /// typed in the text, not as the entities that escaped them.
    /// </summary>
    [Fact]
    public async Task ShowTheModeratorsWordsAsTyped()
    {
        var letter = await Letter(new UsernameChangeRejectionViewModel("<b>имя занято</b> & другое"));

        TextOf(letter).Should().Contain("<b>имя занято</b> & другое");
    }

    private async Task<EmailLetter> Letter(object model)
    {
        var renderer = _services.GetRequiredService<ITemplateRenderer>();

        // RenderAsync is generic over the model type and the lookup uses that type,
        // so a call through object would look up object.
        var render = typeof(ITemplateRenderer).GetMethod(nameof(ITemplateRenderer.RenderAsync))!
            .MakeGenericMethod(model.GetType());
        var html = await (Task<string>)render.Invoke(renderer, [model])!;

        return new EmailLetter
        {
            Address = "reader@example.com",
            Subject = "Dungeon Master",
            Body = html,
            LinkedResources =
            [
                new LinkedResource { ContentId = "logo", MimeType = "image/png", Content = [0x89, 0x50, 0x4E, 0x47] }
            ]
        };
    }

    /// <summary>
    /// The text half as the reader gets it. The part stores its lines the way
    /// the wire carries them, ended by CRLF; the comparisons here read lines.
    /// </summary>
    private static string TextOf(EmailLetter letter) =>
        ((TextPart)((MultipartAlternative)LetterBody.Build(letter))[0]).Text.ReplaceLineEndings("\n");

    /// <summary>
    /// The body as the worker built it before the text half existed, kept here
    /// as the reference the HTML half is compared against.
    /// </summary>
    private static MimeEntity HtmlAsItWas(EmailLetter letter)
    {
        var html = new TextPart(TextFormat.Html) { Text = letter.Body };
        var related = new MultipartRelated { html };
        foreach (var resource in letter.LinkedResources)
        {
            related.Add(new MimePart(resource.MimeType)
            {
                Content = new MimeContent(new MemoryStream(resource.Content)),
                ContentId = resource.ContentId,
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                ContentTransferEncoding = ContentEncoding.Base64
            });
        }

        return related;
    }

    /// <summary>
    /// The entity as it goes over the wire, with the one random part pinned: a
    /// multipart draws a fresh boundary every time it is built.
    /// </summary>
    private static byte[] Bytes(MimeEntity entity)
    {
        if (entity is Multipart multipart)
        {
            multipart.Boundary = "=-pinned-for-comparison";
        }

        using var stream = new MemoryStream();
        entity.WriteTo(stream);
        return stream.ToArray();
    }

    private static string[] Match(string html, string pattern) =>
        Regex.Matches(html, pattern, RegexOptions.Singleline)
            .Select(match => System.Net.WebUtility.HtmlDecode(match.Groups[1].Value))
            .ToArray();

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
