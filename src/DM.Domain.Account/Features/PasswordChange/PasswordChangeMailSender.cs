using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.PasswordChange;

/// <inheritdoc cref="IPasswordChangeMailSender" />
internal class PasswordChangeMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IPasswordChangeMailSender
{
    /// <inheritdoc />
    public Task Send(string email)
    {
        return Deliver(email, "пароль изменен", new PasswordChangeNotificationViewModel(
            SupportUrl: SiteLink("support?reason=access")));
    }
}
