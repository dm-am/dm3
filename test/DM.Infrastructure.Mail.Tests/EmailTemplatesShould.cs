using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using DM.Infrastructure.Mail.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

/// <summary>
/// Every account letter has a template, and its body is a letter.
/// </summary>
/// <remarks>
/// The renderer looks up a Razor component by the model type and used to fall
/// back to JsonSerializer.Serialize when it found none. The project shipped with
/// no components at all, so that fallback was the only path there was: a
/// registering user received {"ConfirmationLinkUrl":"http://..."} as the body of
/// their letter, and the same for password reset, email change, password change
/// and the unfamiliar-device warning. Nothing threw and nothing was logged.
///
/// Coverage is asserted from the view-model side rather than by listing the five
/// templates, because the way this comes back is a sixth letter whose model has no
/// component — and a list of five would still be green.
/// </remarks>
public class EmailTemplatesShould : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly HtmlRenderer _htmlRenderer;
    private readonly TemplateRenderer _renderer;

    public EmailTemplatesShould()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _services = services.BuildServiceProvider();
        _htmlRenderer = EmailRendering.CreateHtmlRenderer(_services.GetRequiredService<ILoggerFactory>());
        _renderer = new TemplateRenderer(NullLogger<TemplateRenderer>.Instance, _htmlRenderer);
    }

    /// <summary>Every view model the domain declares, found by reflection.</summary>
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
        ViewModels().Should().HaveCountGreaterOrEqualTo(5);
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

        var html = await _renderer.RenderAsync(new RegistrationConfirmationViewModel(url));

        html.Should().NotStartWith("{", "a JSON body is what this defect looked like");
        html.Should().Contain("<html");
        html.Should().Contain(url);
        html.Should().Contain("cid:logo", "the logo travels with the letter, not over the network");
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
        { new RegistrationConfirmationViewModel("https://dm.am/activate/a"), "https://dm.am/activate/a" },
        { new PasswordResetConfirmationViewModel("user", "https://dm.am/reset/b"), "https://dm.am/reset/b" },
        { new EmailChangeConfirmationViewModel("user", "https://dm.am/email/c"), "https://dm.am/email/c" },
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
        new RegistrationConfirmationViewModel("https://dm.am/activate/a"),
        new PasswordResetConfirmationViewModel("user", "https://dm.am/reset/b"),
        new EmailChangeConfirmationViewModel("user", "https://dm.am/email/c"),
        new PasswordChangeNotificationViewModel("user"),
        new SuspiciousLoginViewModel("user", "203.0.113.9", "Firefox", "30.07.2026 15:00"),
    };

    /// <summary>
    /// A username reaches the letter as text, and the templates put it there with
    /// Razor's own output, which escapes. The senders this replaces interpolated it
    /// into a hand-written HTML string, where it did not.
    /// </summary>
    [Fact]
    public async Task EscapeWhatComesFromTheUser()
    {
        var html = await _renderer.RenderAsync(
            new PasswordChangeNotificationViewModel("<script>alert(1)</script>"));

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    /// <summary>
    /// Escaped, but not escaped into unreadability. The default encoder turns
    /// everything outside Basic Latin into numeric references, so a Russian name
    /// left as &amp;#x410;&amp;#x43B;… — valid, three times longer, and unlike the
    /// literal text of the template beside it.
    /// </summary>
    [Fact]
    public async Task WriteRussianNamesAsLettersRatherThanNumericReferences()
    {
        var html = await _renderer.RenderAsync(new PasswordChangeNotificationViewModel("Аллигатор"));

        html.Should().Contain("Аллигатор");
        html.Should().NotContain("&#x41");
    }

    /// <summary>
    /// The unfamiliar-device letter takes address and device as optional: the
    /// detector may have neither. A row printing "Устройство:" and nothing else
    /// reads as broken, so the row is not printed.
    /// </summary>
    [Fact]
    public async Task LeaveOutTheFactsTheDetectorDidNotHave()
    {
        var html = await _renderer.RenderAsync(
            new SuspiciousLoginViewModel("user", IpAddress: null, DeviceInfo: null, "30.07.2026 15:00"));

        html.Should().NotContain("Адрес");
        html.Should().NotContain("Устройство");
        html.Should().Contain("30.07.2026 15:00");
    }

    /// <summary>
    /// Loud, not JSON. The silent fallback is what let five broken letters ship.
    /// </summary>
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
    /// answered 400. This builds the container out of MailModule rather than
    /// constructing the renderer by hand, because the lifetimes are the defect.
    /// </remarks>
    [Fact]
    public async Task KeepRenderingAfterAScopeThatUsedItHasEnded()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(NullLoggerFactory.Instance).As<ILoggerFactory>();
        builder.RegisterGeneric(typeof(NullLogger<>)).As(typeof(ILogger<>)).SingleInstance();
        builder.RegisterModule<MailModule>();
        await using var container = builder.Build();

        string first;
        await using (var scope = container.BeginLifetimeScope())
        {
            first = await scope.Resolve<ITemplateRenderer>()
                .RenderAsync(new PasswordChangeNotificationViewModel("user"));
        }

        await using var second = container.BeginLifetimeScope();
        var afterwards = await second.Resolve<ITemplateRenderer>()
            .RenderAsync(new PasswordChangeNotificationViewModel("user"));

        first.Should().NotBeEmpty();
        afterwards.Should().NotBeEmpty("the first scope must not take the renderer with it");
        afterwards.Should().Be(first);
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
