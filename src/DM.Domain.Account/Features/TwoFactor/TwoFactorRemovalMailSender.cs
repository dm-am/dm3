using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dates;
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
    public Task SendRequest(string email, Guid secret)
    {
        const string subject = "Dungeon Master: запрос на отключение двухфакторной аутентификации";
        return Send(email, subject, new TwoFactorRemovalRequestViewModel(
            ConfirmationLinkUrl: ConfirmationLink("two-factor/removal", secret)));
    }

    /// <inheritdoc />
    public Task SendScheduled(string email, DateTimeOffset dueUtc, Guid secret, string? timeZoneId)
    {
        const string subject = "Dungeon Master: отключение двухфакторной аутентификации назначено";
        return Send(email, subject, new TwoFactorRemovalScheduledViewModel(
            Due: ReaderTime.Format(dueUtc, timeZoneId),
            CancellationLinkUrl: ConfirmationLink("two-factor/removal/cancel", secret)));
    }
}
