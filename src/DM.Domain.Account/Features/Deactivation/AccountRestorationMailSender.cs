using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc cref="IAccountRestorationMailSender" />
internal class AccountRestorationMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IAccountRestorationMailSender
{
    /// <inheritdoc />
    public Task SendRestored(string email)
    {
        return Deliver(email, "аккаунт восстановлен", new AccountRestoredViewModel(SiteLink("?action=login")));
    }
}
