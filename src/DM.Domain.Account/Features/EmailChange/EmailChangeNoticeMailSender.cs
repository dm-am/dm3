using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc cref="IEmailChangeNoticeMailSender" />
internal class EmailChangeNoticeMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IEmailChangeNoticeMailSender
{
    /// <inheritdoc />
    public Task SendRequested(string currentEmail, string newEmail)
    {
        return Deliver(currentEmail, "запрос на смену почты", new EmailChangeWarningViewModel(
            CurrentEmail: currentEmail,
            NewEmail: newEmail));
    }

    /// <inheritdoc />
    public Task SendCompleted(string previousEmail, string newEmail)
    {
        return Deliver(previousEmail, "почта изменена", new EmailChangeCompletedViewModel(
            PreviousEmail: previousEmail,
            NewEmail: newEmail,
            SupportUrl: SiteLink("support?reason=access")));
    }
}
