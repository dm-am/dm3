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
internal class TwoFactorRemovalMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), ITwoFactorRemovalMailSender
{
    /// <inheritdoc />
    public Task SendRequest(string email, Guid secret)
    {
        return Deliver(email, "запрос на отключение двухфакторной аутентификации",
            new TwoFactorRemovalRequestViewModel(
                ConfirmationLinkUrl: ConfirmationLink("two-factor/removal", secret)));
    }

    /// <inheritdoc />
    public Task SendScheduled(string email, DateTimeOffset dueUtc, Guid secret, string? timeZoneId)
    {
        return Deliver(email, "отключение двухфакторной аутентификации назначено",
            new TwoFactorRemovalScheduledViewModel(
                Due: ReaderTime.Format(dueUtc, timeZoneId),
                CancellationLinkUrl: ConfirmationLink("two-factor/removal/cancel", secret)));
    }
}
