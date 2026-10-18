using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc cref="IAccountRemovalMailSender" />
internal class AccountRemovalMailSender : AccountMailSender, IAccountRemovalMailSender
{
    public AccountRemovalMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task SendCompleted(string email)
    {
        const string subject = "Dungeon Master: аккаунт удален";
        return Send(email, subject, new AccountRemovedViewModel(
            SupportUrl: SiteLink("support?reason=removed")));
    }
}
