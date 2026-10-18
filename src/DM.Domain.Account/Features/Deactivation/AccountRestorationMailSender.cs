using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc cref="IAccountRestorationMailSender" />
internal class AccountRestorationMailSender : AccountMailSender, IAccountRestorationMailSender
{
    public AccountRestorationMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task SendRestored(string email)
    {
        const string subject = "Dungeon Master: аккаунт восстановлен";
        return Send(email, subject, new AccountRestoredViewModel(SiteLink("?action=login")));
    }
}
