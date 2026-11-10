using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using DM.Infrastructure.Mail.Rendering;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

// Coverage is asserted from the view-model side rather than by listing the five templates, because
// the way this comes back is a sixth letter whose model has no component — and a list of five would
// still be green.
public class EmailTemplatesShould : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly HtmlRenderer _htmlRenderer;
    private readonly TemplateRenderer _renderer;

    // Deliberately a host of nobody: the footer must name the addresses of the site, and taking
    // them from here instead would pass while naming whatever the stand happens to answer on.
    private static readonly SiteAddressConfiguration Deployment = new()
    {
        PublicUrl = "https://example.test",
    };

    public EmailTemplatesShould()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _services = services.BuildServiceProvider();
        _htmlRenderer = EmailRendering.CreateHtmlRenderer(
            _services.GetRequiredService<ILoggerFactory>(),
            Deployment);
        _renderer = new TemplateRenderer(_htmlRenderer);
    }

    // A letter talks about its errand and not about the doors of the site.
    [Fact]
    public async Task NameNoAddressOfItsOwn()
    {
        var body = await _renderer.RenderAsync(
            new PasswordChangeNotificationViewModel("https://example.test/support"));

        foreach (var address in DM.Domain.Core.Site.SiteAddresses.All)
        {
            body.Should().NotContain(address.Name,
                $"the footer of a letter has nothing to say about {address.Name}");
            body.Should().NotContain(address.Title,
                $"the footer of a letter has nothing to say about {address.Title}");
        }
    }

    // The addresses are printed by the shared layout, so a template that draws its own frame
    // silently loses them.
    [Fact]
    public void BuildEveryLetterOnTheSharedLayout()
    {
        var templates = Directory
            .EnumerateFiles(TemplatesDirectory(), "*.razor")
            .Where(path => !Path.GetFileName(path).StartsWith('_'))
            // The shared components are not letters: one draws the frame, one
            // the button and the addresses under it, one the lifetime of a link.
            .Where(path => Path.GetFileNameWithoutExtension(path) != "EmailLayout")
            .Where(path => Path.GetFileNameWithoutExtension(path) != "ActionButton")
            .Where(path => Path.GetFileNameWithoutExtension(path) != "LinkLifetime")
            .ToList();

        templates.Should().HaveCountGreaterThanOrEqualTo(5,
            "a directory scan that stops matching turns this green by checking nothing");

        foreach (var template in templates)
        {
            File.ReadAllText(template).Should().Contain("<EmailLayout",
                $"{Path.GetFileName(template)} draws its own frame and drops everything the shared one carries");
        }
    }

    // A letter carries no font of its own — a mailbox does not fetch one — so the reader gets the
    // fallback, and Arial is wider than the PT Sans the site draws with.
    [Fact]
    public void KeepEveryLetterHeadingOnOneLine()
    {
        const int cap = 24;

        var headings = Directory
            .EnumerateFiles(TemplatesDirectory(), "*.razor")
            .Select(path => (File: Path.GetFileName(path), Title: TitleOf(path)))
            .Where(pair => pair.Title != null)
            .ToList();

        headings.Should().HaveCountGreaterThanOrEqualTo(5,
            "a scan that stops matching turns this green by checking nothing");

        foreach (var (file, title) in headings)
        {
            title!.Length.Should().BeLessThanOrEqualTo(cap,
                $"{file} carries a heading of {title.Length} characters, and the card has room for {cap}");
        }
    }

    private static string? TitleOf(string template)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(template), "<EmailLayout Title=\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    // Templates are content, not build output, so they are read from the source tree.
    private static string TemplatesDirectory() =>
        Path.Combine(DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Mail", "Templates");

    public static TheoryData<Type> ViewModelTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in ViewModels())
        {
            data.Add(type);
        }

        return data;
    }

    private static IReadOnlyCollection<Type> ViewModels() => typeof(RegistrationConfirmationViewModel)
        .Assembly.GetTypes()
        .Where(t => t.Namespace == typeof(RegistrationConfirmationViewModel).Namespace
                    && t is { IsClass: true, IsAbstract: false })
        .ToList();

    [Fact]
    public void FindTheViewModelsToCheck()
    {
        // A reflection filter that stops matching would turn the theory green by
        // running it zero times.
        ViewModels().Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Theory]
    [MemberData(nameof(ViewModelTypes))]
    public void HaveATemplateForEveryViewModel(Type viewModel)
    {
        TemplateRenderer.GetAvailableComponents().Should().ContainKey(viewModel,
            "a model with no template cannot be sent at all");
    }

    [Fact]
    public async Task RenderTheRegistrationLetterAsHtmlCarryingItsLink()
    {
        const string url = "https://dm.am/activate/2f1c";

        var html = await _renderer.RenderAsync(new RegistrationConfirmationViewModel(url, 48));

        html.Should().NotStartWith("{", "a JSON body is what this defect looked like");
        html.Should().Contain("<html");
        html.Should().Contain(url);
        html.Should().Contain("cid:logo", "the logo travels with the letter, not over the network");
    }

    // The button of a letter names one address, and a reader whose address stopped answering cannot
    // rebuild the link by hand: the token sits after the hash, where a browser bar never takes it.
    [Fact]
    public async Task OfferTheSameStepThroughTheOtherAddressesOfTheSite()
    {
        var html = await _renderer.RenderAsync(
            new RegistrationConfirmationViewModel("https://dm.am/activate#token=2f1c", 48));

        html.Should().Contain("Ссылка через RU-прокси");
        html.Should().Contain("https://ru.l.dm.am/activate#token=2f1c",
            "the other door has to open the same step, token and all");
    }

    // The link names the other address by its full name: the sentence has no heading over it, and
    // "Ссылка через Основной" says nothing of what that is.
    [Fact]
    public async Task NameTheOtherAddressByItsFullName()
    {
        var html = await _renderer.RenderAsync(
            new RegistrationConfirmationViewModel("https://ru.l.dm.am/activate#token=2f1c", 48));

        html.Should().Contain("Ссылка через Основной сервер</a>");
        html.Should().Contain("https://dm.am/activate#token=2f1c");
        html.Should().NotContain("Ссылка через RU-прокси", "a letter does not offer the door it came through");
    }

    // A stand answers on an address nobody else does, and a letter from it that pointed at the
    // proxy would promise a page that is not there.
    [Fact]
    public async Task OfferNothingWhenTheLetterComesFromAForeignAddress()
    {
        var html = await _renderer.RenderAsync(
            new RegistrationConfirmationViewModel("http://localhost:5173/activate#token=2f1c", 48));

        html.Should().NotContain("Ссылка через");
    }

    // The offer is drawn by the shared button, so a template that draws its own anchor loses it
    // silently.
    [Fact]
    public void DrawEveryButtonWithTheSharedComponent()
    {
        var templates = Directory
            .EnumerateFiles(TemplatesDirectory(), "*.razor")
            .Where(path => !Path.GetFileName(path).StartsWith('_'))
            .Where(path => Path.GetFileNameWithoutExtension(path) != "ActionButton")
            .ToList();

        templates.Should().HaveCountGreaterThanOrEqualTo(5,
            "a directory scan that stops matching turns this green by checking nothing");

        foreach (var template in templates)
        {
            File.ReadAllText(template).Should().NotContain("EmailConstants.ButtonStyles",
                $"{Path.GetFileName(template)} draws its own button and drops the other addresses");
        }
    }

    [Theory]
    [MemberData(nameof(LettersWithALink))]
    public async Task CarryTheLinkOfEveryLetterThatHasOne(object model, string url)
    {
        var html = await Render(model);

        html.Should().NotStartWith("{");
        html.Should().Contain(url, "a confirmation letter without its link is useless");
    }

    public static TheoryData<object, string> LettersWithALink() => new()
    {
        { new RegistrationConfirmationViewModel("https://dm.am/activate/a", 48), "https://dm.am/activate/a" },
        { new PasswordResetConfirmationViewModel("https://dm.am/reset/b", 48), "https://dm.am/reset/b" },
        { new EmailChangeConfirmationViewModel("https://dm.am/email/c"), "https://dm.am/email/c" },
        { new UsernameChangeApprovalViewModel("https://dm.am/username/d", 48), "https://dm.am/username/d" },
        { NotificationLetterShould.AboutAGame, NotificationLetterShould.AboutAGame.LinkUrl! },
    };

    // The line is a warning about what the box holds, so it reads as a warning only once the box
    // has been read: standing first, it warned about something the reader had not been shown yet.
    [Theory]
    [MemberData(nameof(LettersWithABoxOfAddresses))]
    public async Task SayWhatToDoBelowTheBoxOfAddresses(object model, string line, string firstLabel)
    {
        var html = await Render(model);

        var lineAt = html.IndexOf(line, StringComparison.Ordinal);
        // The label cell rather than the words, which also stand in the comment
        // the template carries — the comment is not rendered, but the assertion
        // should not depend on that.
        var boxAt = html.IndexOf($">{firstLabel}</td>", StringComparison.Ordinal);

        lineAt.Should().BeGreaterThanOrEqualTo(0, "the letter says what to do");
        boxAt.Should().BeGreaterThanOrEqualTo(0, "the letter shows the addresses");
        lineAt.Should().BeGreaterThan(boxAt, "the warning reads over the addresses it is about");
    }

    public static TheoryData<object, string, string> LettersWithABoxOfAddresses() => new()
    {
        {
            new EmailChangeWarningViewModel("now@dm.am", "next@dm.am"),
            "срочно обновите пароль",
            "Текущий адрес"
        },
        {
            new EmailChangeCompletedViewModel("before@dm.am", "next@dm.am", "https://dm.am/support"),
            "срочно обратитесь в поддержку",
            "Прежний адрес"
        },
    };

    [Theory]
    [MemberData(nameof(EveryLetter))]
    public async Task RenderEveryLetterAsHtml(object model)
    {
        var html = await Render(model);

        html.Should().NotStartWith("{");
        html.Should().Contain("<html");
        html.Should().Contain("cid:logo");
    }

    public static TheoryData<object> EveryLetter() => new()
    {
        new RegistrationConfirmationViewModel("https://dm.am/activate/a", 48),
        new PasswordResetConfirmationViewModel("https://dm.am/reset/b", 48),
        new EmailChangeConfirmationViewModel("https://dm.am/email/c"),
        new PasswordChangeNotificationViewModel("https://example.test/support"),
        new SuspiciousLoginViewModel("203.0.113.9", "Firefox", "30.07.2026 15:00"),
        new UsernameChangeApprovalViewModel("https://dm.am/username/d", 48),
        new UsernameChangeRejectionViewModel("имя занято"),
        new EmailChangeWarningViewModel("now@dm.am", "next@dm.am"),
        new EmailChangeCompletedViewModel("before@dm.am", "next@dm.am", "https://dm.am/support"),
        new AccountRemovedViewModel("https://dm.am/support"),
        // A letter of one button: the line it used to carry said the account is
        // back, which the title says already.
        new AccountRestoredViewModel("https://dm.am/login"),
        NotificationLetterShould.AboutAGame,
    };

    // A username reaches the letter as text, and the templates put it there with Razor's own
    // output, which escapes.
    [Fact]
    public async Task EscapeWhatComesFromTheUser()
    {
        var html = await _renderer.RenderAsync(
            new UsernameChangeRejectionViewModel(Reason: "<script>alert(1)</script>"));

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    // The default encoder turns everything outside Basic Latin into numeric references.
    [Fact]
    public async Task WriteRussianNamesAsLettersRatherThanNumericReferences()
    {
        var html = await _renderer.RenderAsync(new UsernameChangeRejectionViewModel(Reason: "Аллигатор"));

        html.Should().Contain("Аллигатор");
        html.Should().NotContain("&#x41");
    }

    // The address is what the row is about, and the country is what makes it readable: few people
    // know their own address, and on a phone it changes with the connection.
    [Fact]
    public async Task PutTheCountryUnderTheAddressItExplains()
    {
        var html = await Render(new SuspiciousLoginViewModel(
            IpAddress: "203.0.113.42",
            DeviceInfo: "Chrome на Windows",
            Timestamp: "30.07.2026 15:00",
            Country: "Россия"));

        var countryAt = html.IndexOf(">Страна</td>", StringComparison.Ordinal);
        var addressAt = html.IndexOf(">IP-адрес</td>", StringComparison.Ordinal);

        countryAt.Should().BeGreaterThanOrEqualTo(0, "the letter names the country");
        addressAt.Should().BeGreaterThanOrEqualTo(0, "the letter names the address");
        addressAt.Should().BeLessThan(countryAt, "the country explains the address above it");
        html.Should().Contain("Россия");
    }

    // A letter that names a country ends where one that names none does, on the footnote: the
    // country table is published into the public domain (PDDL), and its publisher is owed no line
    // under the card.
    [Fact]
    public async Task EndOnTheFootnoteWhenItNamesACountry()
    {
        var named = await Render(new SuspiciousLoginViewModel(
            "203.0.113.42", "Chrome на Windows", "30.07.2026 15:00", Country: "Россия"));
        var unnamed = await Render(new SuspiciousLoginViewModel(
            "203.0.113.42", "Chrome на Windows", "30.07.2026 15:00"));

        named.Should().Contain("Россия");
        named.Should().NotContain("db-ip");
        named.Should().NotContain("Geolocation");
        AfterTheFootnote(named).Should().Be(AfterTheFootnote(unnamed),
            "nothing is printed under the footnote, the country named or not");
    }

    // A row printing "Устройство:" and nothing else reads as broken, so the row is not printed.
    [Fact]
    public async Task LeaveOutTheFactsTheDetectorDidNotHave()
    {
        var html = await _renderer.RenderAsync(
            new SuspiciousLoginViewModel(IpAddress: null, DeviceInfo: null, "30.07.2026 15:00"));

        html.Should().NotContain(">Страна</td>");

        // The label cell, not the word: a bare substring over the whole document
        // also matched the footer, which names the addresses of the site and has
        // nothing to do with the row this test is about.
        html.Should().NotContain(">IP-адрес</td>");
        html.Should().NotContain(">Устройство</td>");
        html.Should().Contain("30.07.2026 15:00");
    }

    // The moderator's comment is the only free-form text these letters carry, and it reaches the
    // reader escaped exactly once.
    [Fact]
    public async Task EscapeTheModeratorsCommentExactlyOnce()
    {
        var html = await _renderer.RenderAsync(
            new UsernameChangeRejectionViewModel("<b>имя занято</b>"));

        html.Should().Contain("&lt;b&gt;имя занято&lt;/b&gt;",
            "a comment is text, and the tag a moderator typed is shown as typed");
        html.Should().NotContain("&amp;lt;",
            "encoding it twice puts the entity itself in front of the reader");
    }

    // A request may be rejected without a comment, and an empty box under the title looks broken,
    // so the letter says in words that there was no comment.
    [Fact]
    public async Task NameTheMissingReasonRatherThanDrawAnEmptyBox()
    {
        var html = await _renderer.RenderAsync(
            new UsernameChangeRejectionViewModel(Reason: null));

        html.Should().Contain("Модератор не назвал причину");
        html.Should().NotContain(EmailConstants.BoxStyles,
            "the box holds the moderator word, and there is none");
    }

    // The silent fallback is what let five broken letters ship.
    [Fact]
    public async Task RefuseAModelWithNoTemplate()
    {
        var act = () => _renderer.RenderAsync(new { Whatever = 1 });

        await act.Should().ThrowAsync<TemplateRenderException>();
    }

    /// <summary>
    /// Two letters in a row, through the real registrations, from two scopes.
    /// </summary>
    /// <remarks>
    /// The renderer used to be disposable and disposed the HtmlRenderer it was
    /// given — a singleton it did not own. Registered per dependency, it took the
    /// shared renderer down with the first scope that ended, and every letter after
    /// that came out empty; the validator then refused the letter and registration
    /// answered 400. This builds the container out of AddDmMail rather than
    /// constructing the renderer by hand, because the lifetimes are the defect.
    /// </remarks>
    [Fact]
    public async Task KeepRenderingAfterAScopeThatUsedItHasEnded()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<SiteAddressConfiguration>>(Options.Create(Deployment));
        services.AddDmMail();
        await using var provider = services.BuildServiceProvider();

        string first;
        await using (var scope = provider.CreateAsyncScope())
        {
            first = await scope.ServiceProvider.GetRequiredService<ITemplateRenderer>()
                .RenderAsync(new PasswordChangeNotificationViewModel("https://example.test/support"));
        }

        await using var second = provider.CreateAsyncScope();
        var afterwards = await second.ServiceProvider.GetRequiredService<ITemplateRenderer>()
            .RenderAsync(new PasswordChangeNotificationViewModel("https://example.test/support"));

        first.Should().NotBeEmpty();
        afterwards.Should().NotBeEmpty("the first scope must not take the renderer with it");
        afterwards.Should().Be(first);
    }

    private static string AfterTheFootnote(string html)
    {
        const string footnote = "не требует ответа</p>";
        var at = html.IndexOf(footnote, StringComparison.Ordinal);
        at.Should().BeGreaterThanOrEqualTo(0, "every letter carries the footnote");
        return html[(at + footnote.Length)..];
    }

    private Task<string> Render(object model)
    {
        // RenderAsync is generic over the model type and the lookup uses that type,
        // so a call through object would look up object.
        var method = typeof(TemplateRenderer).GetMethod(nameof(TemplateRenderer.RenderAsync))!
            .MakeGenericMethod(model.GetType());
        return (Task<string>)method.Invoke(_renderer, new[] { model })!;
    }

    public async ValueTask DisposeAsync()
    {
        await _htmlRenderer.DisposeAsync();
        await _services.DisposeAsync();
    }
}
