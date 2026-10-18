using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc cref="IEmailChangeNoticeMailSender" />
internal class EmailChangeNoticeMailSender : AccountMailSender, IEmailChangeNoticeMailSender
{
    public EmailChangeNoticeMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task SendRequested(string currentEmail, string newEmail)
    {
        const string subject = "Dungeon Master: запрос на смену почты";
        return Send(currentEmail, subject, new EmailChangeWarningViewModel(
            CurrentEmail: currentEmail,
            NewEmail: newEmail));
    }

    /// <inheritdoc />
    public Task SendCompleted(string previousEmail, string newEmail)
    {
        const string subject = "Dungeon Master: почта изменена";
        return Send(previousEmail, subject, new EmailChangeCompletedViewModel(
            PreviousEmail: previousEmail,
            NewEmail: newEmail,
            SupportUrl: SiteLink("support?reason=access")));
    }
}
