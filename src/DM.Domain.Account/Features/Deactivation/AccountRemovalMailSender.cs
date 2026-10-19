using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc cref="IAccountRemovalMailSender" />
internal class AccountRemovalMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IAccountRemovalMailSender
{
    /// <inheritdoc />
    public Task SendCompleted(string email)
    {
        return Deliver(email, "аккаунт удален", new AccountRemovedViewModel(
            SupportUrl: SiteLink("support?reason=removed")));
    }
}
