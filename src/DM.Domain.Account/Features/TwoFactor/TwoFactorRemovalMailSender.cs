using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
internal class TwoFactorRemovalMailSender : AccountMailSender, ITwoFactorRemovalMailSender
{
    public TwoFactorRemovalMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task SendRequest(string email, string username, Guid secret)
    {
        var subject = $"Dungeon Master: запрос на снятие второго фактора для {username}";
        return Send(email, subject, new TwoFactorRemovalRequestViewModel(
            Username: username,
            ConfirmationLinkUrl: ConfirmationLink("two-factor/removal", secret)));
    }

    /// <inheritdoc />
    public Task SendScheduled(string email, string username, DateTimeOffset dueUtc, Guid secret)
    {
        var subject = $"Dungeon Master: второй фактор для {username} будет снят";
        return Send(email, subject, new TwoFactorRemovalScheduledViewModel(
            Username: username,
            DueUtc: dueUtc,
            CancellationLinkUrl: ConfirmationLink("two-factor/removal/cancel", secret)));
    }
}
