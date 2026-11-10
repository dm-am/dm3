using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Geo;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Mail;

public class AccountLetterShould : UnitTestBase
{
    private const string Reader = "reader@dm.am";

    private readonly ITemplateRenderer _renderer = Mock<ITemplateRenderer>();
    private readonly IMailSender _mail = Mock<IMailSender>();
    private readonly IEmailAssetsProvider _assets = Mock<IEmailAssetsProvider>();

    private readonly IOptions<SiteAddressConfiguration> _site =
        Options.Create(new SiteAddressConfiguration { PublicUrl = "https://dm.am" });

    // None of the hours is a default, so a letter that kept its own number would show.
    private readonly IOptions<TokenConfiguration> _tokens = Options.Create(new TokenConfiguration
    {
        PasswordResetTokenLifetimeHours = 21,
        ActivationTokenLifetimeHours = 22,
        UsernameChangeApprovalTokenLifetimeHours = 23
    });

    public static TheoryData<string, string> Subjects => new()
    {
        { "registration", "Dungeon Master: подтвердите почту" },
        { "password reset", "Dungeon Master: восстановление пароля" },
        { "password change", "Dungeon Master: пароль изменен" },
        { "email change", "Dungeon Master: смена почты" },
        { "email change requested", "Dungeon Master: запрос на смену почты" },
        { "email change completed", "Dungeon Master: почта изменена" },
        { "removal", "Dungeon Master: аккаунт удален" },
        { "restoration", "Dungeon Master: аккаунт восстановлен" },
        { "two-factor removal request", "Dungeon Master: запрос на отключение двухфакторной аутентификации" },
        { "two-factor removal scheduled", "Dungeon Master: отключение двухфакторной аутентификации назначено" },
        { "username approval", "Dungeon Master: смена имени одобрена" },
        { "username rejection", "Dungeon Master: смена имени отклонена" },
        { "unfamiliar device", "Dungeon Master: вход с незнакомого устройства" },
    };

    private Task Send(string letter) => letter switch
    {
        "registration" => new RegistrationMailSender(_renderer, _mail, _assets, _site, _tokens).Send(Reader, Guid.NewGuid()),
        "password reset" => new PasswordResetMailSender(_renderer, _mail, _assets, _site, _tokens).Send(Reader, Guid.NewGuid()),
        "password change" => new PasswordChangeMailSender(_renderer, _mail, _assets, _site).Send(Reader),
        "email change" => new EmailChangeMailSender(_renderer, _mail, _assets, _site).Send(Reader, Guid.NewGuid()),
        "email change requested" => new EmailChangeNoticeMailSender(_renderer, _mail, _assets, _site).SendRequested(Reader, "next@dm.am"),
        "email change completed" => new EmailChangeNoticeMailSender(_renderer, _mail, _assets, _site).SendCompleted(Reader, "next@dm.am"),
        "removal" => new AccountRemovalMailSender(_renderer, _mail, _assets, _site).SendCompleted(Reader),
        "restoration" => new AccountRestorationMailSender(_renderer, _mail, _assets, _site).SendRestored(Reader),
        "two-factor removal request" => new TwoFactorRemovalMailSender(_renderer, _mail, _assets, _site).SendRequest(Reader, Guid.NewGuid()),
        "two-factor removal scheduled" => new TwoFactorRemovalMailSender(_renderer, _mail, _assets, _site)
            .SendScheduled(Reader, DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid(), null),
        "username approval" => new UsernameChangeMailSender(_renderer, _mail, _assets, _site, _tokens).SendApprovalAsync(Reader, Guid.NewGuid()),
        "username rejection" => new UsernameChangeMailSender(_renderer, _mail, _assets, _site, _tokens).SendRejectionAsync(Reader, null),
        "unfamiliar device" => new SuspiciousLoginNotificationSender(
            _renderer, _mail, _assets, Mock<IIpCountryLookup>(), Mock<IDateTimeProvider>()).SendAsync(Reader, null, null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(letter), letter, "no such letter")
    };

    // A mailbox filter matches the start of the subject, so every letter opens with the site's
    // name.
    [Theory]
    [MemberData(nameof(Subjects))]
    public async Task GoOutUnderTheSubjectThatOpensWithTheSiteName(string letter, string subject)
    {
        await Send(letter);

        await _mail.Received(1).SendAsync(Arg.Is<EmailLetter>(sent =>
            sent.Subject == subject && sent.Subject.StartsWith(MailSubject.Prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void WriteTheSiteNameOnceInTheSubject() =>
        MailSubject.Of("пароль изменен").Should().Be("Dungeon Master: пароль изменен");

    [Fact]
    public async Task GiveTheResetLinkTheHoursTheSettingsGiveIt()
    {
        await Send("password reset");

        await _renderer.Received(1).RenderAsync(Arg.Is<PasswordResetConfirmationViewModel>(model => model.LinkLifetimeHours == 21));
    }

    [Fact]
    public async Task GiveTheActivationLinkTheHoursTheSettingsGiveIt()
    {
        await Send("registration");

        await _renderer.Received(1).RenderAsync(Arg.Is<RegistrationConfirmationViewModel>(model => model.LinkLifetimeHours == 22));
    }

    [Fact]
    public async Task GiveTheApprovalLinkTheHoursTheSettingsGiveIt()
    {
        await Send("username approval");

        await _renderer.Received(1).RenderAsync(Arg.Is<UsernameChangeApprovalViewModel>(model => model.LinkLifetimeHours == 23));
    }
}
